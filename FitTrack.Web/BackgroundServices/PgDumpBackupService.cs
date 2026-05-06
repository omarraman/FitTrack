using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FitTrack.Web.BackgroundServices;

/// <summary>
/// Runs the pg_dump CLI tool and returns the output as a gzip-compressed byte array.
/// Everything is kept in-memory — no disk write permissions are required.
/// </summary>
public class PgDumpBackupService
{
    private readonly BackupSettings _settings;
    private readonly ILogger<PgDumpBackupService> _logger;

    // Common PostgreSQL bin directories to probe when pg_dump isn't on PATH.
    private static readonly string[] FallbackDirs =
    [
        // Windows — PostgreSQL installer default locations
        @"C:\Program Files\PostgreSQL\17\bin",
        @"C:\Program Files\PostgreSQL\16\bin",
        @"C:\Program Files\PostgreSQL\15\bin",
        @"C:\Program Files\PostgreSQL\14\bin",
        // Linux / macOS — pg_dump is usually on PATH already, but just in case
        "/usr/lib/postgresql/17/bin",
        "/usr/lib/postgresql/16/bin",
        "/usr/lib/postgresql/15/bin",
        "/usr/bin",
        "/usr/local/bin",
    ];

    public PgDumpBackupService(IOptions<BackupSettings> settings, ILogger<PgDumpBackupService> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    /// <summary>
    /// Dumps the database described by <paramref name="connectionString"/> using pg_dump
    /// (plain-SQL format, gzip-compressed) and returns the bytes plus a timestamped filename.
    /// </summary>
    public async Task<(byte[] GzipBytes, string FileName)> DumpAsync(
        string connectionString, CancellationToken ct)
    {
        var csb      = new NpgsqlConnectionStringBuilder(connectionString);
        var host     = csb.Host     ?? "localhost";
        var port     = csb.Port > 0 ? csb.Port : 5432;
        var database = csb.Database ?? "fittrack";
        var username = csb.Username ?? "fittrack";
        var password = csb.Password ?? string.Empty;

        var pgDump   = ResolvePgDump();
        var tag      = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HH-mm");
        var fileName = $"fittrack-pgdump_{tag}.sql.gz";

        var psi = new ProcessStartInfo
        {
            FileName               = pgDump,
            Arguments              = $"-h \"{host}\" -p {port} -U \"{username}\" -d \"{database}\" --format=plain --no-owner --no-acl",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        // Pass password via environment variable — never visible in process listings.
        psi.Environment["PGPASSWORD"] = password;

        _logger.LogInformation(
            "Running pg_dump ({Exe}): database={Database} host={Host}:{Port}", pgDump, database, host, port);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start pg_dump process at '{pgDump}'.");

        using var ms = new MemoryStream();
        string stderr;

        // Gzip stdout on the fly; read stderr concurrently to avoid pipe deadlocks.
        await using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            var copyTask   = process.StandardOutput.BaseStream.CopyToAsync(gzip, ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(copyTask, stderrTask);
            stderr = stderrTask.Result;
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"pg_dump exited with code {process.ExitCode}: {stderr.Trim()}");

        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogWarning("pg_dump stderr: {Stderr}", stderr.Trim());

        _logger.LogInformation(
            "pg_dump complete → {FileName} ({Bytes:N0} bytes gzipped)", fileName, ms.Length);

        return (ms.ToArray(), fileName);
    }

    /// <summary>
    /// Returns the path to the pg_dump executable.
    /// Priority: explicit setting → auto-discover from fallback dirs → plain "pg_dump" (relies on PATH).
    /// </summary>
    private string ResolvePgDump()
    {
        // 1. Explicit override in appsettings / secrets
        if (!string.IsNullOrWhiteSpace(_settings.PgDumpPath) && File.Exists(_settings.PgDumpPath))
            return _settings.PgDumpPath;

        // 2. Scan known installation directories
        var exeName = OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump";
        foreach (var dir in FallbackDirs)
        {
            var candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate))
            {
                _logger.LogInformation("pg_dump auto-discovered at {Path}", candidate);
                return candidate;
            }
        }

        // 3. Fall through to PATH — will throw Win32Exception 2 if not found
        _logger.LogWarning(
            "pg_dump not found in known locations; falling back to PATH resolution. " +
            "Set Backup:PgDumpPath in appsettings/secrets to suppress this warning.");
        return "pg_dump";
    }
}
