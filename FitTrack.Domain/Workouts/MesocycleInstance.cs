using FitTrack.Domain.Common;

namespace FitTrack.Domain.Workouts;

public enum MesocycleInstanceStatus
{
    Active = 0,
    Completed = 1,
    Abandoned = 2
}

/// <summary>
/// A single run-through of a mesocycle template, starting on a date and
/// containing scheduled workout sessions with their target weights snapshotted
/// at start time (so progression doesn't rewrite history).
/// </summary>
public class MesocycleInstance : Entity
{
    public int MesocycleId { get; set; }
    public Mesocycle? Mesocycle { get; set; }

    /// <summary>Owner (user who started this instance).</summary>
    public int UserId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public MesocycleInstanceStatus Status { get; set; } = MesocycleInstanceStatus.Active;

    /// <summary>
    /// Weight multiplier applied to template target weights for this instance.
    /// Each new instance of the same mesocycle after a completed one bumps this by 5%.
    /// </summary>
    public decimal WeightMultiplier { get; set; } = 1.00m;

    public List<WorkoutSession> Sessions { get; set; } = new();
}

/// <summary>
/// One scheduled workout within a mesocycle instance, on a specific date.
/// </summary>
public class WorkoutSession : Entity
{
    public int MesocycleInstanceId { get; set; }
    public MesocycleInstance? MesocycleInstance { get; set; }

    public int MesocycleWorkoutId { get; set; }
    public MesocycleWorkout? MesocycleWorkout { get; set; }

    public DateOnly ScheduledDate { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Week number within the instance (1-based).</summary>
    public int WeekNumber { get; set; }

    /// <summary>
    /// True when this session has been deliberately skipped (e.g. closing a week early).
    /// Skipped sessions are excluded from "next pending" logic.
    /// </summary>
    public bool IsSkipped { get; set; }

    public string? Notes { get; set; }

    public List<ExerciseLog> Logs { get; set; } = new();
}

/// <summary>
/// A single logged set of one exercise performed during a workout session.
/// </summary>
public class ExerciseLog : Entity
{
    public int WorkoutSessionId { get; set; }
    public WorkoutSession? WorkoutSession { get; set; }

    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    public int PlannedExerciseId { get; set; }
    public PlannedExercise? PlannedExercise { get; set; }

    public int SetNumber { get; set; }
    public int TargetReps { get; set; }
    public decimal TargetWeightKg { get; set; }

    public int? ActualReps { get; set; }
    public decimal? ActualWeightKg { get; set; }

    public DateTimeOffset? PerformedAt { get; set; }
    public string? Notes { get; set; }
}
