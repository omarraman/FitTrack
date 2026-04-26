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
    int Id, DateTimeOffset MeasuredAt, int Systolic, int Diastolic, int? Pulse, string? Notes);

public record UpsertBloodPressureDto(
    DateTimeOffset MeasuredAt, int Systolic, int Diastolic, int? Pulse, string? Notes);

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
