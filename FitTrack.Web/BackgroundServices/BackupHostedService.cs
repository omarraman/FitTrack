using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace FitTrack.Web.BackgroundServices;

/// <summary>
/// Wakes up every <see cref="BackupSettings.CheckIntervalMinutes"/> minutes and, when the
/// backup interval has elapsed, runs:
///   1. pg_dump  — entirely in-memory, attached to an email (no disk permissions needed).
///   2. SQL-INSERT zip — written to the configured backup directory on disk (best-effort).
/// </summary>
public class BackupHostedService : BackgroundService
{
    private readonly BackupSettings _settings;
    private readonly SqlBackupService _sqlBackupSvc;
    private readonly PgDumpBackupService _pgDumpSvc;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly ILogger<BackupHostedService> _logger;

    private const string LastBackupFile = "last_backup.txt";

    public BackupHostedService(
        IOptions<BackupSettings> settings,
        SqlBackupService sqlBackupSvc,
        PgDumpBackupService pgDumpSvc,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<BackupHostedService> logger)
    {
        _settings     = settings.Value;
        _sqlBackupSvc = sqlBackupSvc;
        _pgDumpSvc    = pgDumpSvc;
        _config       = config;
        _env          = env;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BackupHostedService started. Check interval: {Interval} min, backup interval: {Hours} h.",
            _settings.CheckIntervalMinutes, _settings.BackupIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndBackupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Backup check/run failed.");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.CheckIntervalMinutes),
                stoppingToken);
        }
    }

    private async Task CheckAndBackupAsync(CancellationToken ct)
    {
        // Stamp file: use temp dir when email is configured (no disk write needed),
        // otherwise use the backup root directory.
        var root      = ResolveBackupRoot();
        bool emailConfigured = _settings.Email is { } ec && !string.IsNullOrWhiteSpace(ec.From);
        var stampDir  = emailConfigured ? Path.GetTempPath()
                        : (TryEnsureDirectory(root) ? root : Path.GetTempPath());
        var stampFile = Path.Combine(stampDir, LastBackupFile);

        var lastBackup = ReadLastBackupTime(stampFile);
        var threshold  = TimeSpan.FromHours(_settings.BackupIntervalHours);

        if (DateTimeOffset.UtcNow - lastBackup < threshold)
            return; // Not yet due.

        var connectionString = _config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        var tag = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HH-mm");

        // ── 1. pg_dump (fully in-memory — no disk write permissions required) ─────
        byte[] pgDumpBytes = [];
        string pgDumpFile  = string.Empty;
        try
        {
            (pgDumpBytes, pgDumpFile) = await _pgDumpSvc.DumpAsync(connectionString, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "pg_dump failed.");
        }

        // ── 2. SQL-INSERT zip (disk — only when email is not configured) ────────
        int sqlRows = 0;
        string? sqlZipPath = null;

        if (!emailConfigured)
        {
            var zipPath = Path.Combine(root, $"fittrack-backup_{tag}.zip");
            try
            {
                _logger.LogInformation("Starting SQL backup → {Path}", zipPath);
                sqlRows = await _sqlBackupSvc.ExportToZipAsync(connectionString, zipPath, ct);
                _logger.LogInformation("SQL backup complete: {Rows} rows → {Path}", sqlRows, zipPath);
                sqlZipPath = zipPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SQL backup to disk failed (disk may not be writable).");
            }
        }

        // ── 3. Email ──────────────────────────────────────────────────────────────
        if (emailConfigured)
        {
            if (pgDumpBytes.Length > 0)
            {
                try
                {
                    await SendBackupEmailAsync(_settings.Email!, pgDumpBytes, pgDumpFile, null, 0, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Backup email could not be sent.");
                }
            }
            else
            {
                _logger.LogWarning("Email is configured but pg_dump produced no output — no email sent.");
            }
        }

        // ── 4. Stamp + purge ──────────────────────────────────────────────────────
        try { await File.WriteAllTextAsync(stampFile, DateTimeOffset.UtcNow.ToString("O"), ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not write stamp file {StampFile}.", stampFile); }

        if (_settings.RetainDays > 0 && TryEnsureDirectory(root))
            PurgeOldBackups(root, _settings.RetainDays);
    }

    private string ResolveBackupRoot()
    {
        var dir = _settings.BackupDirectory;
        return Path.IsPathRooted(dir) ? dir : Path.Combine(_env.ContentRootPath, dir);
    }

    private static bool TryEnsureDirectory(string path)
    {
        try { Directory.CreateDirectory(path); return true; }
        catch { return false; }
    }

    private static DateTimeOffset ReadLastBackupTime(string stampFile)
    {
        if (!File.Exists(stampFile)) return DateTimeOffset.MinValue;
        var text = File.ReadAllText(stampFile).Trim();
        return DateTimeOffset.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt : DateTimeOffset.MinValue;
    }

    private void PurgeOldBackups(string root, int retainDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retainDays);
        foreach (var file in Directory.EnumerateFiles(root, "fittrack-backup_*.zip"))
        {
            try
            {
                if (File.GetCreationTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    _logger.LogInformation("Purged old backup: {File}", file);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not purge: {File}", file); }
        }
    }

    private async Task SendBackupEmailAsync(
        BackupEmailSettings cfg,
        byte[] pgDumpBytes, string pgDumpFile,
        string? sqlZipPath, int sqlRows,
        CancellationToken ct)
    {
        var to   = string.IsNullOrWhiteSpace(cfg.To) ? cfg.From : cfg.To;
        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        using var msg = new MailMessage();
        msg.From = new MailAddress(cfg.From, "FitTrack Backup");
        msg.To.Add(to);
        msg.Subject = $"FitTrack Backup {date}";

        var body = new StringBuilder();
        body.AppendLine("Your daily FitTrack backup is attached.");
        body.AppendLine();
        body.AppendLine($"  Date: {date}");

        if (pgDumpBytes.Length > 0)
        {
            body.AppendLine($"  pg_dump:  {pgDumpFile}  ({pgDumpBytes.Length / 1024.0:F1} KB gzipped)");
            body.AppendLine();
            body.AppendLine("To restore from the pg_dump attachment:");
            body.AppendLine("  gunzip -c fittrack-pgdump_<date>.sql.gz | psql -U fittrack -d fittrack");

            // Attach directly from memory — no disk write needed.
            msg.Attachments.Add(new Attachment(new MemoryStream(pgDumpBytes), pgDumpFile, "application/gzip"));
        }

        if (sqlZipPath is not null)
        {
            body.AppendLine();
            body.AppendLine($"  SQL zip:  {Path.GetFileName(sqlZipPath)}  ({sqlRows:N0} rows)");
            body.AppendLine("  (Restore by replaying the numbered .sql files inside the zip in order.)");
            msg.Attachments.Add(new Attachment(sqlZipPath));
        }

        msg.Body = body.ToString();

        using var smtp = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort);
        smtp.EnableSsl    = true;
        smtp.Credentials  = new NetworkCredential(cfg.From, cfg.AppPassword);

        await smtp.SendMailAsync(msg, ct);
        _logger.LogInformation("Backup emailed to {To} — pg_dump: {PgFile}, sql: {SqlFile}",
            to, pgDumpFile, sqlZipPath is null ? "n/a" : Path.GetFileName(sqlZipPath));
    }
}
