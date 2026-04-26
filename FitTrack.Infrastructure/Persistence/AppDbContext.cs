using FitTrack.Application.Abstractions;
using FitTrack.Domain.Common;
using FitTrack.Domain.Health;
using FitTrack.Domain.Nutrition;
using FitTrack.Domain.Users;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<Mesocycle> Mesocycles => Set<Mesocycle>();
    public DbSet<MesocycleWorkout> MesocycleWorkouts => Set<MesocycleWorkout>();
    public DbSet<PlannedExercise> PlannedExercises => Set<PlannedExercise>();
    public DbSet<MesocycleInstance> MesocycleInstances => Set<MesocycleInstance>();
    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();
    public DbSet<ExerciseLog> ExerciseLogs => Set<ExerciseLog>();

    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();
    public DbSet<BodyPartMeasurement> BodyPartMeasurements => Set<BodyPartMeasurement>();
    public DbSet<BloodPressureReading> BloodPressureReadings => Set<BloodPressureReading>();
    public DbSet<ColdEpisode> ColdEpisodes => Set<ColdEpisode>();

    public DbSet<CardioSession> CardioSessions => Set<CardioSession>();

    public DbSet<Food> Foods => Set<Food>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // --- Users ---
        mb.Entity<AppUser>(e =>
        {
            e.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.HasIndex(x => x.ExternalId).IsUnique();
        });

        // --- Workouts ---
        mb.Entity<Exercise>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        mb.Entity<Mesocycle>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Workouts)
                .WithOne(x => x.Mesocycle!)
                .HasForeignKey(x => x.MesocycleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Instances)
                .WithOne(x => x.Mesocycle!)
                .HasForeignKey(x => x.MesocycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MesocycleWorkout>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.PlannedExercises)
                .WithOne(x => x.MesocycleWorkout!)
                .HasForeignKey(x => x.MesocycleWorkoutId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PlannedExercise>(e =>
        {
            e.Property(x => x.TargetWeightKg).HasPrecision(8, 2);
            e.HasOne(x => x.Exercise)
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MesocycleInstance>(e =>
        {
            e.Property(x => x.WeightMultiplier).HasPrecision(6, 4);
            e.HasMany(x => x.Sessions)
                .WithOne(x => x.MesocycleInstance!)
                .HasForeignKey(x => x.MesocycleInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        mb.Entity<WorkoutSession>(e =>
        {
            e.HasOne(x => x.MesocycleWorkout)
                .WithMany()
                .HasForeignKey(x => x.MesocycleWorkoutId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Logs)
                .WithOne(x => x.WorkoutSession!)
                .HasForeignKey(x => x.WorkoutSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.MesocycleInstanceId, x.ScheduledDate });
        });

        mb.Entity<ExerciseLog>(e =>
        {
            e.Property(x => x.TargetWeightKg).HasPrecision(8, 2);
            e.Property(x => x.ActualWeightKg).HasPrecision(8, 2);
            e.HasOne(x => x.Exercise)
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PlannedExercise)
                .WithMany()
                .HasForeignKey(x => x.PlannedExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- Health ---
        mb.Entity<BodyMeasurement>(e =>
        {
            e.Property(x => x.WeightKg).HasPrecision(6, 2);
            e.Property(x => x.BodyFatPercent).HasPrecision(5, 2);
            e.Property(x => x.MusclePercent).HasPrecision(5, 2);
            e.Property(x => x.MuscleKg).HasPrecision(6, 2);
            e.HasIndex(x => x.MeasuredOn);
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        mb.Entity<BodyPartMeasurement>(e =>
        {
            e.Property(x => x.ValueCm).HasPrecision(5, 1);
            e.Property(x => x.BodyPart).HasConversion<int>();
            e.HasIndex(x => x.MeasuredOn);
            e.HasIndex(x => new { x.UserId, x.BodyPart, x.MeasuredOn });
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        mb.Entity<BloodPressureReading>(e =>
        {
            e.HasIndex(x => x.MeasuredAt);
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        mb.Entity<ColdEpisode>(e =>
        {
            e.HasIndex(x => x.StartDate);
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        mb.Entity<CardioSession>(e =>
        {
            e.Property(x => x.MaxSpeedKph).HasPrecision(5, 2);
            e.HasIndex(x => x.SessionDate);
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        // --- Nutrition ---
        mb.Entity<Food>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.CaloriesPer100g).HasPrecision(7, 2);
            e.Property(x => x.ProteinPer100g).HasPrecision(7, 2);
            e.Property(x => x.CarbsPer100g).HasPrecision(7, 2);
            e.Property(x => x.FatPer100g).HasPrecision(7, 2);
            e.Property(x => x.FiberPer100g).HasPrecision(7, 2);
            e.HasIndex(x => x.Name);
        });

        mb.Entity<Recipe>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Ingredients)
                .WithOne(x => x.Recipe!)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RecipeIngredient>(e =>
        {
            e.Property(x => x.Grams).HasPrecision(8, 2);
            e.HasOne(x => x.Food)
                .WithMany()
                .HasForeignKey(x => x.FoodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MealEntry>(e =>
        {
            e.Property(x => x.Grams).HasPrecision(8, 2);
            e.Property(x => x.Servings).HasPrecision(6, 2);
            e.HasIndex(x => x.EatenOn);
            e.HasOne(x => x.Food).WithMany().HasForeignKey(x => x.FoodId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(ct);
    }
}