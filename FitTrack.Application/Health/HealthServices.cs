using FitTrack.Application.Abstractions;
using FitTrack.Domain.Health;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Health;

public interface IBodyMeasurementService
{
    Task<List<BodyMeasurementDto>> ListAsync(CancellationToken ct = default);
    Task<BodyMeasurementDto> CreateAsync(UpsertBodyMeasurementDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertBodyMeasurementDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class BodyMeasurementService : IBodyMeasurementService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public BodyMeasurementService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<BodyMeasurementDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.BodyMeasurements.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.MeasuredOn)
            .Select(b => new BodyMeasurementDto(b.Id, b.MeasuredOn, b.WeightKg, b.BodyFatPercent, b.MusclePercent, b.MuscleKg, b.Notes))
            .ToListAsync(ct);
    }

    public async Task<BodyMeasurementDto> CreateAsync(UpsertBodyMeasurementDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = new BodyMeasurement
        {
            UserId = userId,
            MeasuredOn = dto.MeasuredOn,
            WeightKg = dto.WeightKg,
            BodyFatPercent = dto.BodyFatPercent,
            MusclePercent = dto.MusclePercent,
            MuscleKg = dto.MuscleKg,
            Notes = dto.Notes
        };
        _db.BodyMeasurements.Add(b);
        await _db.SaveChangesAsync(ct);
        return new BodyMeasurementDto(b.Id, b.MeasuredOn, b.WeightKg, b.BodyFatPercent, b.MusclePercent, b.MuscleKg, b.Notes);
    }

    public async Task<bool> UpdateAsync(int id, UpsertBodyMeasurementDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = await _db.BodyMeasurements.FindAsync(new object?[] { id }, ct);
        if (b is null) return false;
        if (b.UserId != userId) throw new ForbiddenException("You do not own this measurement.");
        b.MeasuredOn = dto.MeasuredOn;
        b.WeightKg = dto.WeightKg;
        b.BodyFatPercent = dto.BodyFatPercent;
        b.MusclePercent = dto.MusclePercent;
        b.MuscleKg = dto.MuscleKg;
        b.Notes = dto.Notes;
        b.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = await _db.BodyMeasurements.FindAsync(new object?[] { id }, ct);
        if (b is null) return false;
        if (b.UserId != userId) throw new ForbiddenException("You do not own this measurement.");
        _db.BodyMeasurements.Remove(b);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public interface IBodyPartMeasurementService
{
    Task<List<BodyPartMeasurementDto>> ListAsync(BodyPart? part, CancellationToken ct = default);
    Task<BodyPartMeasurementDto> CreateAsync(UpsertBodyPartMeasurementDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertBodyPartMeasurementDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class BodyPartMeasurementService : IBodyPartMeasurementService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public BodyPartMeasurementService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<BodyPartMeasurementDto>> ListAsync(BodyPart? part, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var q = _db.BodyPartMeasurements.AsNoTracking().Where(b => b.UserId == userId);
        if (part.HasValue) q = q.Where(b => b.BodyPart == part.Value);
        return await q.OrderByDescending(b => b.MeasuredOn).ThenBy(b => b.BodyPart)
            .Select(b => new BodyPartMeasurementDto(b.Id, b.MeasuredOn, b.BodyPart, b.ValueCm, b.Notes))
            .ToListAsync(ct);
    }

    public async Task<BodyPartMeasurementDto> CreateAsync(UpsertBodyPartMeasurementDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = new BodyPartMeasurement
        {
            UserId = userId,
            MeasuredOn = dto.MeasuredOn,
            BodyPart = dto.BodyPart,
            ValueCm = dto.ValueCm,
            Notes = dto.Notes
        };
        _db.BodyPartMeasurements.Add(b);
        await _db.SaveChangesAsync(ct);
        return new BodyPartMeasurementDto(b.Id, b.MeasuredOn, b.BodyPart, b.ValueCm, b.Notes);
    }

    public async Task<bool> UpdateAsync(int id, UpsertBodyPartMeasurementDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = await _db.BodyPartMeasurements.FindAsync(new object?[] { id }, ct);
        if (b is null) return false;
        if (b.UserId != userId) throw new ForbiddenException("You do not own this measurement.");
        b.MeasuredOn = dto.MeasuredOn;
        b.BodyPart = dto.BodyPart;
        b.ValueCm = dto.ValueCm;
        b.Notes = dto.Notes;
        b.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = await _db.BodyPartMeasurements.FindAsync(new object?[] { id }, ct);
        if (b is null) return false;
        if (b.UserId != userId) throw new ForbiddenException("You do not own this measurement.");
        _db.BodyPartMeasurements.Remove(b);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public interface IBloodPressureService
{
    Task<List<BloodPressureDto>> ListAsync(CancellationToken ct = default);
    Task<BloodPressureDto> CreateAsync(UpsertBloodPressureDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertBloodPressureDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class BloodPressureService : IBloodPressureService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public BloodPressureService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<BloodPressureDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.BloodPressureReadings.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.MeasuredAt)
            .Select(b => new BloodPressureDto(b.Id, b.MeasuredAt, b.Systolic, b.Diastolic, b.Pulse, b.Notes))
            .ToListAsync(ct);
    }

    public async Task<BloodPressureDto> CreateAsync(UpsertBloodPressureDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = new BloodPressureReading
        {
            UserId = userId,
            MeasuredAt = dto.MeasuredAt,
            Systolic = dto.Systolic,
            Diastolic = dto.Diastolic,
            Pulse = dto.Pulse,
            Notes = dto.Notes
        };
        _db.BloodPressureReadings.Add(b);
        await _db.SaveChangesAsync(ct);
        return new BloodPressureDto(b.Id, b.MeasuredAt, b.Systolic, b.Diastolic, b.Pulse, b.Notes);
    }

    public async Task<bool> UpdateAsync(int id, UpsertBloodPressureDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = await _db.BloodPressureReadings.FindAsync(new object?[] { id }, ct);
        if (b is null) return false;
        if (b.UserId != userId) throw new ForbiddenException("You do not own this reading.");
        b.MeasuredAt = dto.MeasuredAt;
        b.Systolic = dto.Systolic;
        b.Diastolic = dto.Diastolic;
        b.Pulse = dto.Pulse;
        b.Notes = dto.Notes;
        b.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var b = await _db.BloodPressureReadings.FindAsync(new object?[] { id }, ct);
        if (b is null) return false;
        if (b.UserId != userId) throw new ForbiddenException("You do not own this reading.");
        _db.BloodPressureReadings.Remove(b);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public interface IColdEpisodeService
{
    Task<List<ColdEpisodeDto>> ListAsync(int? year, CancellationToken ct = default);
    Task<ColdEpisodeDto> CreateAsync(UpsertColdEpisodeDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertColdEpisodeDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class ColdEpisodeService : IColdEpisodeService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public ColdEpisodeService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<ColdEpisodeDto>> ListAsync(int? year, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var q = _db.ColdEpisodes.AsNoTracking().Where(c => c.UserId == userId);
        if (year.HasValue)
        {
            var from = new DateOnly(year.Value, 1, 1);
            var to = new DateOnly(year.Value, 12, 31);
            q = q.Where(c => c.StartDate >= from && c.StartDate <= to);
        }
        return await q.OrderByDescending(c => c.StartDate)
            .Select(c => new ColdEpisodeDto(c.Id, c.StartDate, c.EndDate, c.Severity, c.Symptoms, c.Notes))
            .ToListAsync(ct);
    }

    public async Task<ColdEpisodeDto> CreateAsync(UpsertColdEpisodeDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var c = new ColdEpisode
        {
            UserId = userId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Severity = dto.Severity,
            Symptoms = dto.Symptoms,
            Notes = dto.Notes
        };
        _db.ColdEpisodes.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ColdEpisodeDto(c.Id, c.StartDate, c.EndDate, c.Severity, c.Symptoms, c.Notes);
    }

    public async Task<bool> UpdateAsync(int id, UpsertColdEpisodeDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var c = await _db.ColdEpisodes.FindAsync(new object?[] { id }, ct);
        if (c is null) return false;
        if (c.UserId != userId) throw new ForbiddenException("You do not own this cold episode.");
        c.StartDate = dto.StartDate;
        c.EndDate = dto.EndDate;
        c.Severity = dto.Severity;
        c.Symptoms = dto.Symptoms;
        c.Notes = dto.Notes;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var c = await _db.ColdEpisodes.FindAsync(new object?[] { id }, ct);
        if (c is null) return false;
        if (c.UserId != userId) throw new ForbiddenException("You do not own this cold episode.");
        _db.ColdEpisodes.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public interface ICardioSessionService
{
    Task<List<CardioSessionDto>> ListAsync(CancellationToken ct = default);
    Task<CardioSessionDto> CreateAsync(UpsertCardioSessionDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertCardioSessionDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class CardioSessionService : ICardioSessionService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public CardioSessionService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<CardioSessionDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.CardioSessions.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.SessionDate)
            .Select(c => new CardioSessionDto(
                c.Id, c.SessionDate, c.DurationMinutes, c.Calories,
                c.Watts, c.MaxSpeedKph, c.MaxHeartRate, c.Notes))
            .ToListAsync(ct);
    }

    public async Task<CardioSessionDto> CreateAsync(UpsertCardioSessionDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var c = new Domain.Health.CardioSession
        {
            UserId = userId,
            SessionDate = dto.SessionDate,
            DurationMinutes = dto.DurationMinutes,
            Calories = dto.Calories,
            Watts = dto.Watts,
            MaxSpeedKph = dto.MaxSpeedKph,
            MaxHeartRate = dto.MaxHeartRate,
            Notes = dto.Notes
        };
        _db.CardioSessions.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CardioSessionDto(c.Id, c.SessionDate, c.DurationMinutes, c.Calories,
            c.Watts, c.MaxSpeedKph, c.MaxHeartRate, c.Notes);
    }

    public async Task<bool> UpdateAsync(int id, UpsertCardioSessionDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var c = await _db.CardioSessions.FindAsync(new object?[] { id }, ct);
        if (c is null) return false;
        if (c.UserId != userId) throw new ForbiddenException("You do not own this cardio session.");
        c.SessionDate = dto.SessionDate;
        c.DurationMinutes = dto.DurationMinutes;
        c.Calories = dto.Calories;
        c.Watts = dto.Watts;
        c.MaxSpeedKph = dto.MaxSpeedKph;
        c.MaxHeartRate = dto.MaxHeartRate;
        c.Notes = dto.Notes;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var c = await _db.CardioSessions.FindAsync(new object?[] { id }, ct);
        if (c is null) return false;
        if (c.UserId != userId) throw new ForbiddenException("You do not own this cardio session.");
        _db.CardioSessions.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
