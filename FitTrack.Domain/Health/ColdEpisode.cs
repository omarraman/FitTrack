using FitTrack.Domain.Common;

namespace FitTrack.Domain.Health;

public enum ColdSeverity
{
    VeryMild = 1,
    Mild = 2,
    Moderate = 3,
    Severe = 4,
    VerySevere = 5
}

public class ColdEpisode : Entity
{
    public int UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ColdSeverity Severity { get; set; } = ColdSeverity.Mild;
    public string? Symptoms { get; set; }
    public string? Notes { get; set; }
}
