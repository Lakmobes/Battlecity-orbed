using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Components;

public struct PlayerInventory
{
    /// <summary>Placeables cycled / dropped with D (and selected via [ ] among stocked slots).</summary>
    public static readonly ItemType[] SelectableItems =
    [
        ItemType.MedKit,
        ItemType.Wall,
        ItemType.Turret,
        ItemType.Sleeper,
        ItemType.Plasma,
        ItemType.Mine,
        ItemType.Bomb,
        ItemType.Orb,
        ItemType.Dfg,
    ];

    /// <summary>Full HUD bar: gear first, then placeables (empty slots stay visible).</summary>
    public static readonly ItemType[] HudItems =
    [
        ItemType.Cloak,
        ItemType.Rocket,
        ItemType.Flare,
        ItemType.MedKit,
        ItemType.Wall,
        ItemType.Turret,
        ItemType.Sleeper,
        ItemType.Plasma,
        ItemType.Mine,
        ItemType.Bomb,
        ItemType.Orb,
        ItemType.Dfg,
    ];

    public int Cloak;
    public int Rocket;
    public int MedKit;
    public int Bomb;
    public int Mine;
    public int Orb;
    public int Flare;
    public int Dfg;
    public int Wall;
    public int Turret;
    public int Sleeper;
    public int Plasma;
    public ItemType SelectedItemType;

    /// <summary>
    /// Laser is always free. Rocket / cloak / flare are granted only while the matching
    /// city factory still stands (<c>CanBuild[factory]==2</c>), matching the intended remake spawn rule.
    /// </summary>
    public static PlayerInventory CreateLoadoutForCity(CityBuildState? build)
    {
        var inventory = new PlayerInventory { SelectedItemType = ItemType.Rocket };
        if (build is null)
        {
            return inventory;
        }

        if (HasBuiltFactory(build, treeIndex: 0))
        {
            inventory.Rocket = 1;
        }

        if (HasBuiltFactory(build, EconomyConstants.CloakResearchTreeIndex))
        {
            inventory.Cloak = 1;
        }

        if (HasBuiltFactory(build, EconomyConstants.FlareResearchTreeIndex))
        {
            inventory.Flare = 1;
        }

        if (inventory.Rocket <= 0)
        {
            inventory.SelectedItemType = ItemType.Cloak;
        }

        return inventory;
    }

    /// <summary>Empty gear (laser only). Prefer <see cref="CreateLoadoutForCity"/> online/offline with a city.</summary>
    public static PlayerInventory CreateStarterLoadout() => CreateLoadoutForCity(null);

    /// <summary>Obsolete name kept for call sites; same as <see cref="CreateStarterLoadout"/>.</summary>
    public static PlayerInventory CreateDemoLoadout() => CreateStarterLoadout();

    private static bool HasBuiltFactory(CityBuildState build, int treeIndex)
    {
        var factoryMenu = BuildingCatalog.GetFactoryMenuIndex(treeIndex);
        return factoryMenu >= 0
            && factoryMenu < build.CanBuild.Length
            && build.CanBuild[factoryMenu] == 2;
    }

    public int GetCount(ItemType type) =>
        type switch
        {
            ItemType.Cloak => Cloak,
            ItemType.Rocket => Rocket,
            ItemType.MedKit => MedKit,
            ItemType.Bomb => Bomb,
            ItemType.Mine => Mine,
            ItemType.Orb => Orb,
            ItemType.Flare => Flare,
            ItemType.Dfg => Dfg,
            ItemType.Wall => Wall,
            ItemType.Turret => Turret,
            ItemType.Sleeper => Sleeper,
            ItemType.Plasma => Plasma,
            _ => 0,
        };

    public bool TryConsume(ItemType type)
    {
        if (GetCount(type) <= 0)
        {
            return false;
        }

        switch (type)
        {
            case ItemType.Cloak: Cloak--; break;
            case ItemType.Rocket: Rocket--; break;
            case ItemType.MedKit: MedKit--; break;
            case ItemType.Bomb: Bomb--; break;
            case ItemType.Mine: Mine--; break;
            case ItemType.Orb: Orb--; break;
            case ItemType.Flare: Flare--; break;
            case ItemType.Dfg: Dfg--; break;
            case ItemType.Wall: Wall--; break;
            case ItemType.Turret: Turret--; break;
            case ItemType.Sleeper: Sleeper--; break;
            case ItemType.Plasma: Plasma--; break;
        }

        return true;
    }

    public bool TryAdd(ItemType type, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        var max = ItemCatalog.MaxCarryCount[(int)type];
        if (GetCount(type) + amount > max)
        {
            return false;
        }

        switch (type)
        {
            case ItemType.Cloak: Cloak += amount; break;
            case ItemType.Rocket: Rocket += amount; break;
            case ItemType.MedKit: MedKit += amount; break;
            case ItemType.Bomb: Bomb += amount; break;
            case ItemType.Mine: Mine += amount; break;
            case ItemType.Orb: Orb += amount; break;
            case ItemType.Flare: Flare += amount; break;
            case ItemType.Dfg: Dfg += amount; break;
            case ItemType.Wall: Wall += amount; break;
            case ItemType.Turret: Turret += amount; break;
            case ItemType.Sleeper: Sleeper += amount; break;
            case ItemType.Plasma: Plasma += amount; break;
            default: return false;
        }

        return true;
    }

    public void CycleSelection(int delta)
    {
        if (delta == 0 || HudItems.Length == 0)
        {
            return;
        }

        var currentIndex = Array.IndexOf(HudItems, SelectedItemType);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        for (var step = 0; step < HudItems.Length; step++)
        {
            currentIndex = (currentIndex + delta + HudItems.Length) % HudItems.Length;
            SelectedItemType = HudItems[currentIndex];
            if (GetCount(SelectedItemType) > 0)
            {
                return;
            }
        }
    }

    /// <summary>After depleting the selected item, move highlight to the next stocked slot.</summary>
    public void SelectNextAvailablePlaceable()
    {
        if (GetCount(SelectedItemType) > 0)
        {
            return;
        }

        CycleSelection(1);
    }
}
