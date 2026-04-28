using System.Globalization;
using System.IO.Compression;
using System.Text;
using Npgsql;

namespace FitTrack.Web.BackgroundServices;

/// <summary>
/// Exports every application table to a single .zip file containing per-table
/// .sql files with re-runnable INSERT statements, in FK-safe restore order.
/// </summary>
public class SqlBackupService
{
    // Tables in topological (FK-safe) order so a restore can replay them top to bottom.
    private static readonly (string Table, string File)[] Tables =
    [
        ("AppUsers",              "01_AppUsers.sql"),
        ("Exercises",             "02_Exercises.sql"),
        ("Foods",                 "03_Foods.sql"),
        ("Recipes",               "04_Recipes.sql"),
        ("Mesocycles",            "05_Mesocycles.sql"),
        ("MesocycleWorkouts",     "06_MesocycleWorkouts.sql"),
        ("PlannedExercises",      "07_PlannedExercises.sql"),
        ("MesocycleInstances",    "08_MesocycleInstances.sql"),
        ("WorkoutSessions",       "09_WorkoutSessions.sql"),
        ("ExerciseLogs",          "10_ExerciseLogs.sql"),
        ("RecipeIngredients",     "11_RecipeIngredients.sql"),
        ("BodyMeasurements",      "12_BodyMeasurements.sql"),
        ("BodyPartMeasurements",  "13_BodyPartMeasurements.sql"),
        ("BloodPressureReadings", "14_BloodPressureReadings.sql"),
        ("ColdEpisodes",          "15_ColdEpisodes.sql"),
        ("CardioSessions",        "16_CardioSessions.sql"),
        ("MealEntries",           "17_MealEntries.sql"),
    ];

    private readonly ILogger<SqlBackupService> _logger;

    public SqlBackupService(ILogger<SqlBackupService> logger) => _logger = logger;

    /// <summary>
    /// Connects to the database and writes a complete backup zip to <paramref name="zipFilePath"/>.
    /// Returns the total number of rows exported.
    /// </summary>
    public async Task<int> ExportToZipAsync(
        string connectionString, string zipFilePath, CancellationToken ct)
    {
        // Ensure the parent directory exists (the configured backup root).
        var dir = Path.GetDirectoryName(zipFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        int totalRows = 0;

        // Write everything into an in-memory zip, then flush to disk once.
        // This avoids partial/corrupt zips if the process is interrupted mid-write.
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // README entry
            var readme = zip.CreateEntry("README.txt", CompressionLevel.Optimal);
            await using (var rw = new StreamWriter(readme.Open(), Encoding.UTF8))
                await rw.WriteAsync(BuildReadme());

            foreach (var (table, file) in Tables)
            {
                try
                {
                    var entry = zip.CreateEntry(file, CompressionLevel.Optimal);
                    await using var stream = entry.Open();
                    int rows = await ExportTableAsync(conn, table, stream, ct);
                    _logger.LogDebug("Backup: {Table} → {Rows} rows", table, rows);
                    totalRows += rows;
                }
                catch (Exception ex)
                {
                    // Table may not exist yet in older schemas — log and continue.
                    _logger.LogWarning(ex, "Backup: skipped table {Table}", table);
                }
            }
        }

        // Atomic-ish write: save to a temp file then replace, so a crash never
        // leaves a corrupt zip where a good one existed.
        var tmp = zipFilePath + ".tmp";
        await File.WriteAllBytesAsync(tmp, ms.ToArray(), ct);
        File.Move(tmp, zipFilePath, overwrite: true);

        return totalRows;
    }

    private static async Task<int> ExportTableAsync(
        NpgsqlConnection conn, string table, Stream target, CancellationToken ct)
    {
        await using var writer = new StreamWriter(target, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync($"-- ============================================================");
        await writer.WriteLineAsync($"-- Table: {table}");
        await writer.WriteLineAsync($"-- Generated: {DateTimeOffset.UtcNow:O}");
        await writer.WriteLineAsync($"-- ============================================================");
        await writer.WriteLineAsync($"-- Uncomment TRUNCATE for a clean-slate restore (CASCADE clears children).");
        await writer.WriteLineAsync($"-- TRUNCATE TABLE \"{table}\" CASCADE;");
        await writer.WriteLineAsync();

        int rowCount = 0;

        await using var cmd    = new NpgsqlCommand($"SELECT * FROM \"{table}\" ORDER BY \"Id\"", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var columns    = Enumerable.Range(0, reader.FieldCount).Select(i => $"\"{reader.GetName(i)}\"").ToArray();
        var columnList = string.Join(", ", columns);

        while (await reader.ReadAsync(ct))
        {
            var values = Enumerable.Range(0, reader.FieldCount)
                .Select(i => FormatValue(reader.GetValue(i)))
                .ToArray();

            await writer.WriteLineAsync(
                $"INSERT INTO \"{table}\" ({columnList}) VALUES ({string.Join(", ", values)}) " +
                $"ON CONFLICT (\"Id\") DO NOTHING;");
            rowCount++;
        }

        if (rowCount > 0)
        {
            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"-- Reset PK sequence after restore:");
            await writer.WriteLineAsync(
                $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), " +
                $"COALESCE((SELECT MAX(\"Id\") FROM \"{table}\"), 0) + 1, false);");
        }

        return rowCount;
    }

    private static string FormatValue(object value)
    {
        if (value is DBNull or null) return "NULL";

        return value switch
        {
            bool b             => b ? "TRUE" : "FALSE",
            int i              => i.ToString(CultureInfo.InvariantCulture),
            long l             => l.ToString(CultureInfo.InvariantCulture),
            short s            => s.ToString(CultureInfo.InvariantCulture),
            decimal d          => d.ToString(CultureInfo.InvariantCulture),
            float f            => f.ToString(CultureInfo.InvariantCulture),
            double db          => db.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset dto => $"'{dto:O}'",
            DateTime dt        => $"'{dt:O}'",
            DateOnly date      => $"'{date:yyyy-MM-dd}'",
            TimeOnly time      => $"'{time:HH:mm:ss}'",
            Guid g             => $"'{g}'",
            byte[] bytes       => $"'\\x{Convert.ToHexString(bytes)}'",
            string str         => $"'{str.Replace("'", "''")}'",
            _                  => $"'{value.ToString()!.Replace("'", "''")}'",
        };
    }

    private static string BuildReadme() =>
        """
        FitTrack SQL Backup
        ===================

        Files are numbered in FK-safe restore order. To restore:

          1. Unzip this archive.
          2. Connect to your PostgreSQL database as the fittrack user.
          3. (Optional) Run EF migrations first to ensure the schema is up to date.
          4. Run each .sql file in numerical order:
               psql -U fittrack -d fittrack -f 01_AppUsers.sql
               psql -U fittrack -d fittrack -f 02_Exercises.sql
               ... and so on up to 17_MealEntries.sql

        INSERT statements use ON CONFLICT (Id) DO NOTHING — safe to re-run against
        a populated database (existing rows are skipped).

        For a clean restore, uncomment the TRUNCATE line at the top of each file.
        Truncate from highest number downwards first, then INSERT from lowest upwards.

        Backup directory is configured via appsettings.json:
          "Backup": { "BackupDirectory": "/your/path/here" }
        Or via environment variable:
          Backup__BackupDirectory=/your/path/here
        """;
}
