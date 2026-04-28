namespace FitTrack.Web.BackgroundServices;

public class BackupSettings
{
    public const string Section = "Backup";

    /// <summary>How often the service wakes up to check whether a backup is due. Default: 1 minute.</summary>
    public int CheckIntervalMinutes { get; set; } = 1;

    /// <summary>Minimum gap between backups. Default: 24 hours.</summary>
    public int BackupIntervalHours { get; set; } = 24;

    /// <summary>Root folder for backup sets. Relative paths resolve against the app's content root.</summary>
    public string BackupDirectory { get; set; } = "backups";

    /// <summary>Automatically delete backup sets older than this many days. 0 = keep forever.</summary>
    public int RetainDays { get; set; } = 30;
}

