using FitTrack.Domain.Workouts;

namespace FitTrack.Application.Workouts;

public record ExerciseDto(int Id, string Name, string? Description, MuscleGroup PrimaryMuscleGroup, decimal WeeklyIncrementKg);
public record CreateExerciseDto(string Name, string? Description, MuscleGroup PrimaryMuscleGroup, decimal WeeklyIncrementKg = 0.25m);

public record PlannedExerciseDto(
    int Id,
    int ExerciseId,
    string ExerciseName,
    int TargetSets,
    int TargetReps,
    decimal TargetWeightKg,
    int OrderIndex);

public record CreatePlannedExerciseDto(
    int ExerciseId,
    int TargetSets,
    int TargetReps,
    decimal TargetWeightKg,
    int OrderIndex);

public record MesocycleWorkoutDto(
    int Id,
    string Name,
    int DayOrder,
    List<PlannedExerciseDto> PlannedExercises);

public record CreateMesocycleWorkoutDto(
    string Name,
    int DayOrder,
    List<CreatePlannedExerciseDto> PlannedExercises);

public record MesocycleDto(
    int Id,
    string Name,
    string? Description,
    int DurationWeeks,
    bool HasRampUpWeek,
    List<MesocycleWorkoutDto> Workouts);

public record CreateMesocycleDto(
    string Name,
    string? Description,
    int DurationWeeks,
    bool HasRampUpWeek,
    List<CreateMesocycleWorkoutDto> Workouts);

public record MesocycleInstanceDto(
    int Id,
    int MesocycleId,
    string MesocycleName,
    DateOnly StartDate,
    DateOnly? EndDate,
    MesocycleInstanceStatus Status,
    decimal WeightMultiplier);

public record WorkoutSessionDto(
    int Id,
    int MesocycleInstanceId,
    int MesocycleWorkoutId,
    string WorkoutName,
    DateOnly ScheduledDate,
    int WeekNumber,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Notes,
    List<ExerciseLogDto> Logs);

public record ExerciseLogDto(
    int Id,
    int ExerciseId,
    string ExerciseName,
    int SetNumber,
    int TargetReps,
    decimal TargetWeightKg,
    int? ActualReps,
    decimal? ActualWeightKg,
    DateTimeOffset? PerformedAt,
    string? Notes);

public record UpdateExerciseLogDto(int? ActualReps, decimal? ActualWeightKg, string? Notes);
