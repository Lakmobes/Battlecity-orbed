using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

using Xunit;

namespace BattleCity.Shared.Tests;

public class CatalogTests
{
    [Fact]
    public void CityCatalogHasSixtyFourEntries()
    {
        Assert.Equal(GameConstants.MaxCities, CityCatalog.Names.Count);
        Assert.Equal("Balkh", CityCatalog.Names[0]);
        Assert.Equal("Admin Inn", CityCatalog.Names[^1]);
    }

    [Fact]
    public void ItemCatalogHasTwelveEntries()
    {
        Assert.Equal(12, ItemCatalog.Names.Count);
        Assert.Equal(12, ItemCatalog.MaxCarryCount.Count);
        Assert.Equal("Laser", ItemCatalog.Names[0]);
        Assert.Equal(20, ItemCatalog.MaxCarryCount[(int)Data.ItemType.Bomb]);
        Assert.Equal(1, ItemCatalog.MaxCarryCount[(int)Data.ItemType.Orb]);
    }

    [Fact]
    public void BuildingCatalogMenuArraysAreSameLength()
    {
        Assert.Equal(26, BuildingCatalog.MenuTypeCodes.Count);
        Assert.Equal(BuildingCatalog.MenuTypeCodes.Count, BuildingCatalog.MenuNames.Count);
        Assert.Equal(BuildingCatalog.MenuTypeCodes.Count, BuildingCatalog.MenuIconIndices.Count);
    }

    [Fact]
    public void BuildingCatalogMatchesLegacyStructsCppTypeCodes()
    {
        int[] expected =
        [
            200, 300, 400, 100, 409, 109, 403, 103, 402, 102, 411, 111,
            404, 104, 405, 105, 401, 101, 410, 110, 408, 108, 407, 107, 406, 106,
        ];

        Assert.Equal(expected, BuildingCatalog.MenuTypeCodes);
    }

    [Fact]
    public void FactoryProductsMatchLegacyServerItemTypes()
    {
        int[] expected =
        [
            1, 9, 0, 2, 11, 4, 5, 3, 10, 8, 7, 6,
        ];

        var actual = BuildingCatalog.FactoryProducts.Select(item => (int)item).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildTreeMatchesLegacyServerBuildTree()
    {
        int[] expected = [-1, -1, 0, 0, 1, 1, 2, 2, 4, 4, 5, 6];
        Assert.Equal(expected, BuildingCatalog.BuildTreePrerequisites);
    }

    [Theory]
    [InlineData(200, true, false, false, false)]
    [InlineData(300, false, true, false, false)]
    [InlineData(401, false, false, true, false)]
    [InlineData(101, false, false, false, true)]
    public void BuildingTypeCodeClassification(
        int code,
        bool hospital,
        bool house,
        bool research,
        bool factory)
    {
        Assert.Equal(hospital, BuildingCatalog.IsHospital(code));
        Assert.Equal(house, BuildingCatalog.IsHouse(code));
        Assert.Equal(research, BuildingCatalog.IsResearch(code));
        Assert.Equal(factory, BuildingCatalog.IsFactory(code));
    }
}
