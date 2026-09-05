using FitTrack.Application.Health;
using Xunit;

namespace FitTrack.Tests;

public class PelvicFloorProgramDefinitionTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);

    [Fact]
    public void GetPrescription_ReturnsTheExactPlanValues()
    {
        Assert.Equal(
            new PelvicFloorPrescriptionDto(1, "Learn control", 3, 2, 6, 3, 6, 60, 0, 0, 0, 0, 5, false),
            PelvicFloorProgramDefinition.GetPrescription(1));

        Assert.Equal(
            new PelvicFloorPrescriptionDto(3, "Basic endurance", 3, 2, 8, 5, 7, 60, 1, 5, 1, 3, 5, false),
            PelvicFloorProgramDefinition.GetPrescription(3));

        Assert.Equal(
            new PelvicFloorPrescriptionDto(5, "Capacity", 3, 3, 8, 7, 9, 75, 1, 8, 1, 3, 5, false),
            PelvicFloorProgramDefinition.GetPrescription(5));

        Assert.Equal(
            new PelvicFloorPrescriptionDto(8, "Deload and assess", 2, 2, 6, 4, 8, 60, 0, 0, 0, 0, 5, true),
            PelvicFloorProgramDefinition.GetPrescription(8));

        Assert.Equal(
            new PelvicFloorPrescriptionDto(9, "Consolidate", 3, 3, 8, 7, 9, 75, 1, 8, 1, 3, 5, false),
            PelvicFloorProgramDefinition.GetPrescription(9));
    }

    [Fact]
    public void GetPrescription_ExposesDerivedQuickWork()
    {
        var week1 = PelvicFloorProgramDefinition.GetPrescription(1);
        Assert.Equal(0, week1.TotalQuickReps);
        Assert.False(week1.HasQuickContractions);

        var week5 = PelvicFloorProgramDefinition.GetPrescription(5);
        Assert.Equal(8, week5.TotalQuickReps);
        Assert.True(week5.HasQuickContractions);

        Assert.False(PelvicFloorProgramDefinition.GetPrescription(8).HasQuickContractions);
        Assert.True(PelvicFloorProgramDefinition.GetPrescription(8).IsDeloadWeek);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public void GetPrescription_RejectsWeeksOutsideThePlan(int weekNumber)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PelvicFloorProgramDefinition.GetPrescription(weekNumber));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(49, 8)]
    [InlineData(69, 10)]
    [InlineData(-3, 1)]
    [InlineData(200, 10)]
    public void GetWeekNumber_UsesSevenDayBlocksClampedToThePlan(int dayOffset, int expectedWeek)
        => Assert.Equal(expectedWeek, PelvicFloorProgramDefinition.GetWeekNumber(Start, Start.AddDays(dayOffset)));

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public void IsScheduledDate_UsesOffsets0_2_4_InAThreeSessionWeek(int dayOffset, bool scheduled)
        => Assert.Equal(scheduled, PelvicFloorProgramDefinition.IsScheduledDate(Start, Start.AddDays(dayOffset)));

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    [InlineData(51, false)]
    [InlineData(52, true)]
    [InlineData(53, false)]
    public void IsScheduledDate_UsesOffsets0_3_InTheDeloadWeek(int dayOffset, bool scheduled)
    {
        Assert.Equal(8, PelvicFloorProgramDefinition.GetWeekNumber(Start, Start.AddDays(dayOffset)));
        Assert.Equal(scheduled, PelvicFloorProgramDefinition.IsScheduledDate(Start, Start.AddDays(dayOffset)));
    }

    [Fact]
    public void GetScheduledOffsets_MatchesTheSessionsPerWeekTarget()
    {
        Assert.Equal(new[] { 0, 2, 4 }, PelvicFloorProgramDefinition.GetScheduledOffsets(1));
        Assert.Equal(new[] { 0, 3 }, PelvicFloorProgramDefinition.GetScheduledOffsets(8));

        for (var week = 1; week <= PelvicFloorProgramDefinition.TotalWeeks; week++)
        {
            Assert.Equal(
                PelvicFloorProgramDefinition.GetPrescription(week).SessionsPerWeek,
                PelvicFloorProgramDefinition.GetScheduledOffsets(week).Count);
        }
    }

    [Fact]
    public void IsWithinProgram_CoversDayZeroThroughDay69()
    {
        Assert.False(PelvicFloorProgramDefinition.IsWithinProgram(Start, Start.AddDays(-1)));
        Assert.True(PelvicFloorProgramDefinition.IsWithinProgram(Start, Start));
        Assert.True(PelvicFloorProgramDefinition.IsWithinProgram(Start, Start.AddDays(69)));
        Assert.False(PelvicFloorProgramDefinition.IsWithinProgram(Start, Start.AddDays(70)));
    }

    [Fact]
    public void GetExpectedSessionCountThrough_CountsScheduledDaysOnly()
    {
        Assert.Equal(0, PelvicFloorProgramDefinition.GetExpectedSessionCountThrough(Start, Start.AddDays(-1)));
        Assert.Equal(1, PelvicFloorProgramDefinition.GetExpectedSessionCountThrough(Start, Start));
        Assert.Equal(3, PelvicFloorProgramDefinition.GetExpectedSessionCountThrough(Start, Start.AddDays(6)));
        Assert.Equal(6, PelvicFloorProgramDefinition.GetExpectedSessionCountThrough(Start, Start.AddDays(13)));

        // 7 three-session weeks + the two-session deload + two more three-session weeks.
        Assert.Equal(29, PelvicFloorProgramDefinition.GetExpectedSessionCountThrough(Start, Start.AddDays(69)));
        Assert.Equal(29, PelvicFloorProgramDefinition.GetExpectedSessionCountThrough(Start, Start.AddDays(365)));
        Assert.Equal(29, PelvicFloorProgramDefinition.GetScheduledDates(Start).Count);
    }

    [Fact]
    public void GetNextScheduledDate_SkipsDaysThatAlreadyHaveASession()
    {
        var empty = new HashSet<DateOnly>();
        Assert.Equal(Start, PelvicFloorProgramDefinition.GetNextScheduledDate(Start, Start, empty));
        Assert.Equal(Start.AddDays(2), PelvicFloorProgramDefinition.GetNextScheduledDate(Start, Start.AddDays(1), empty));

        var logged = new HashSet<DateOnly> { Start, Start.AddDays(2) };
        Assert.Equal(Start.AddDays(4), PelvicFloorProgramDefinition.GetNextScheduledDate(Start, Start, logged));

        Assert.Null(PelvicFloorProgramDefinition.GetNextScheduledDate(Start, Start.AddDays(70), empty));
    }
}
