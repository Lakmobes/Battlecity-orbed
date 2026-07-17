using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Components;

public struct PlayerInventory
{
    public static readonly ItemType[] SelectableItems =
    [
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

    public static PlayerInventory CreateDemoLoadout() =>
        new()
        {
            Rocket = 2,
            MedKit = 2,
            Wall = 5,
            Mine = 3,
            Bomb = 2,
            Orb = 1,
            Flare = 1,
            Turret = 3,
            Sleeper = 2,
            Plasma = 2,
            Cloak = 1,
            Dfg = 1,
            SelectedItemType = ItemType.Wall,
        };

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
        if (delta == 0 || SelectableItems.Length == 0)
        {
            return;
        }

        var currentIndex = Array.IndexOf(SelectableItems, SelectedItemType);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        for (var step = 0; step < SelectableItems.Length; step++)
        {
            currentIndex = (currentIndex + delta + SelectableItems.Length) % SelectableItems.Length;
            SelectedItemType = SelectableItems[currentIndex];
            if (GetCount(SelectedItemType) > 0)
            {
                return;
            }
        }
    }

    /// <summary>After depleting the selected placeable, move selection to the next stocked slot.</summary>
    public void SelectNextAvailablePlaceable()
    {
        if (GetCount(SelectedItemType) > 0 && ItemCatalog.IsPlaceable(SelectedItemType))
        {
            return;
        }

        CycleSelection(1);
    }
}
