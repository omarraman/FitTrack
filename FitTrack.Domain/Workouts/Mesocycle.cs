using FitTrack.Domain.Common;

namespace FitTrack.Domain.Workouts;

/// <summary>
/// A mesocycle template: a set of workout days, each with target exercises/reps/weight,
/// designed to be run for DurationWeeks weeks.
/// </summary>
public class Mesocycle : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationWeeks { get; set; } = 4;

    /// <summary>
    /// When true, week 1 is a ramp-up week: all exercises do exactly 1 set
    /// regardless of TargetSets. Full sets resume from week 2.
    /// </summary>
    public bool HasRampUpWeek { get; set; } = true;

    public List<MesocycleWorkout> Workouts { get; set; } = new();
    public List<MesocycleInstance> Instances { get; set; } = new();
}

/// <summary>
/// A single workout day within a mesocycle template (e.g. "Day A - Push").
/// </summary>
public class MesocycleWorkout : Entity
{
    public int MesocycleId { get; set; }
    public Mesocycle? Mesocycle { get; set; }

    public string Name { get; set; } = string.Empty;
    /// <summary>Order within the week rotation, 1-based.</summary>
    public int DayOrder { get; set; }

    public List<PlannedExercise> PlannedExercises { get; set; } = new();
}

/// <summary>
/// A target exercise within a workout template: X sets of Y reps at Z kg.
/// </summary>
public class PlannedExercise : Entity
{
    public int MesocycleWorkoutId { get; set; }
    public MesocycleWorkout? MesocycleWorkout { get; set; }

    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    public int TargetSets { get; set; } = 3;
    public int TargetReps { get; set; } = 8;
    public decimal TargetWeightKg { get; set; }

    /// <summary>Order within the workout.</summary>
    public int OrderIndex { get; set; }
}
