using FitTrack.Application.Abstractions;
using FitTrack.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Workouts;

public interface IWorkoutSessionService
{
    /// <summary>Today's (or most relevant) workout session to show on the home screen.</summary>
    Task<WorkoutSessionDto?> GetTodaysSessionAsync(CancellationToken ct = default);

    Task<List<WorkoutSessionDto>> ListForInstanceAsync(int instanceId, CancellationToken ct = default);
    Task<WorkoutSessionDto?> GetAsync(int sessionId, CancellationToken ct = default);
    Task<bool> LogSetAsync(int logId, UpdateExerciseLogDto dto, CancellationToken ct = default);
    Task<bool> CompleteSessionAsync(int sessionId, CancellationToken ct = default);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Prefer a session scheduled today for an active instance owned by the user.
        var session = await _db.WorkoutSessions
            .AsNoTracking()
            .Include(s => s.MesocycleWorkout)
            .Include(s => s.MesocycleInstance)
            .Include(s => s.Logs).ThenInclude(l => l.Exercise)
            .Where(s => s.MesocycleInstance!.UserId == userId)
            .Where(s => s.MesocycleInstance!.Status == MesocycleInstanceStatus.Active)
            .Where(s => s.CompletedAt == null)
            .OrderBy(s => s.ScheduledDate)
            .ThenBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.ScheduledDate == today, ct);

        // Fall back to the next upcoming pending session (in case user missed a day).
        if (session is null)
        {
            session = await _db.WorkoutSessions
                .AsNoTracking()
                .Include(s => s.MesocycleWorkout)
                .Include(s => s.MesocycleInstance)
                .Include(s => s.Logs).ThenInclude(l => l.Exercise)
                .Where(s => s.MesocycleInstance!.UserId == userId)
                .Where(s => s.MesocycleInstance!.Status == MesocycleInstanceStatus.Active)
                .Where(s => s.CompletedAt == null)
                .OrderBy(s => s.ScheduledDate)
                .ThenBy(s => s.Id)
                .FirstOrDefaultAsync(ct);
        }

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

    private static WorkoutSessionDto ToDto(WorkoutSession s) => new(
        s.Id, s.MesocycleInstanceId, s.MesocycleWorkoutId,
        s.MesocycleWorkout?.Name ?? string.Empty,
        s.ScheduledDate, s.WeekNumber, s.StartedAt, s.CompletedAt, s.Notes,
        s.Logs.OrderBy(l => l.ExerciseId).ThenBy(l => l.SetNumber).Select(l => new ExerciseLogDto(
            l.Id, l.ExerciseId,
            l.Exercise?.Name ?? string.Empty,
            l.SetNumber, l.TargetReps, l.TargetWeightKg,
            l.ActualReps, l.ActualWeightKg, l.PerformedAt, l.Notes
        )).ToList()
    );
}
