using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace FitTrack.Web.BackgroundServices;

/// <summary>
/// Long-running hosted service that wakes up every <see cref="BackupSettings.CheckIntervalMinutes"/>
/// minutes, checks whether more than <see cref="BackupSettings.BackupIntervalHours"/> hours have
/// elapsed since the last backup, and if so triggers a full SQL export.
///
/// The check itself is negligible (a file-stat + DateTime comparison) so a 1-minute
/// poll interval is completely fine — the actual export only runs once per 24 hours.
/// </summary>
public class BackupHostedService : BackgroundService
{
    private readonly BackupSettings _settings;
    private readonly SqlBackupService _backupSvc;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly ILogger<BackupHostedService> _logger;

    private const string LastBackupFile = "last_backup.txt";

    public BackupHostedService(
        IOptions<BackupSettings> settings,
        SqlBackupService backupSvc,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<BackupHostedService> logger)
    {
        _settings  = settings.Value;
        _backupSvc = backupSvc;
        _config    = config;
        _env       = env;
        _logger    = logger;
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
        var root = ResolveBackupRoot();
        Directory.CreateDirectory(root);

        var stampFile = Path.Combine(root, LastBackupFile);
        var lastBackup = ReadLastBackupTime(stampFile);
        var threshold  = TimeSpan.FromHours(_settings.BackupIntervalHours);

        if (DateTimeOffset.UtcNow - lastBackup < threshold)
            return; // Not yet due.

        var connectionString = _config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        var tag = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HH-mm");
        var zipPath = Path.Combine(root, $"fittrack-backup_{tag}.zip");

        _logger.LogInformation("Starting SQL backup → {Path}", zipPath);
        var rows = await _backupSvc.ExportToZipAsync(connectionString, zipPath, ct);
        _logger.LogInformation("SQL backup complete. {Rows} total rows → {Path}.", rows, zipPath);

        // Send email if configured.
        if (_settings.Email is { } emailCfg && !string.IsNullOrWhiteSpace(emailCfg.From))
        {
            try
            {
                await SendBackupEmailAsync(emailCfg, zipPath, rows, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backup email could not be sent.");
            }
        }

        // Record completion time.
        await File.WriteAllTextAsync(stampFile, DateTimeOffset.UtcNow.ToString("O"), ct);

        // Purge old backup sets if retention is configured.
        if (_settings.RetainDays > 0)
            PurgeOldBackups(root, _settings.RetainDays);
    }

    private string ResolveBackupRoot()
    {
        var dir = _settings.BackupDirectory;
        if (Path.IsPathRooted(dir)) return dir;
        return Path.Combine(_env.ContentRootPath, dir);
    }

    private static DateTimeOffset ReadLastBackupTime(string stampFile)
    {
        if (!File.Exists(stampFile)) return DateTimeOffset.MinValue;
        var text = File.ReadAllText(stampFile).Trim();
        return DateTimeOffset.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : DateTimeOffset.MinValue;
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not purge old backup: {File}", file);
            }
        }
    }

    private async Task SendBackupEmailAsync(
        BackupEmailSettings cfg, string zipPath, int totalRows, CancellationToken ct)
    {
        var to = string.IsNullOrWhiteSpace(cfg.To) ? cfg.From : cfg.To;
        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var fileName = Path.GetFileName(zipPath);

        using var msg = new MailMessage();
        msg.From = new MailAddress(cfg.From, "FitTrack Backup");
        msg.To.Add(to);
        msg.Subject = $"FitTrack Backup {date}";
        msg.Body =
            $"Your daily FitTrack backup is attached.\n\n" +
            $"  Date:       {date}\n" +
            $"  File:       {fileName}\n" +
            $"  Total rows: {totalRows:N0}\n\n" +
            $"Use the numbered .sql files inside the zip to restore, in order.";
        msg.Attachments.Add(new Attachment(zipPath));

        using var smtp = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort);
        smtp.EnableSsl = true;
        smtp.Credentials = new NetworkCredential(cfg.From, cfg.AppPassword);

        await smtp.SendMailAsync(msg, ct);
        _logger.LogInformation("Backup emailed to {To} ({File})", to, fileName);
    }
}

