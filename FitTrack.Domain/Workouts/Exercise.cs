using FitTrack.Domain.Common;

namespace FitTrack.Domain.Workouts;

/// <summary>
/// A movement pattern that can be performed in a workout (e.g. Bench Press, Squat).
/// </summary>
public class Exercise : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MuscleGroup PrimaryMuscleGroup { get; set; } = MuscleGroup.Other;

    /// <summary>
    /// How much weight (kg) is added per completed week when this exercise
    /// is part of a mesocycle instance. Defaults to the smallest increment (0.25 kg).
    /// Valid values: 0.25, 0.50, 0.75, 1.00, 1.25, 1.50.
    /// </summary>
    public decimal WeeklyIncrementKg { get; set; } = 0.25m;
}

public enum MuscleGroup
{
    Other = 0,
    Chest = 1,
    Back = 2,
    Shoulders = 3,
    Biceps = 4,
    Triceps = 5,
    Legs = 6,
    Glutes = 7,
    Core = 8,
    Cardio = 9,
    FullBody = 10
}
