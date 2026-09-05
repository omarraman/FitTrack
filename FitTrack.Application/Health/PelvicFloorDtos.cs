using FitTrack.Domain.Health;

namespace FitTrack.Application.Health;

public record PelvicFloorPrescriptionDto(
    int WeekNumber,
    string PhaseName,
    int SessionsPerWeek,
    int SustainedSets,
    int SustainedRepsPerSet,
    int HoldSeconds,
    int RestSeconds,
    int RestBetweenSetsSeconds,
    int QuickSets,
    int QuickReps,
    int QuickHoldSeconds,
    int QuickRestSeconds,
    int BreathingBreaths,
    bool IsDeloadWeek)
{
    public int TotalQuickReps => QuickSets * QuickReps;
    public bool HasQuickContractions => TotalQuickReps > 0;
}

public record PelvicFloorSessionLogDto(
    int Id,
    DateOnly SessionDate,
    int WeekNumber,
    bool IsOptional,
    PelvicFloorSessionOutcome Outcome,
    int CompletedSustainedSets,
    int CompletedSustainedRepsPerSet,
    int CompletedHoldSeconds,
    int CompletedRestSeconds,
    int CompletedQuickReps,
    bool CompletedBreathingFinish,
    int? EffortRating,
    int? RelaxationRating,
    string? Notes);

public record PelvicFloorDailyCheckInDto(
    int Id,
    DateOnly CheckInDate,
    int? PelvicTensionRating,
    bool HasPainOrUrinarySymptoms,
    string? Notes);

public record PelvicFloorProgramSummaryDto(
    int Id,
    DateOnly StartDate,
    PelvicFloorProgramStatus Status,
    DateOnly? CompletedOn,
    DateOnly? CancelledOn,
    string? Notes,
    int CurrentWeekNumber,
    int CompletedSessionCount,
    int ExpectedSessionCountThroughToday,
    int CompletedScheduledSessionCount,
    int? LatestEffortRating,
    int? LatestRelaxationRating,
    int? LatestPelvicTensionRating,
    bool HasRecentSafetyFlag,
    DateOnly? NextScheduledSessionDate,
    PelvicFloorPrescriptionDto CurrentPrescription,
    List<PelvicFloorSessionLogDto> RecentSessions,
    List<PelvicFloorDailyCheckInDto> RecentCheckIns);

public record StartPelvicFloorProgramDto(DateOnly StartDate, string? Notes);

public record LogPelvicFloorSessionDto(
    DateOnly SessionDate,
    PelvicFloorSessionOutcome Outcome,
    int CompletedSustainedSets,
    int CompletedSustainedRepsPerSet,
    int CompletedHoldSeconds,
    int CompletedRestSeconds,
    int CompletedQuickReps,
    bool CompletedBreathingFinish,
    int? EffortRating,
    int? RelaxationRating,
    string? Notes);

public record UpsertPelvicFloorCheckInDto(
    DateOnly CheckInDate,
    int? PelvicTensionRating,
    bool HasPainOrUrinarySymptoms,
    string? Notes);

/// <summary>Small explicit request record for the complete/cancel endpoints.</summary>
public record PelvicFloorProgramDateDto(DateOnly Date);

/// <summary>One row of the fixed ten-week plan overview.</summary>
public record PelvicFloorPhaseDto(
    string PhaseName,
    int StartWeek,
    int EndWeek,
    int SessionsPerWeek,
    string Focus);
