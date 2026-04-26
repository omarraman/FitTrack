using FitTrack.Domain.Nutrition;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        // Exercises
        if (!await db.Exercises.AnyAsync(ct))
        {
            db.Exercises.AddRange(
                new Exercise { Name = "Bench Press", PrimaryMuscleGroup = MuscleGroup.Chest },
                new Exercise { Name = "Squat", PrimaryMuscleGroup = MuscleGroup.Legs },
                new Exercise { Name = "Deadlift", PrimaryMuscleGroup = MuscleGroup.Back },
                new Exercise { Name = "Overhead Press", PrimaryMuscleGroup = MuscleGroup.Shoulders },
                new Exercise { Name = "Barbell Row", PrimaryMuscleGroup = MuscleGroup.Back },
                new Exercise { Name = "Pull-Up", PrimaryMuscleGroup = MuscleGroup.Back },
                new Exercise { Name = "Dip", PrimaryMuscleGroup = MuscleGroup.Triceps }
            );
            await db.SaveChangesAsync(ct);
        }

        // Sample mesocycle
        if (!await db.Mesocycles.AnyAsync(ct))
        {
            var bench = await db.Exercises.FirstAsync(e => e.Name == "Bench Press", ct);
            var squat = await db.Exercises.FirstAsync(e => e.Name == "Squat", ct);
            var row   = await db.Exercises.FirstAsync(e => e.Name == "Barbell Row", ct);
            var ohp   = await db.Exercises.FirstAsync(e => e.Name == "Overhead Press", ct);
            var dead  = await db.Exercises.FirstAsync(e => e.Name == "Deadlift", ct);

            db.Mesocycles.Add(new Mesocycle
            {
                Name = "Upper / Lower 4-Week",
                Description = "Simple 4-week upper/lower split, two days per week.",
                DurationWeeks = 4,
                Workouts = new List<MesocycleWorkout>
                {
                    new()
                    {
                        Name = "Upper",
                        DayOrder = 1,
                        PlannedExercises = new List<PlannedExercise>
                        {
                            new() { ExerciseId = bench.Id, TargetSets = 4, TargetReps = 6, TargetWeightKg = 70m, OrderIndex = 1 },
                            new() { ExerciseId = row.Id,   TargetSets = 4, TargetReps = 8, TargetWeightKg = 60m, OrderIndex = 2 },
                            new() { ExerciseId = ohp.Id,   TargetSets = 3, TargetReps = 8, TargetWeightKg = 40m, OrderIndex = 3 },
                        }
                    },
                    new()
                    {
                        Name = "Lower",
                        DayOrder = 2,
                        PlannedExercises = new List<PlannedExercise>
                        {
                            new() { ExerciseId = squat.Id, TargetSets = 4, TargetReps = 6, TargetWeightKg = 90m, OrderIndex = 1 },
                            new() { ExerciseId = dead.Id,  TargetSets = 3, TargetReps = 5, TargetWeightKg = 110m, OrderIndex = 2 },
                        }
                    }
                }
            });
            await db.SaveChangesAsync(ct);
        }

        // A few foods
        if (!await db.Foods.AnyAsync(ct))
        {
            db.Foods.AddRange(
                new Food { Name = "Chicken Breast (raw)",  CaloriesPer100g = 165m, ProteinPer100g = 31m, CarbsPer100g = 0m,   FatPer100g = 3.6m },
                new Food { Name = "White Rice (cooked)",   CaloriesPer100g = 130m, ProteinPer100g = 2.4m,CarbsPer100g = 28m,  FatPer100g = 0.3m },
                new Food { Name = "Olive Oil",             CaloriesPer100g = 884m, ProteinPer100g = 0m,  CarbsPer100g = 0m,   FatPer100g = 100m },
                new Food { Name = "Kidney Beans (canned)", CaloriesPer100g = 127m, ProteinPer100g = 8.7m,CarbsPer100g = 22.8m,FatPer100g = 0.5m },
                new Food { Name = "Canned Tomatoes",       CaloriesPer100g = 32m,  ProteinPer100g = 1.6m,CarbsPer100g = 7.2m, FatPer100g = 0.3m },
                new Food { Name = "Minced Beef 10% fat",   CaloriesPer100g = 217m, ProteinPer100g = 19.4m,CarbsPer100g = 0m,  FatPer100g = 15.4m }
            );
            await db.SaveChangesAsync(ct);
        }
    }
}
