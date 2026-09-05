namespace FitTrack.Application.Health;

/// <summary>
/// The fixed, immutable ten-week pelvic-floor plan. This is the single source of
/// truth for prescriptions, scheduled days, week numbers and adherence maths.
/// Every member is pure so it can be unit tested without a database.
/// </summary>
public static class PelvicFloorProgramDefinition
{
    public const int TotalWeeks = 10;

    /// <summary>Number of calendar days covered by the plan (day 0 .. day 69).</summary>
    public const int ProgramLengthDays = TotalWeeks * 7;

    /// <summary>Offset of the last day on which a session may be logged.</summary>
    public const int LastSessionDayOffset = ProgramLengthDays - 1;

    /// <summary>The program can be completed from this day offset onwards.</summary>
    public const int CompletionDayOffset = ProgramLengthDays;

    public const int BreathingBreaths = 5;

    public const int DeloadWeek = 8;

    private static readonly int[] ThreeSessionOffsets = [0, 2, 4];
    private static readonly int[] DeloadWeekOffsets = [0, 3];

    public static IReadOnlyList<PelvicFloorPhaseDto> Phases { get; } =
    [
        new("Learn control", 1, 2, 3, "Gentle identification, normal breathing, complete release."),
        new("Basic endurance", 3, 4, 3, "Controlled endurance and crisp quick contractions."),
        new("Capacity", 5, 7, 3, "Moderate effort, no straining, full release."),
        new("Deload and assess", 8, 8, 2, "Easy practice, recovery, reassessment."),
        new("Consolidate", 9, 10, 3, "Controlled work plus relaxation under normal breathing.")
    ];

    /// <summary>Short technique cues shown immediately before a guided session.</summary>
    public static IReadOnlyList<string> TechniqueCues { get; } =
    [
        "Use a gentle inward-and-upward pelvic-floor contraction; do not strain.",
        "Breathe normally. Keep the abdomen, buttocks, thighs, jaw, and shoulders relaxed.",
        "The release is part of every repetition; use the full rest interval.",
        "Do not practise by repeatedly stopping urine midstream."
    ];

    public const string SafetyNotice =
        "This tracker provides general exercise guidance, not medical diagnosis or treatment. " +
        "Use gentle, pain-free effort. Stop strengthening work and seek advice from a clinician or " +
        "pelvic-health physiotherapist if you develop pelvic, penile, testicular, rectal, lower-abdominal, " +
        "or low-back pain; painful urination or ejaculation; new urinary urgency; trouble urinating; or " +
        "persistent pelvic tightness. If erection changes are new or persistent, discuss them with a " +
        "healthcare professional.";

    public static PelvicFloorPrescriptionDto GetPrescription(int weekNumber) => weekNumber switch
    {
        1 or 2 => new(weekNumber, "Learn control", 3, 2, 6, 3, 6, 60, 0, 0, 0, 0, BreathingBreaths, false),
        3 or 4 => new(weekNumber, "Basic endurance", 3, 2, 8, 5, 7, 60, 1, 5, 1, 3, BreathingBreaths, false),
        5 or 6 or 7 => new(weekNumber, "Capacity", 3, 3, 8, 7, 9, 75, 1, 8, 1, 3, BreathingBreaths, false),
        8 => new(weekNumber, "Deload and assess", 2, 2, 6, 4, 8, 60, 0, 0, 0, 0, BreathingBreaths, true),
        9 or 10 => new(weekNumber, "Consolidate", 3, 3, 8, 7, 9, 75, 1, 8, 1, 3, BreathingBreaths, false),
        _ => throw new ArgumentOutOfRangeException(
            nameof(weekNumber), weekNumber, "Pelvic-floor week number must be between 1 and 10.")
    };

    /// <summary>Program day offsets within a training week that are scheduled target days.</summary>
    public static IReadOnlyList<int> GetScheduledOffsets(int weekNumber)
    {
        _ = GetPrescription(weekNumber);
        return weekNumber == DeloadWeek ? DeloadWeekOffsets : ThreeSessionOffsets;
    }

    /// <summary>
    /// Training weeks are seven consecutive calendar days beginning on <paramref name="startDate"/>.
    /// The result is clamped to 1..10.
    /// </summary>
    public static int GetWeekNumber(DateOnly startDate, DateOnly date)
    {
        var offset = date.DayNumber - startDate.DayNumber;
        if (offset < 0) return 1;
        return Math.Clamp(offset / 7 + 1, 1, TotalWeeks);
    }

    /// <summary>True when the date falls inside the 70-day plan window (day 0 .. day 69).</summary>
    public static bool IsWithinProgram(DateOnly startDate, DateOnly date)
    {
        var offset = date.DayNumber - startDate.DayNumber;
        return offset >= 0 && offset <= LastSessionDayOffset;
    }

    /// <summary>True when the date is one of the plan's scheduled target days.</summary>
    public static bool IsScheduledDate(DateOnly startDate, DateOnly date)
    {
        if (!IsWithinProgram(startDate, date)) return false;
        var offset = date.DayNumber - startDate.DayNumber;
        return GetScheduledOffsets(offset / 7 + 1).Contains(offset % 7);
    }

    /// <summary>All scheduled target days of the whole plan, in ascending order.</summary>
    public static IReadOnlyList<DateOnly> GetScheduledDates(DateOnly startDate)
    {
        var dates = new List<DateOnly>();
        for (var week = 1; week <= TotalWeeks; week++)
        {
            var weekStart = startDate.AddDays((week - 1) * 7);
            foreach (var offset in GetScheduledOffsets(week))
                dates.Add(weekStart.AddDays(offset));
        }

        return dates;
    }

    /// <summary>Number of scheduled target days from the start date through <paramref name="through"/>.</summary>
    public static int GetExpectedSessionCountThrough(DateOnly startDate, DateOnly through)
    {
        if (through < startDate) return 0;
        var last = startDate.AddDays(LastSessionDayOffset);
        if (through > last) through = last;
        return GetScheduledDates(startDate).Count(d => d <= through);
    }

    /// <summary>
    /// The next scheduled target day on or after <paramref name="from"/> that has no session logged yet,
    /// or null when the plan has no remaining scheduled day.
    /// </summary>
    public static DateOnly? GetNextScheduledDate(DateOnly startDate, DateOnly from, IReadOnlySet<DateOnly> loggedDates)
        => GetScheduledDates(startDate)
            .Where(d => d >= from && !loggedDates.Contains(d))
            .Select(d => (DateOnly?)d)
            .FirstOrDefault();
}
