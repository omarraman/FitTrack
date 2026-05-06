namespace FitTrack.Web.BackgroundServices;

public class BackupSettings
{
    public const string Section = "Backup";

    /// <summary>How often the service wakes up to check whether a backup is due. Default: 1 minute.</summary>
    public int CheckIntervalMinutes { get; set; } = 1;

    /// <summary>Minimum gap between backups. Default: 24 hours.</summary>
    public double BackupIntervalHours { get; set; } = 24;

    /// <summary>Root folder for backup sets. Relative paths resolve against the app's content root.</summary>
    public string BackupDirectory { get; set; } = "backups";

    /// <summary>Automatically delete backup sets older than this many days. 0 = keep forever.</summary>
    public int RetainDays { get; set; } = 30;

    /// <summary>
    /// Full path to the pg_dump executable. Leave empty to auto-discover from PATH
    /// or common PostgreSQL installation directories.
    /// Example: "C:\\Program Files\\PostgreSQL\\16\\bin\\pg_dump.exe"
    /// </summary>
    public string PgDumpPath { get; set; } = "";

    /// <summary>Optional email settings. If null or From is empty, no email is sent.</summary>
    public BackupEmailSettings? Email { get; set; }
}

public class BackupEmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;

    /// <summary>The Gmail address used to send (and receive) the backup.</summary>
    public string From { get; set; } = "";

    /// <summary>Gmail App Password (16 chars, no spaces required). NOT your account password.</summary>
    public string AppPassword { get; set; } = "";

    /// <summary>Destination address. Defaults to From if left empty.</summary>
    public string To { get; set; } = "";
}
