using FitTrack.Application.Abstractions;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Workouts;

public interface IMesocycleService
{
    Task<List<MesocycleDto>> ListAsync(CancellationToken ct = default);
    Task<MesocycleDto?> GetAsync(int id, CancellationToken ct = default);
    Task<MesocycleDto> CreateAsync(CreateMesocycleDto dto, CancellationToken ct = default);
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
            Workouts = dto.Workouts.Select(w => new MesocycleWorkout
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
        _db.Mesocycles.Add(m);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(m.Id, ct))!;
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

        // Replace the template structure wholesale. Simple and robust for a personal app.
        foreach (var w in m.Workouts.ToList())
        {
            _db.PlannedExercises.RemoveRange(w.PlannedExercises);
            _db.MesocycleWorkouts.Remove(w);
        }
        m.Workouts = dto.Workouts.Select(w => new MesocycleWorkout
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
        }).ToList();

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();
        var m = await _db.Mesocycles.FindAsync(new object?[] { id }, ct);
        if (m is null) return false;
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
