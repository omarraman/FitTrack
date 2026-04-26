using FitTrack.Domain.Common;

namespace FitTrack.Domain.Health;

/// <summary>
/// A single cardio session on the indoor bike.
/// </summary>
public class CardioSession : Entity
{
    public int UserId { get; set; }
    public DateOnly SessionDate { get; set; }

    /// <summary>Total session duration in minutes.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>Total calories burned.</summary>
    public int Calories { get; set; }

    /// <summary>Average output in watts.</summary>
    public int Watts { get; set; }

    /// <summary>Maximum speed reached during the session (km/h).</summary>
    public decimal MaxSpeedKph { get; set; }

    /// <summary>Maximum heart rate (bpm). Nullable — only set when a tracker is worn.</summary>
    public int? MaxHeartRate { get; set; }

    public string? Notes { get; set; }
}
