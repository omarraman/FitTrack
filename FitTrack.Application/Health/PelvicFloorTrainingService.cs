using FitTrack.Application.Abstractions;
using FitTrack.Domain.Health;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Health;

public interface IPelvicFloorTrainingService
{
    Task<PelvicFloorProgramSummaryDto?> GetActiveOrMostRecentAsync(CancellationToken ct = default);
    Task<PelvicFloorProgramSummaryDto> StartAsync(StartPelvicFloorProgramDto dto, CancellationToken ct = default);
    Task<PelvicFloorSessionLogDto> LogSessionAsync(int programId, LogPelvicFloorSessionDto dto, CancellationToken ct = default);
    Task<PelvicFloorDailyCheckInDto> UpsertCheckInAsync(int programId, UpsertPelvicFloorCheckInDto dto, CancellationToken ct = default);
    Task<bool> CompleteAsync(int programId, DateOnly completedOn, CancellationToken ct = default);
    Task<bool> CancelAsync(int programId, DateOnly cancelledOn, CancellationToken ct = default);
    Task<bool> DeleteSessionAsync(int programId, int sessionId, CancellationToken ct = default);
    Task<bool> DeleteCheckInAsync(int programId, int checkInId, CancellationToken ct = default);
}

/// <summary>
/// Private, user-owned pelvic-floor training. Every query and mutation is scoped to the
/// signed-in user; the plan itself comes from <see cref="PelvicFloorProgramDefinition"/> and is
/// never accepted from the client.
/// </summary>
public class PelvicFloorTrainingService : IPelvicFloorTrainingService
{
    private const int RecentSessionCount = 10;
    private const int RecentCheckInCount = 14;
    private const int SafetyFlagWindowDays = 14;

    private const int ProgramNotesMaxLength = 2000;
    private const int EntryNotesMaxLength = 1000;

    private const int MaxSustainedSets = 10;
    private const int MaxSustainedRepsPerSet = 30;
    private const int MaxHoldSeconds = 30;
    private const int MaxRestSeconds = 30;
    private const int MaxQuickReps = 50;

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public PelvicFloorTrainingService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PelvicFloorProgramSummaryDto?> GetActiveOrMostRecentAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();

        var program = await OwnedPrograms(userId)
            .Where(p => p.Status == PelvicFloorProgramStatus.Active)
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        program ??= await OwnedPrograms(userId)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return program is null ? null : BuildSummary(program);
    }

    public async Task<PelvicFloorProgramSummaryDto> StartAsync(StartPelvicFloorProgramDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var today = Today();

        if (dto.StartDate > today)
            throw new InvalidOperationException("The program start date cannot be in the future.");

        var hasActive = await _db.PelvicFloorPrograms
            .AnyAsync(p => p.UserId == userId && p.Status == PelvicFloorProgramStatus.Active, ct);
        if (hasActive)
            throw new InvalidOperationException(
                "You already have an active pelvic-floor program. Complete or cancel it before starting a new one.");

        var program = new PelvicFloorProgram
        {
            UserId = userId,
            StartDate = dto.StartDate,
            Status = PelvicFloorProgramStatus.Active,
            Notes = NormalizeNotes(dto.Notes, ProgramNotesMaxLength, "Program notes")
        };

        _db.PelvicFloorPrograms.Add(program);
        await _db.SaveChangesAsync(ct);

        return BuildSummary(program);
    }

    public async Task<PelvicFloorSessionLogDto> LogSessionAsync(int programId, LogPelvicFloorSessionDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var program = await LoadProgramAsync(programId, ct)
            ?? throw new KeyNotFoundException("Pelvic-floor program not found.");
        EnsureOwned(program, userId);
        EnsureActive(program);

        var today = Today();
        if (dto.SessionDate > today)
            throw new InvalidOperationException("You cannot log a pelvic-floor session for a future date.");
        if (dto.SessionDate < program.StartDate)
            throw new InvalidOperationException("You cannot log a pelvic-floor session before the program start date.");
        if (!PelvicFloorProgramDefinition.IsWithinProgram(program.StartDate, dto.SessionDate))
            throw new InvalidOperationException("That date is outside the 10-week plan window.");

        var weekNumber = PelvicFloorProgramDefinition.GetWeekNumber(program.StartDate, dto.SessionDate);
        var prescription = PelvicFloorProgramDefinition.GetPrescription(weekNumber);
        var isOptional = !PelvicFloorProgramDefinition.IsScheduledDate(program.StartDate, dto.SessionDate);

        ValidateSession(dto, prescription);

        var alreadyLogged = await _db.PelvicFloorSessionLogs
            .AnyAsync(s => s.PelvicFloorProgramId == program.Id && s.SessionDate == dto.SessionDate, ct);
        if (alreadyLogged)
            throw new InvalidOperationException(
                $"A pelvic-floor session is already logged for {dto.SessionDate:yyyy-MM-dd}. " +
                "Delete that entry first if you need to record it differently.");

        var log = new PelvicFloorSessionLog
        {
            PelvicFloorProgramId = program.Id,
            SessionDate = dto.SessionDate,
            WeekNumber = weekNumber,
            IsOptional = isOptional,
            Outcome = dto.Outcome,
            CompletedSustainedSets = dto.CompletedSustainedSets,
            CompletedSustainedRepsPerSet = dto.CompletedSustainedRepsPerSet,
            CompletedHoldSeconds = dto.CompletedHoldSeconds,
            CompletedRestSeconds = dto.CompletedRestSeconds,
            CompletedQuickReps = dto.CompletedQuickReps,
            CompletedBreathingFinish = dto.CompletedBreathingFinish,
            EffortRating = dto.EffortRating,
            RelaxationRating = dto.RelaxationRating,
            Notes = NormalizeNotes(dto.Notes, EntryNotesMaxLength, "Session notes")
        };

        _db.PelvicFloorSessionLogs.Add(log);
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToDto(log);
    }

    public async Task<PelvicFloorDailyCheckInDto> UpsertCheckInAsync(int programId, UpsertPelvicFloorCheckInDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var program = await LoadProgramAsync(programId, ct)
            ?? throw new KeyNotFoundException("Pelvic-floor program not found.");
        EnsureOwned(program, userId);
        EnsureActive(program);

        var today = Today();
        if (dto.CheckInDate > today)
            throw new InvalidOperationException("You cannot record a check-in for a future date.");
        if (dto.CheckInDate < program.StartDate)
            throw new InvalidOperationException("You cannot record a check-in before the program start date.");

        ValidateRating(dto.PelvicTensionRating, "Pelvic tension rating");

        var notes = NormalizeNotes(dto.Notes, EntryNotesMaxLength, "Check-in notes");

        var checkIn = await _db.PelvicFloorDailyCheckIns
            .FirstOrDefaultAsync(c => c.PelvicFloorProgramId == program.Id && c.CheckInDate == dto.CheckInDate, ct);

        if (checkIn is null)
        {
            checkIn = new PelvicFloorDailyCheckIn
            {
                PelvicFloorProgramId = program.Id,
                CheckInDate = dto.CheckInDate,
                PelvicTensionRating = dto.PelvicTensionRating,
                HasPainOrUrinarySymptoms = dto.HasPainOrUrinarySymptoms,
                Notes = notes
            };
            _db.PelvicFloorDailyCheckIns.Add(checkIn);
        }
        else
        {
            checkIn.PelvicTensionRating = dto.PelvicTensionRating;
            checkIn.HasPainOrUrinarySymptoms = dto.HasPainOrUrinarySymptoms;
            checkIn.Notes = notes;
            checkIn.UpdatedAt = DateTimeOffset.UtcNow;
        }

        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToDto(checkIn);
    }

    public async Task<bool> CompleteAsync(int programId, DateOnly completedOn, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var program = await LoadProgramAsync(programId, ct);
        if (program is null) return false;
        EnsureOwned(program, userId);
        EnsureActive(program);

        if (completedOn > Today())
            throw new InvalidOperationException("The completion date cannot be in the future.");

        var earliest = program.StartDate.AddDays(PelvicFloorProgramDefinition.CompletionDayOffset);
        if (completedOn < earliest)
            throw new InvalidOperationException(
                $"This program can only be completed from {earliest:yyyy-MM-dd} (the end of day 70) onwards.");

        program.Status = PelvicFloorProgramStatus.Completed;
        program.CompletedOn = completedOn;
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelAsync(int programId, DateOnly cancelledOn, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var program = await LoadProgramAsync(programId, ct);
        if (program is null) return false;
        EnsureOwned(program, userId);
        EnsureActive(program);

        if (cancelledOn > Today())
            throw new InvalidOperationException("The cancellation date cannot be in the future.");
        if (cancelledOn < program.StartDate)
            throw new InvalidOperationException("The cancellation date cannot be before the program start date.");

        program.Status = PelvicFloorProgramStatus.Cancelled;
        program.CancelledOn = cancelledOn;
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteSessionAsync(int programId, int sessionId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var program = await LoadProgramAsync(programId, ct);
        if (program is null) return false;
        EnsureOwned(program, userId);

        var log = await _db.PelvicFloorSessionLogs
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.PelvicFloorProgramId == program.Id, ct);
        if (log is null) return false;

        _db.PelvicFloorSessionLogs.Remove(log);
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteCheckInAsync(int programId, int checkInId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var program = await LoadProgramAsync(programId, ct);
        if (program is null) return false;
        EnsureOwned(program, userId);

        var checkIn = await _db.PelvicFloorDailyCheckIns
            .FirstOrDefaultAsync(c => c.Id == checkInId && c.PelvicFloorProgramId == program.Id, ct);
        if (checkIn is null) return false;

        _db.PelvicFloorDailyCheckIns.Remove(checkIn);
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<PelvicFloorProgram> OwnedPrograms(int userId) => _db.PelvicFloorPrograms
        .AsNoTracking()
        .Include(p => p.Sessions)
        .Include(p => p.CheckIns)
        .Where(p => p.UserId == userId);

    private Task<PelvicFloorProgram?> LoadProgramAsync(int programId, CancellationToken ct)
        => _db.PelvicFloorPrograms.FirstOrDefaultAsync(p => p.Id == programId, ct);

    private static void EnsureOwned(PelvicFloorProgram program, int userId)
    {
        if (program.UserId != userId)
            throw new ForbiddenException("You do not own this pelvic-floor program.");
    }

    private static void EnsureActive(PelvicFloorProgram program)
    {
        if (program.Status != PelvicFloorProgramStatus.Active)
            throw new InvalidOperationException("This pelvic-floor program is no longer active.");
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static string? NormalizeNotes(string? notes, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var trimmed = notes.Trim();
        if (trimmed.Length > maxLength)
            throw new InvalidOperationException($"{label} cannot be longer than {maxLength} characters.");
        return trimmed;
    }

    private static void ValidateRating(int? rating, string label)
    {
        if (rating is not null && rating is < 1 or > 5)
            throw new InvalidOperationException($"{label} must be between 1 and 5.");
    }

    private static void ValidateSession(LogPelvicFloorSessionDto dto, PelvicFloorPrescriptionDto prescription)
    {
        RequireRange(dto.CompletedSustainedSets, MaxSustainedSets, "Completed sustained sets");
        RequireRange(dto.CompletedSustainedRepsPerSet, MaxSustainedRepsPerSet, "Completed sustained reps per set");
        RequireRange(dto.CompletedHoldSeconds, MaxHoldSeconds, "Completed hold seconds");
        RequireRange(dto.CompletedRestSeconds, MaxRestSeconds, "Completed rest seconds");
        RequireRange(dto.CompletedQuickReps, MaxQuickReps, "Completed quick reps");

        ValidateRating(dto.EffortRating, "Effort rating");
        ValidateRating(dto.RelaxationRating, "Relaxation rating");

        if (dto.Outcome != PelvicFloorSessionOutcome.Completed)
            return;

        if (!dto.CompletedBreathingFinish)
            throw new InvalidOperationException(
                "A completed session must finish with the 5 slow diaphragmatic breaths.");
        if (dto.EffortRating is null || dto.RelaxationRating is null)
            throw new InvalidOperationException(
                "A completed session needs both an effort rating and a relaxation rating between 1 and 5.");

        RequireAtMostPrescribed(dto.CompletedSustainedSets, prescription.SustainedSets, "sustained sets");
        RequireAtMostPrescribed(dto.CompletedSustainedRepsPerSet, prescription.SustainedRepsPerSet, "sustained reps per set");
        RequireAtMostPrescribed(dto.CompletedHoldSeconds, prescription.HoldSeconds, "hold seconds");
        RequireAtMostPrescribed(dto.CompletedRestSeconds, prescription.RestSeconds, "rest seconds");
        RequireAtMostPrescribed(dto.CompletedQuickReps, prescription.TotalQuickReps, "quick reps");
    }

    private static void RequireRange(int value, int max, string label)
    {
        if (value < 0)
            throw new InvalidOperationException($"{label} cannot be negative.");
        if (value > max)
            throw new InvalidOperationException($"{label} cannot be greater than {max}.");
    }

    private static void RequireAtMostPrescribed(int value, int prescribed, string label)
    {
        if (value > prescribed)
            throw new InvalidOperationException(
                $"A completed session cannot record more {label} than this week prescribes ({prescribed}).");
    }

    private PelvicFloorProgramSummaryDto BuildSummary(PelvicFloorProgram program)
    {
        var today = Today();
        var sessions = program.Sessions.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.Id).ToList();
        var checkIns = program.CheckIns.OrderByDescending(c => c.CheckInDate).ThenByDescending(c => c.Id).ToList();

        var currentWeek = PelvicFloorProgramDefinition.GetWeekNumber(program.StartDate, today);

        // Adherence stops accruing once the program is no longer running.
        var through = program.Status switch
        {
            PelvicFloorProgramStatus.Completed => program.CompletedOn ?? today,
            PelvicFloorProgramStatus.Cancelled => program.CancelledOn ?? today,
            _ => today
        };

        var expected = PelvicFloorProgramDefinition.GetExpectedSessionCountThrough(program.StartDate, through);

        var loggedDates = sessions.Select(s => s.SessionDate).ToHashSet();
        var nextScheduled = program.Status == PelvicFloorProgramStatus.Active
            ? PelvicFloorProgramDefinition.GetNextScheduledDate(program.StartDate, today, loggedDates)
            : null;

        var safetyCutoff = today.AddDays(-(SafetyFlagWindowDays - 1));
        var hasRecentSafetyFlag =
            checkIns.Any(c => c.HasPainOrUrinarySymptoms && c.CheckInDate >= safetyCutoff && c.CheckInDate <= today)
            || sessions.Any(s => s.Outcome != PelvicFloorSessionOutcome.Completed
                                 && s.SessionDate >= safetyCutoff && s.SessionDate <= today);

        return new PelvicFloorProgramSummaryDto(
            program.Id,
            program.StartDate,
            program.Status,
            program.CompletedOn,
            program.CancelledOn,
            program.Notes,
            currentWeek,
            sessions.Count(s => s.Outcome == PelvicFloorSessionOutcome.Completed),
            expected,
            sessions.Count(s => s.Outcome == PelvicFloorSessionOutcome.Completed && !s.IsOptional),
            sessions.FirstOrDefault(s => s.EffortRating is not null)?.EffortRating,
            sessions.FirstOrDefault(s => s.RelaxationRating is not null)?.RelaxationRating,
            checkIns.FirstOrDefault(c => c.PelvicTensionRating is not null)?.PelvicTensionRating,
            hasRecentSafetyFlag,
            nextScheduled,
            PelvicFloorProgramDefinition.GetPrescription(currentWeek),
            sessions.Take(RecentSessionCount).Select(ToDto).ToList(),
            checkIns.Take(RecentCheckInCount).Select(ToDto).ToList());
    }

    private static PelvicFloorSessionLogDto ToDto(PelvicFloorSessionLog s) => new(
        s.Id,
        s.SessionDate,
        s.WeekNumber,
        s.IsOptional,
        s.Outcome,
        s.CompletedSustainedSets,
        s.CompletedSustainedRepsPerSet,
        s.CompletedHoldSeconds,
        s.CompletedRestSeconds,
        s.CompletedQuickReps,
        s.CompletedBreathingFinish,
        s.EffortRating,
        s.RelaxationRating,
        s.Notes);

    private static PelvicFloorDailyCheckInDto ToDto(PelvicFloorDailyCheckIn c) => new(
        c.Id,
        c.CheckInDate,
        c.PelvicTensionRating,
        c.HasPainOrUrinarySymptoms,
        c.Notes);
}
