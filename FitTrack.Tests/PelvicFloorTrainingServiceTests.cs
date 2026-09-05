using FitTrack.Application.Abstractions;
using FitTrack.Application.Health;
using FitTrack.Domain.Health;
using FitTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FitTrack.Tests;

public class PelvicFloorTrainingServiceTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task StartAsync_RejectsFutureStartDateAndASecondActiveProgram()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(1), null)));

        var summary = await service.StartAsync(new StartPelvicFloorProgramDto(Today, "  gentle start  "));
        Assert.Equal(PelvicFloorProgramStatus.Active, summary.Status);
        Assert.Equal("gentle start", summary.Notes);
        Assert.Equal(1, summary.CurrentWeekNumber);
        Assert.Equal(1, summary.CurrentPrescription.WeekNumber);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(new StartPelvicFloorProgramDto(Today, null)));

        Assert.Equal(1, await db.PelvicFloorPrograms.CountAsync());
    }

    [Fact]
    public async Task StartAsync_AllowsANewProgramAfterCancellingOrCompleting()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });

        var first = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-5), null));
        Assert.True(await service.CancelAsync(first.Id, Today));

        var second = await service.StartAsync(new StartPelvicFloorProgramDto(Today, null));
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await db.PelvicFloorPrograms.CountAsync());
    }

    [Fact]
    public async Task GetActiveOrMostRecentAsync_IsUserScopedAndFallsBackToTheLastProgram()
    {
        using var db = CreateDb();
        var current = new TestCurrentUserService { UserId = 1 };
        var service = new PelvicFloorTrainingService(db, current);

        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-3), null));

        current.UserId = 2;
        Assert.Null(await service.GetActiveOrMostRecentAsync());

        current.UserId = 1;
        Assert.Equal(program.Id, (await service.GetActiveOrMostRecentAsync())!.Id);

        await service.CancelAsync(program.Id, Today);
        var mostRecent = await service.GetActiveOrMostRecentAsync();
        Assert.Equal(program.Id, mostRecent!.Id);
        Assert.Equal(PelvicFloorProgramStatus.Cancelled, mostRecent.Status);
        Assert.Equal(Today, mostRecent.CancelledOn);
    }

    [Fact]
    public async Task LogSessionAsync_DerivesWeekPrescriptionAndOptionalStatusOnTheServer()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });

        // Day 16 is week 3, offset 2 -> a scheduled day. Day 15 is week 3, offset 1 -> optional.
        var start = Today.AddDays(-16);
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(start, null));

        var scheduled = await service.LogSessionAsync(program.Id, CompletedSession(Today));
        Assert.Equal(3, scheduled.WeekNumber);
        Assert.False(scheduled.IsOptional);
        Assert.Equal(PelvicFloorSessionOutcome.Completed, scheduled.Outcome);

        var optional = await service.LogSessionAsync(program.Id, CompletedSession(Today.AddDays(-1)));
        Assert.Equal(3, optional.WeekNumber);
        Assert.True(optional.IsOptional);

        var summary = await service.GetActiveOrMostRecentAsync();
        Assert.Equal(2, summary!.CompletedSessionCount);
        Assert.Equal(1, summary.CompletedScheduledSessionCount);
        Assert.Equal(3, summary.LatestEffortRating);
        Assert.Equal(4, summary.LatestRelaxationRating);
        Assert.False(summary.HasRecentSafetyFlag);
    }

    [Fact]
    public async Task LogSessionAsync_RejectsDuplicateFutureAndOutOfRangeDates()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var start = Today.AddDays(-16);
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(start, null));

        await service.LogSessionAsync(program.Id, CompletedSession(Today));

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LogSessionAsync(program.Id, CompletedSession(Today)));
        Assert.Contains("already logged", duplicate.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LogSessionAsync(program.Id, CompletedSession(Today.AddDays(1))));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LogSessionAsync(program.Id, CompletedSession(start.AddDays(-1))));

        Assert.Equal(1, await db.PelvicFloorSessionLogs.CountAsync());
    }

    [Fact]
    public async Task LogSessionAsync_RejectsDatesOutsideThe70DayWindow()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var start = Today.AddDays(-80);
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(start, null));

        var outside = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LogSessionAsync(program.Id, CompletedSession(start.AddDays(70))));
        Assert.Contains("10-week plan window", outside.Message);

        var lastDay = await service.LogSessionAsync(program.Id, CompletedSession(start.AddDays(69)));
        Assert.Equal(10, lastDay.WeekNumber);
    }

    [Fact]
    public async Task LogSessionAsync_RejectsCompletedWorkAbovePrescriptionAndMissingFinishers()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today, null));

        // Week 1 prescribes 2 sets x 6 reps, 3 sec hold, 6 sec release, no quick reps.
        var week1 = PelvicFloorProgramDefinition.GetPrescription(1);
        Assert.Equal(2, week1.SustainedSets);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.Completed, 3, 6, 3, 6, 0, true, 3, 4, null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.Completed, 2, 7, 3, 6, 0, true, 3, 4, null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.Completed, 2, 6, 3, 6, 4, true, 3, 4, null)));

        // Breathing acknowledgement and both ratings are required for a completed session.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.Completed, 2, 6, 3, 6, 0, false, 3, 4, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.Completed, 2, 6, 3, 6, 0, true, null, 4, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.Completed, 2, 6, 3, 6, 0, true, 3, 9, null)));

        // Negative and implausible values are rejected regardless of outcome.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.StoppedEarly, -1, 0, 0, 0, 0, false, null, null, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.StoppedEarly, 0, 0, 0, 0, 500, false, null, null, null)));

        Assert.Equal(0, await db.PelvicFloorSessionLogs.CountAsync());

        // A scaled-down completed session is allowed.
        var partial = await service.LogSessionAsync(program.Id,
            new LogPelvicFloorSessionDto(Today, PelvicFloorSessionOutcome.Completed, 1, 4, 3, 6, 0, true, 2, 5, null));
        Assert.Equal(1, partial.CompletedSustainedSets);
        Assert.Equal(4, partial.CompletedSustainedRepsPerSet);
    }

    [Fact]
    public async Task LogSessionAsync_AllowsZeroWorkForNonCompletedOutcomesAndRaisesTheSafetyFlag()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today, null));

        var stopped = await service.LogSessionAsync(program.Id, new LogPelvicFloorSessionDto(
            Today, PelvicFloorSessionOutcome.SkippedForSymptoms, 0, 0, 0, 0, 0, false, null, null, null));

        Assert.Equal(PelvicFloorSessionOutcome.SkippedForSymptoms, stopped.Outcome);
        Assert.Equal(0, stopped.CompletedSustainedSets);
        Assert.Null(stopped.Notes);

        var summary = await service.GetActiveOrMostRecentAsync();
        Assert.Equal(0, summary!.CompletedSessionCount);
        Assert.True(summary.HasRecentSafetyFlag);
    }

    [Fact]
    public async Task UpsertCheckInAsync_KeepsExactlyOneRowPerProgramAndDate()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-2), null));

        var created = await service.UpsertCheckInAsync(program.Id,
            new UpsertPelvicFloorCheckInDto(Today, 2, false, "fine"));
        var updated = await service.UpsertCheckInAsync(program.Id,
            new UpsertPelvicFloorCheckInDto(Today, 5, true, "tight"));

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(5, updated.PelvicTensionRating);
        Assert.True(updated.HasPainOrUrinarySymptoms);
        Assert.Equal(1, await db.PelvicFloorDailyCheckIns.CountAsync());

        // An earlier in-program date creates a second row.
        await service.UpsertCheckInAsync(program.Id, new UpsertPelvicFloorCheckInDto(Today.AddDays(-2), 3, false, null));
        Assert.Equal(2, await db.PelvicFloorDailyCheckIns.CountAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertCheckInAsync(program.Id,
            new UpsertPelvicFloorCheckInDto(Today.AddDays(1), 3, false, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertCheckInAsync(program.Id,
            new UpsertPelvicFloorCheckInDto(Today.AddDays(-3), 3, false, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertCheckInAsync(program.Id,
            new UpsertPelvicFloorCheckInDto(Today, 6, false, null)));

        var summary = await service.GetActiveOrMostRecentAsync();
        Assert.Equal(5, summary!.LatestPelvicTensionRating);
        Assert.True(summary.HasRecentSafetyFlag);
    }

    [Fact]
    public async Task CheckInsAreAcceptedForAnyDateFromTheStartOnwardsBeyondThePlanWindow()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-80), null));

        var checkIn = await service.UpsertCheckInAsync(program.Id,
            new UpsertPelvicFloorCheckInDto(Today, 3, false, null));

        Assert.Equal(Today, checkIn.CheckInDate);
    }

    [Fact]
    public async Task CompleteAsync_IsRejectedBeforeDay70AndSucceedsOnOrAfterIt()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });

        var tooEarly = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-69), null));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(tooEarly.Id, Today));
        Assert.Contains("day 70", error.Message);
        await service.CancelAsync(tooEarly.Id, Today);

        var eligible = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-70), null));
        Assert.True(await service.CompleteAsync(eligible.Id, Today));

        var summary = await service.GetActiveOrMostRecentAsync();
        Assert.Equal(eligible.Id, summary!.Id);
        Assert.Equal(PelvicFloorProgramStatus.Completed, summary.Status);
        Assert.Equal(Today, summary.CompletedOn);

        // Completing twice is not allowed, and an unknown program is a plain miss.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(eligible.Id, Today));
        Assert.False(await service.CompleteAsync(9999, Today));
    }

    [Fact]
    public async Task CancelAsync_KeepsHistoryVisibleInTheMostRecentSummary()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-16), null));

        await service.LogSessionAsync(program.Id, CompletedSession(Today));
        await service.UpsertCheckInAsync(program.Id, new UpsertPelvicFloorCheckInDto(Today, 2, false, null));
        Assert.True(await service.CancelAsync(program.Id, Today));

        var summary = await service.GetActiveOrMostRecentAsync();
        Assert.Equal(PelvicFloorProgramStatus.Cancelled, summary!.Status);
        Assert.Single(summary.RecentSessions);
        Assert.Single(summary.RecentCheckIns);
        Assert.Null(summary.NextScheduledSessionDate);

        // No further writes against a finished program.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LogSessionAsync(program.Id, CompletedSession(Today.AddDays(-1))));
    }

    [Fact]
    public async Task DeletingASessionOrCheckInUpdatesTheSummaryTotals()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });
        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-16), null));

        var session = await service.LogSessionAsync(program.Id, CompletedSession(Today));
        await service.LogSessionAsync(program.Id, CompletedSession(Today.AddDays(-1)));
        var checkIn = await service.UpsertCheckInAsync(program.Id, new UpsertPelvicFloorCheckInDto(Today, 2, false, null));

        Assert.Equal(2, (await service.GetActiveOrMostRecentAsync())!.CompletedSessionCount);

        Assert.True(await service.DeleteSessionAsync(program.Id, session.Id));
        Assert.True(await service.DeleteCheckInAsync(program.Id, checkIn.Id));
        Assert.False(await service.DeleteSessionAsync(program.Id, session.Id));
        Assert.False(await service.DeleteCheckInAsync(program.Id, checkIn.Id));

        var summary = await service.GetActiveOrMostRecentAsync();
        Assert.Equal(1, summary!.CompletedSessionCount);
        Assert.Empty(summary.RecentCheckIns);
    }

    [Fact]
    public async Task EveryMutationIsScopedToTheOwningUser()
    {
        using var db = CreateDb();
        var current = new TestCurrentUserService { UserId = 1 };
        var service = new PelvicFloorTrainingService(db, current);

        var program = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-16), null));
        var session = await service.LogSessionAsync(program.Id, CompletedSession(Today));
        var checkIn = await service.UpsertCheckInAsync(program.Id, new UpsertPelvicFloorCheckInDto(Today, 2, false, null));

        current.UserId = 2;

        Assert.Null(await service.GetActiveOrMostRecentAsync());
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.LogSessionAsync(program.Id, CompletedSession(Today.AddDays(-1))));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.UpsertCheckInAsync(program.Id, new UpsertPelvicFloorCheckInDto(Today, 1, false, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CompleteAsync(program.Id, Today));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CancelAsync(program.Id, Today));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteSessionAsync(program.Id, session.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteCheckInAsync(program.Id, checkIn.Id));

        Assert.Equal(1, await db.PelvicFloorSessionLogs.CountAsync());
        Assert.Equal(1, await db.PelvicFloorDailyCheckIns.CountAsync());
    }

    [Fact]
    public async Task ChildRowsFromAnotherProgramAreNotDeletable()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });

        var first = await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-16), null));
        var session = await service.LogSessionAsync(first.Id, CompletedSession(Today));
        await service.CancelAsync(first.Id, Today);

        var second = await service.StartAsync(new StartPelvicFloorProgramDto(Today, null));

        Assert.False(await service.DeleteSessionAsync(second.Id, session.Id));
        Assert.Equal(1, await db.PelvicFloorSessionLogs.CountAsync());
    }

    [Fact]
    public async Task ExpectedSessionCountFollowsThePlanCadence()
    {
        using var db = CreateDb();
        var service = new PelvicFloorTrainingService(db, new TestCurrentUserService { UserId = 1 });

        // Day 16 of the program: weeks 1 and 2 are complete (6 target days) plus offsets 0 and 2 of week 3.
        await service.StartAsync(new StartPelvicFloorProgramDto(Today.AddDays(-16), null));
        var summary = await service.GetActiveOrMostRecentAsync();

        Assert.Equal(3, summary!.CurrentWeekNumber);
        Assert.Equal(8, summary.ExpectedSessionCountThroughToday);
        Assert.Equal(Today, summary.NextScheduledSessionDate);
    }

    private static LogPelvicFloorSessionDto CompletedSession(DateOnly date)
    {
        var week = PelvicFloorProgramDefinition.GetWeekNumber(Today.AddDays(-16), date);
        var p = PelvicFloorProgramDefinition.GetPrescription(week);
        return new LogPelvicFloorSessionDto(
            date,
            PelvicFloorSessionOutcome.Completed,
            p.SustainedSets,
            p.SustainedRepsPerSet,
            p.HoldSeconds,
            p.RestSeconds,
            p.TotalQuickReps,
            true,
            3,
            4,
            null);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public int? UserId { get; set; }
        public bool IsAdmin { get; set; }
        public int RequireUserId() => UserId ?? throw new NotAuthenticatedException();
    }
}
