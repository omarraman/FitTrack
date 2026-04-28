using FitTrack.Domain.Health;

namespace FitTrack.Application.Health;

public record BodyMeasurementDto(
    int Id, DateOnly MeasuredOn, decimal WeightKg,
    decimal? BodyFatPercent, decimal? MusclePercent, decimal? MuscleKg, string? Notes);

public record UpsertBodyMeasurementDto(
    DateOnly MeasuredOn, decimal WeightKg,
    decimal? BodyFatPercent, decimal? MusclePercent, decimal? MuscleKg, string? Notes);

public record BodyPartMeasurementDto(
    int Id, DateOnly MeasuredOn, BodyPart BodyPart, decimal ValueCm, string? Notes);

public record UpsertBodyPartMeasurementDto(
    DateOnly MeasuredOn, BodyPart BodyPart, decimal ValueCm, string? Notes);

public record BloodPressureDto(
    int Id, DateTimeOffset MeasuredAt, BpSessionType? SessionType,
    int Systolic, int Diastolic, int? Pulse, string? Notes);

/// <summary>One raw reading within a 5-reading BP session.</summary>
public record BpReadingInput(int Systolic, int Diastolic, int? Pulse);

/// <summary>
/// Submit a morning or evening session of up to 5 readings.
/// The service computes the rounded average and stores a single row.
/// </summary>
public record LogBpSessionDto(
    DateOnly MeasuredOn,
    BpSessionType SessionType,
    List<BpReadingInput> Readings,
    string? Notes);

public record ColdEpisodeDto(
    int Id, DateOnly StartDate, DateOnly? EndDate,
    ColdSeverity Severity, string? Symptoms, string? Notes);

public record UpsertColdEpisodeDto(
    DateOnly StartDate, DateOnly? EndDate,
    ColdSeverity Severity, string? Symptoms, string? Notes);

public record CardioSessionDto(
    int Id,
    DateOnly SessionDate,
    int DurationMinutes,
    int Calories,
    int Watts,
    decimal MaxSpeedKph,
    int? MaxHeartRate,
    string? Notes);

public record UpsertCardioSessionDto(
    DateOnly SessionDate,
    int DurationMinutes,
    int Calories,
    int Watts,
    decimal MaxSpeedKph,
    int? MaxHeartRate,
    string? Notes);