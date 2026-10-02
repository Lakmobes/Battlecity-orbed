using BattleCity.Server;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class CityDestructScheduleTests
{
    [Fact]
    public void Tick_ReturnsCityWhenTimerElapses_AndCancelStopsIt()
    {
        var schedule = new CityDestructSchedule();
        schedule.Schedule(4, 120f);

        Assert.Empty(schedule.Tick(119f));
        Assert.True(schedule.IsScheduled(4));

        var due = schedule.Tick(1f);
        Assert.Equal(new byte[] { 4 }, due);
        Assert.False(schedule.IsScheduled(4));
    }

    [Fact]
    public void Cancel_RemovesPendingCity()
    {
        var schedule = new CityDestructSchedule();
        schedule.Schedule(8, 10f);
        schedule.Cancel(8);

        Assert.Empty(schedule.Tick(30f));
    }
}
