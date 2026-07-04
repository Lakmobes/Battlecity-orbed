using BattleCity.Server;

using Xunit;

namespace BattleCity.Core.Tests;

public class InterviewCommsTests
{
    [Fact]
    public void CitySlot_DenyApplicants_DefaultsFalse()
    {
        var slot = new CitySlot(0);

        Assert.False(slot.DenyApplicants);
    }

    [Fact]
    public void CitySlot_DenyApplicants_CanBeSet()
    {
        var slot = new CitySlot(0);
        slot.DenyApplicants = true;

        Assert.True(slot.DenyApplicants);
    }
}
