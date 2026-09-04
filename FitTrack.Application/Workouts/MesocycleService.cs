using FitTrack.Application.Abstractions;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Workouts;

public interface IMesocycleService
{
    Task<List<MesocycleDto>> ListAsync(CancellationToken ct = default);
    Task<MesocycleDto?> GetAsync(int id, CancellationToken ct = default);
    Task<MesocycleDto> CreateAsync(CreateMesocycleDto dto, CancellationToken ct = default);
    Task<MesocycleDto?> CopyAsync(int id, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, CreateMesocycleDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class MesocycleService : IMesocycleService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public MesocycleService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<MesocycleDto>> ListAsync(CancellationToken ct = default)
    {
        _current.RequireUserId();
        var entities = await _db.Mesocycles
            .AsNoTracking()
            .Include(m => m.Workouts)
                .ThenInclude(w => w.PlannedExercises)
                    .ThenInclude(p => p.Exercise)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        return entities.Select(ToDto).ToList();
    }

    public async Task<MesocycleDto?> GetAsync(int id, CancellationToken ct = default)
    {
        _current.RequireUserId();
        var m = await _db.Mesocycles
            .AsNoTracking()
            .Include(m => m.Workouts.OrderBy(w => w.DayOrder))
                .ThenInclude(w => w.PlannedExercises.OrderBy(p => p.OrderIndex))
                    .ThenInclude(p => p.Exercise)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        return m is null ? null : ToDto(m);
    }

    public async Task<MesocycleDto> CreateAsync(CreateMesocycleDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var m = new Mesocycle
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            DurationWeeks = dto.DurationWeeks,
            HasRampUpWeek = dto.HasRampUpWeek,
            Workouts = dto.Workouts.Select(CreateWorkout).ToList()
        };
        _db.Mesocycles.Add(m);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(m.Id, ct))!;
    }

    public async Task<MesocycleDto?> CopyAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();

        var source = await _db.Mesocycles
            .AsNoTracking()
            .Include(m => m.Workouts)
                .ThenInclude(w => w.PlannedExercises)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (source is null) return null;

        var copy = new Mesocycle
        {
            Name = $"{source.Name} (copy)",
            Description = source.Description,
            DurationWeeks = source.DurationWeeks,
            HasRampUpWeek = source.HasRampUpWeek,
            Workouts = source.Workouts.Select(w => new MesocycleWorkout
            {
                Name = w.Name,
                DayOrder = w.DayOrder,
                PlannedExercises = w.PlannedExercises.Select(p => new PlannedExercise
                {
                    ExerciseId = p.ExerciseId,
                    TargetSets = p.TargetSets,
                    TargetReps = p.TargetReps,
                    TargetWeightKg = p.TargetWeightKg,
                    OrderIndex = p.OrderIndex
                }).ToList()
            }).ToList()
        };

        _db.Mesocycles.Add(copy);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(copy.Id, ct))!;
    }

    public async Task<bool> UpdateAsync(int id, CreateMesocycleDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var m = await _db.Mesocycles
            .Include(m => m.Workouts)
                .ThenInclude(w => w.PlannedExercises)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (m is null) return false;

        m.Name = dto.Name.Trim();
        m.Description = dto.Description;
        m.DurationWeeks = dto.DurationWeeks;
        m.HasRampUpWeek = dto.HasRampUpWeek;
        m.UpdatedAt = DateTimeOffset.UtcNow;

        var hasStartedInstances = await _db.MesocycleInstances
            .AnyAsync(i => i.MesocycleId == id, ct);

        if (hasStartedInstances)
        {
            ApplyInPlaceTemplateUpdate(m, dto);
        }
        else
        {
            ReplaceTemplateStructure(m, dto);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private void ReplaceTemplateStructure(Mesocycle mesocycle, CreateMesocycleDto dto)
    {
        foreach (var workout in mesocycle.Workouts.ToList())
        {
            _db.PlannedExercises.RemoveRange(workout.PlannedExercises);
            _db.MesocycleWorkouts.Remove(workout);
        }

        mesocycle.Workouts = dto.Workouts.Select(CreateWorkout).ToList();
    }

    private static void ApplyInPlaceTemplateUpdate(Mesocycle mesocycle, CreateMesocycleDto dto)
    {
        var existingWorkouts = mesocycle.Workouts
            .OrderBy(w => w.DayOrder)
            .ThenBy(w => w.Id)
            .ToList();

        if (existingWorkouts.Count != dto.Workouts.Count)
            throw new InvalidOperationException("This mesocycle already has started instances, so you can edit existing workout details but cannot add or remove workout days.");

        for (var workoutIndex = 0; workoutIndex < existingWorkouts.Count; workoutIndex++)
        {
            var existingWorkout = existingWorkouts[workoutIndex];
            var incomingWorkout = dto.Workouts[workoutIndex];

            existingWorkout.Name = incomingWorkout.Name;
            existingWorkout.DayOrder = incomingWorkout.DayOrder;

            var existingExercises = existingWorkout.PlannedExercises
                .OrderBy(p => p.OrderIndex)
                .ThenBy(p => p.Id)
                .ToList();

            if (existingExercises.Count != incomingWorkout.PlannedExercises.Count)
                throw new InvalidOperationException("This mesocycle already has started instances, so you can edit existing exercise details but cannot add or remove exercises.");

            for (var exerciseIndex = 0; exerciseIndex < existingExercises.Count; exerciseIndex++)
            {
                var existingExercise = existingExercises[exerciseIndex];
                var incomingExercise = incomingWorkout.PlannedExercises[exerciseIndex];

                existingExercise.ExerciseId = incomingExercise.ExerciseId;
                existingExercise.TargetSets = incomingExercise.TargetSets;
                existingExercise.TargetReps = incomingExercise.TargetReps;
                existingExercise.TargetWeightKg = incomingExercise.TargetWeightKg;
                existingExercise.OrderIndex = incomingExercise.OrderIndex;
            }
        }
    }

    private static MesocycleWorkout CreateWorkout(CreateMesocycleWorkoutDto dto) => new()
    {
        Name = dto.Name,
        DayOrder = dto.DayOrder,
        PlannedExercises = dto.PlannedExercises.Select(CreatePlannedExercise).ToList()
    };

    private static PlannedExercise CreatePlannedExercise(CreatePlannedExerciseDto dto) => new()
    {
        ExerciseId = dto.ExerciseId,
        TargetSets = dto.TargetSets,
        TargetReps = dto.TargetReps,
        TargetWeightKg = dto.TargetWeightKg,
        OrderIndex = dto.OrderIndex
    };

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();

        var m = await _db.Mesocycles
            .Include(m => m.Workouts)
                .ThenInclude(w => w.PlannedExercises)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (m is null) return false;

        // Load all instances with their sessions and logs so EF can cascade the deletes.
        var instances = await _db.MesocycleInstances
            .Include(i => i.Sessions)
                .ThenInclude(s => s.Logs)
            .Where(i => i.MesocycleId == id)
            .ToListAsync(ct);

        foreach (var instance in instances)
        {
            foreach (var session in instance.Sessions)
            {
                _db.ExerciseLogs.RemoveRange(session.Logs);
            }
            _db.WorkoutSessions.RemoveRange(instance.Sessions);
        }
        _db.MesocycleInstances.RemoveRange(instances);

        // Remove template children, then the template itself.
        foreach (var w in m.Workouts)
            _db.PlannedExercises.RemoveRange(w.PlannedExercises);
        _db.MesocycleWorkouts.RemoveRange(m.Workouts);
        _db.Mesocycles.Remove(m);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private void RequireAdmin()
    {
        _current.RequireUserId();
        if (!_current.IsAdmin)
            throw new ForbiddenException("Only admins can modify shared mesocycle templates.");
    }

    private static MesocycleDto ToDto(Mesocycle m) => new(
        m.Id, m.Name, m.Description, m.DurationWeeks, m.HasRampUpWeek,
        m.Workouts.OrderBy(w => w.DayOrder).Select(w => new MesocycleWorkoutDto(
            w.Id, w.Name, w.DayOrder,
            w.PlannedExercises.OrderBy(p => p.OrderIndex).Select(p => new PlannedExerciseDto(
                p.Id, p.ExerciseId,
                p.Exercise?.Name ?? string.Empty,
                p.TargetSets, p.TargetReps, p.TargetWeightKg, p.OrderIndex
            )).ToList()
        )).ToList()
    );
}
