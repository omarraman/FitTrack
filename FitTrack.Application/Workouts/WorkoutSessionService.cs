using FitTrack.Application.Abstractions;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Workouts;

public interface IWorkoutSessionService
{
    /// <summary>The next pending (not completed, not skipped) session in sequential order.</summary>
    Task<WorkoutSessionDto?> GetTodaysSessionAsync(CancellationToken ct = default);

    Task<List<WorkoutSessionDto>> ListForInstanceAsync(int instanceId, CancellationToken ct = default);
    Task<WorkoutSessionDto?> GetAsync(int sessionId, CancellationToken ct = default);
    Task<bool> LogSetAsync(int logId, UpdateExerciseLogDto dto, CancellationToken ct = default);
    Task<bool> CompleteSessionAsync(int sessionId, CancellationToken ct = default);

    /// <summary>Skip a single session (removes it from the pending queue).</summary>
    Task<bool> SkipSessionAsync(int sessionId, CancellationToken ct = default);

    /// <summary>
    /// Skips all remaining (not yet completed) sessions in the given week of the instance,
    /// allowing the user to close a week early.
    /// </summary>
    Task<int> SkipWeekAsync(int instanceId, int weekNumber, CancellationToken ct = default);
}

public class WorkoutSessionService : IWorkoutSessionService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public WorkoutSessionService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<WorkoutSessionDto?> GetTodaysSessionAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();

        // Always return the next pending session in sequential order (WeekNumber → DayOrder).
        // ScheduledDate is historical metadata only — do NOT filter by calendar date, because
        // a user may miss a day and cross into a new calendar week without finishing the prior week.
        var session = await _db.WorkoutSessions
            .AsNoTracking()
            .Include(s => s.MesocycleWorkout)
            .Include(s => s.MesocycleInstance)
            .Include(s => s.Logs).ThenInclude(l => l.Exercise)
            .Where(s => s.MesocycleInstance!.UserId == userId)
            .Where(s => s.MesocycleInstance!.Status == MesocycleInstanceStatus.Active)
            .Where(s => s.CompletedAt == null && !s.IsSkipped)
            .OrderBy(s => s.WeekNumber)
            .ThenBy(s => s.MesocycleWorkout!.DayOrder)
            .ThenBy(s => s.Id)
            .FirstOrDefaultAsync(ct);

        return session is null ? null : ToDto(session);
    }

    public async Task<List<WorkoutSessionDto>> ListForInstanceAsync(int instanceId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var sessions = await _db.WorkoutSessions
            .AsNoTracking()
            .Include(s => s.MesocycleWorkout)
            .Include(s => s.MesocycleInstance)
            .Include(s => s.Logs).ThenInclude(l => l.Exercise)
            .Where(s => s.MesocycleInstanceId == instanceId)
            .Where(s => s.MesocycleInstance!.UserId == userId)
            .OrderBy(s => s.ScheduledDate)
            .ToListAsync(ct);

        return sessions.Select(ToDto).ToList();
    }

    public async Task<WorkoutSessionDto?> GetAsync(int sessionId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var session = await _db.WorkoutSessions
            .AsNoTracking()
            .Include(s => s.MesocycleWorkout)
            .Include(s => s.MesocycleInstance)
            .Include(s => s.Logs).ThenInclude(l => l.Exercise)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return null;
        if (session.MesocycleInstance!.UserId != userId)
            throw new ForbiddenException("You do not own this workout session.");
        return ToDto(session);
    }

    public async Task<bool> LogSetAsync(int logId, UpdateExerciseLogDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var log = await _db.ExerciseLogs
            .Include(l => l.WorkoutSession)
                .ThenInclude(s => s!.MesocycleInstance)
            .FirstOrDefaultAsync(l => l.Id == logId, ct);
        if (log is null) return false;
        if (log.WorkoutSession!.MesocycleInstance!.UserId != userId)
            throw new ForbiddenException("You do not own this exercise log.");

        log.ActualReps = dto.ActualReps;
        log.ActualWeightKg = dto.ActualWeightKg;
        log.Notes = dto.Notes;
        log.PerformedAt = DateTimeOffset.UtcNow;
        log.UpdatedAt = DateTimeOffset.UtcNow;

        // Stamp the session as started if it isn't yet.
        if (log.WorkoutSession is not null && log.WorkoutSession.StartedAt is null)
            log.WorkoutSession.StartedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CompleteSessionAsync(int sessionId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var session = await _db.WorkoutSessions
            .Include(s => s.MesocycleInstance)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return false;
        if (session.MesocycleInstance!.UserId != userId)
            throw new ForbiddenException("You do not own this workout session.");
        session.CompletedAt = DateTimeOffset.UtcNow;
        if (session.StartedAt is null) session.StartedAt = session.CompletedAt;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SkipSessionAsync(int sessionId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var session = await _db.WorkoutSessions
            .Include(s => s.MesocycleInstance)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return false;
        if (session.MesocycleInstance!.UserId != userId)
            throw new ForbiddenException("You do not own this workout session.");
        if (session.CompletedAt is not null)
            throw new InvalidOperationException("Cannot skip a session that is already completed.");
        session.IsSkipped = true;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> SkipWeekAsync(int instanceId, int weekNumber, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var instance = await _db.MesocycleInstances.FindAsync(new object?[] { instanceId }, ct);
        if (instance is null) return 0;
        if (instance.UserId != userId)
            throw new ForbiddenException("You do not own this mesocycle instance.");

        var sessions = await _db.WorkoutSessions
            .Where(s => s.MesocycleInstanceId == instanceId
                     && s.WeekNumber == weekNumber
                     && s.CompletedAt == null
                     && !s.IsSkipped)
            .ToListAsync(ct);

        foreach (var s in sessions)
        {
            s.IsSkipped = true;
            s.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return sessions.Count;
    }

    private static WorkoutSessionDto ToDto(WorkoutSession s) => new(
        s.Id, s.MesocycleInstanceId, s.MesocycleWorkoutId,
        s.MesocycleWorkout?.Name ?? string.Empty,
        s.ScheduledDate, s.WeekNumber, s.StartedAt, s.CompletedAt, s.IsSkipped, s.Notes,
        s.Logs.OrderBy(l => l.ExerciseId).ThenBy(l => l.SetNumber).Select(l => new ExerciseLogDto(
            l.Id, l.ExerciseId,
            l.Exercise?.Name ?? string.Empty,
            l.SetNumber, l.TargetReps, l.TargetWeightKg,
            l.ActualReps, l.ActualWeightKg, l.PerformedAt, l.Notes
        )).ToList()
    );
}
