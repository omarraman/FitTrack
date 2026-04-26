using FitTrack.Application.Abstractions;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Workouts;

public interface IExerciseService
{
    Task<List<ExerciseDto>> ListAsync(CancellationToken ct = default);
    Task<ExerciseDto?> GetAsync(int id, CancellationToken ct = default);
    Task<ExerciseDto> CreateAsync(CreateExerciseDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, CreateExerciseDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class ExerciseService : IExerciseService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public ExerciseService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    // Reading shared data is allowed for any authenticated user.
    public async Task<List<ExerciseDto>> ListAsync(CancellationToken ct = default)
    {
        _current.RequireUserId();
        return await _db.Exercises
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ExerciseDto(x.Id, x.Name, x.Description, x.PrimaryMuscleGroup, x.WeeklyIncrementKg))
            .ToListAsync(ct);
    }

    public async Task<ExerciseDto?> GetAsync(int id, CancellationToken ct = default)
    {
        _current.RequireUserId();
        var x = await _db.Exercises.FindAsync(new object?[] { id }, ct);
        return x is null ? null : new ExerciseDto(x.Id, x.Name, x.Description, x.PrimaryMuscleGroup, x.WeeklyIncrementKg);
    }

    public async Task<ExerciseDto> CreateAsync(CreateExerciseDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var ex = new Exercise
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            PrimaryMuscleGroup = dto.PrimaryMuscleGroup,
            WeeklyIncrementKg = dto.WeeklyIncrementKg
        };
        _db.Exercises.Add(ex);
        await _db.SaveChangesAsync(ct);
        return new ExerciseDto(ex.Id, ex.Name, ex.Description, ex.PrimaryMuscleGroup, ex.WeeklyIncrementKg);
    }

    public async Task<bool> UpdateAsync(int id, CreateExerciseDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var ex = await _db.Exercises.FindAsync(new object?[] { id }, ct);
        if (ex is null) return false;
        ex.Name = dto.Name.Trim();
        ex.Description = dto.Description;
        ex.PrimaryMuscleGroup = dto.PrimaryMuscleGroup;
        ex.WeeklyIncrementKg = dto.WeeklyIncrementKg;
        ex.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();
        var ex = await _db.Exercises.FindAsync(new object?[] { id }, ct);
        if (ex is null) return false;
        _db.Exercises.Remove(ex);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private void RequireAdmin()
    {
        _current.RequireUserId();
        if (!_current.IsAdmin)
            throw new ForbiddenException("Only admins can modify shared exercises.");
    }
}
