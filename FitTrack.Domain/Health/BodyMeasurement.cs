using FitTrack.Domain.Common;

namespace FitTrack.Domain.Health;

public class BodyMeasurement : Entity
{
    public int UserId { get; set; }
    public DateOnly MeasuredOn { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? BodyFatPercent { get; set; }
    public decimal? MusclePercent { get; set; }
    public decimal? MuscleKg { get; set; }
    public string? Notes { get; set; }
}

public enum BpSessionType
{
    Morning = 0,
    Evening = 1
}

public class BloodPressureReading : Entity
{
    public int UserId { get; set; }
    public DateTimeOffset MeasuredAt { get; set; }
    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public int? Pulse { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Morning or Evening session. Null for readings recorded before the
    /// session-based workflow was introduced.
    /// </summary>
    public BpSessionType? SessionType { get; set; }
}
