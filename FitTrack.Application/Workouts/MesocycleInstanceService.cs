using FitTrack.Application.Abstractions;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Workouts;

public interface IMesocycleInstanceService
{
    Task<List<MesocycleInstanceDto>> ListAsync(CancellationToken ct = default);
    Task<MesocycleInstanceDto?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Starts a new instance of the given mesocycle for the current user. If a previous
    /// instance of this mesocycle owned by the same user has status=Completed, the new
    /// instance gets a WeightMultiplier equal to previous.WeightMultiplier * 1.05.
    /// Sessions are created for each week × workout in the template.
    /// </summary>
    Task<MesocycleInstanceDto> StartAsync(int mesocycleId, DateOnly startDate, CancellationToken ct = default);

    Task<bool> CompleteAsync(int instanceId, CancellationToken ct = default);
    Task<bool> AbandonAsync(int instanceId, CancellationToken ct = default);
}

public class MesocycleInstanceService : IMesocycleInstanceService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;
    private const decimal ProgressionFactor = 1.05m;

    public MesocycleInstanceService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<MesocycleInstanceDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.MesocycleInstances
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .Include(i => i.Mesocycle)
            .OrderByDescending(i => i.StartDate)
            .Select(i => new MesocycleInstanceDto(
                i.Id, i.MesocycleId, i.Mesocycle!.Name, i.StartDate, i.EndDate,
                i.Status, i.WeightMultiplier))
            .ToListAsync(ct);
    }

    public async Task<MesocycleInstanceDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var i = await _db.MesocycleInstances
            .AsNoTracking()
            .Include(i => i.Mesocycle)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (i is null) return null;
        if (i.UserId != userId) throw new ForbiddenException("You do not own this mesocycle instance.");
        return new MesocycleInstanceDto(
            i.Id, i.MesocycleId, i.Mesocycle!.Name, i.StartDate, i.EndDate, i.Status, i.WeightMultiplier);
    }

    public async Task<MesocycleInstanceDto> StartAsync(int mesocycleId, DateOnly startDate, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();

        var meso = await _db.Mesocycles
            .Include(m => m.Workouts.OrderBy(w => w.DayOrder))
                .ThenInclude(w => w.PlannedExercises.OrderBy(p => p.OrderIndex))
                    .ThenInclude(pe => pe.Exercise)
            .FirstOrDefaultAsync(m => m.Id == mesocycleId, ct)
            ?? throw new InvalidOperationException($"Mesocycle {mesocycleId} not found.");

        if (meso.Workouts.Count == 0)
            throw new InvalidOperationException("Cannot start an instance of a mesocycle with no workouts.");

        // Progression: look up the most recent Completed instance for this mesocycle
        // OWNED BY THE CURRENT USER. Each user has their own progression timeline.
        var lastCompleted = await _db.MesocycleInstances
            .Where(i => i.MesocycleId == mesocycleId
                     && i.UserId == userId
                     && i.Status == MesocycleInstanceStatus.Completed)
            .OrderByDescending(i => i.EndDate)
            .ThenByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);

        var multiplier = lastCompleted is null
            ? 1.00m
            : decimal.Round(lastCompleted.WeightMultiplier * ProgressionFactor, 4);

        var instance = new MesocycleInstance
        {
            UserId = userId,
            MesocycleId = mesocycleId,
            StartDate = startDate,
            Status = MesocycleInstanceStatus.Active,
            WeightMultiplier = multiplier
        };

        // Generate sessions: for week 1..DurationWeeks, for each workout in DayOrder.
        for (int week = 1; week <= Math.Max(1, meso.DurationWeeks); week++)
        {
            int dayOffset = (week - 1) * 7;
            foreach (var workout in meso.Workouts.OrderBy(w => w.DayOrder))
            {
                var sessionDate = startDate.AddDays(dayOffset + (workout.DayOrder - 1));
                var session = new WorkoutSession
                {
                    MesocycleWorkoutId = workout.Id,
                    ScheduledDate = sessionDate,
                    WeekNumber = week
                };

                foreach (var pe in workout.PlannedExercises.OrderBy(p => p.OrderIndex))
                {
                    var roundedBase = RoundToNearest2p5(pe.TargetWeightKg * multiplier);
                    var increment = pe.Exercise?.WeeklyIncrementKg ?? 0.25m;
                    var targetKg = roundedBase + (week - 1) * increment;

                    // Ramp-up week: cap sets to 1 so the athlete eases into the mesocycle.
                    var setsThisWeek = (week == 1 && meso.HasRampUpWeek) ? 1 : Math.Max(1, pe.TargetSets);

                    for (int setNumber = 1; setNumber <= setsThisWeek; setNumber++)
                    {
                        session.Logs.Add(new ExerciseLog
                        {
                            ExerciseId = pe.ExerciseId,
                            PlannedExerciseId = pe.Id,
                            SetNumber = setNumber,
                            TargetReps = pe.TargetReps,
                            TargetWeightKg = targetKg
                        });
                    }
                }

                instance.Sessions.Add(session);
            }
        }

        _db.MesocycleInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        return new MesocycleInstanceDto(
            instance.Id, mesocycleId, meso.Name, instance.StartDate, instance.EndDate,
            instance.Status, instance.WeightMultiplier);
    }

    public async Task<bool> CompleteAsync(int instanceId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var i = await _db.MesocycleInstances.FindAsync(new object?[] { instanceId }, ct);
        if (i is null) return false;
        if (i.UserId != userId) throw new ForbiddenException("You do not own this mesocycle instance.");
        i.Status = MesocycleInstanceStatus.Completed;
        i.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        i.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AbandonAsync(int instanceId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var i = await _db.MesocycleInstances.FindAsync(new object?[] { instanceId }, ct);
        if (i is null) return false;
        if (i.UserId != userId) throw new ForbiddenException("You do not own this mesocycle instance.");
        i.Status = MesocycleInstanceStatus.Abandoned;
        i.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        i.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Rounds a kg value to the nearest 2.5 (gym plate reality).</summary>
    public static decimal RoundToNearest2p5(decimal value)
    {
        if (value <= 0) return 0;
        return Math.Round(value / 2.5m, MidpointRounding.AwayFromZero) * 2.5m;
    }
}
