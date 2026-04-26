using FitTrack.Domain.Common;

namespace FitTrack.Domain.Nutrition;

/// <summary>
/// A base food whose macros are defined per 100 grams.
/// </summary>
public class Food : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }

    // Per 100g
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal CarbsPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public decimal? FiberPer100g { get; set; }
}
