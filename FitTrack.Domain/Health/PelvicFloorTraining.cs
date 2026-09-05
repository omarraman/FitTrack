using FitTrack.Domain.Common;

namespace FitTrack.Domain.Health;

public enum PelvicFloorProgramStatus
{
    Active = 1,
    Completed = 2,
    Cancelled = 3
}

public enum PelvicFloorSessionOutcome
{
    Completed = 1,
    StoppedEarly = 2,
    SkippedForSymptoms = 3
}

public class PelvicFloorProgram : Entity
{
    public int UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public PelvicFloorProgramStatus Status { get; set; } = PelvicFloorProgramStatus.Active;
    public DateOnly? CompletedOn { get; set; }
    public DateOnly? CancelledOn { get; set; }
    public string? Notes { get; set; }

    public List<PelvicFloorSessionLog> Sessions { get; set; } = new();
    public List<PelvicFloorDailyCheckIn> CheckIns { get; set; } = new();
}

public class PelvicFloorSessionLog : Entity
{
    public int PelvicFloorProgramId { get; set; }
    public PelvicFloorProgram? PelvicFloorProgram { get; set; }
    public DateOnly SessionDate { get; set; }
    public int WeekNumber { get; set; }
    public bool IsOptional { get; set; }
    public PelvicFloorSessionOutcome Outcome { get; set; }
    public int CompletedSustainedSets { get; set; }
    public int CompletedSustainedRepsPerSet { get; set; }
    public int CompletedHoldSeconds { get; set; }
    public int CompletedRestSeconds { get; set; }
    public int CompletedQuickReps { get; set; }
    public bool CompletedBreathingFinish { get; set; }
    public int? EffortRating { get; set; }
    public int? RelaxationRating { get; set; }
    public string? Notes { get; set; }
}

public class PelvicFloorDailyCheckIn : Entity
{
    public int PelvicFloorProgramId { get; set; }
    public PelvicFloorProgram? PelvicFloorProgram { get; set; }
    public DateOnly CheckInDate { get; set; }
    public int? PelvicTensionRating { get; set; }
    public bool HasPainOrUrinarySymptoms { get; set; }
    public string? Notes { get; set; }
}
