using FitTrack.Domain.Common;

namespace FitTrack.Domain.Health;

public enum BodyPart
{
    Neck = 1,
    Shoulders = 2,
    Chest = 3,
    Bicep = 4,
    Forearm = 5,
    Waist = 6,
    Hips = 7,
    Thigh = 8,
    Calf = 9
}

public class BodyPartMeasurement : Entity
{
    public int UserId { get; set; }
    public DateOnly MeasuredOn { get; set; }
    public BodyPart BodyPart { get; set; }
    public decimal ValueCm { get; set; }
    public string? Notes { get; set; }
}
