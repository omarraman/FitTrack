using FitTrack.Domain.Health;
using FitTrack.Domain.Nutrition;
using FitTrack.Domain.Users;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Abstractions;

/// <summary>
/// Persistence contract used by application services. The Infrastructure
/// layer provides an EF Core implementation; application code stays
/// decoupled from EF-specific types (mostly).
/// </summary>
public interface IAppDbContext
{
    DbSet<AppUser> AppUsers { get; }
    DbSet<Exercise> Exercises { get; }
    DbSet<Mesocycle> Mesocycles { get; }
    DbSet<MesocycleWorkout> MesocycleWorkouts { get; }
    DbSet<PlannedExercise> PlannedExercises { get; }
    DbSet<MesocycleInstance> MesocycleInstances { get; }
    DbSet<WorkoutSession> WorkoutSessions { get; }
    DbSet<ExerciseLog> ExerciseLogs { get; }

    DbSet<BodyMeasurement> BodyMeasurements { get; }
    DbSet<BodyPartMeasurement> BodyPartMeasurements { get; }
    DbSet<BloodPressureReading> BloodPressureReadings { get; }
    DbSet<ColdEpisode> ColdEpisodes { get; }
    DbSet<CardioSession> CardioSessions { get; }

    DbSet<Food> Foods { get; }
    DbSet<Recipe> Recipes { get; }
    DbSet<RecipeIngredient> RecipeIngredients { get; }
    DbSet<MealEntry> MealEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}