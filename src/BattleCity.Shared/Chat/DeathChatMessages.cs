using BattleCity.Shared.Catalogs;

namespace BattleCity.Shared.Chat;

public static class DeathChatMessages
{
    private static readonly string[] Templates =
    [
        "{name} no longer exists as a single entity",
        "{name} just had a TNT experience",
        "{name} has shattered into many pieces",
        "{name} has been blown up",
        "{name} is no more",
        "{name} is worm food",
        "{name} just ran out of luck",
        "Alas poor {name} we knew him well",
        "A funeral service will shortly be held for {name}",
        "{name} has bitten the dust",
        "{name} has gone to meet his maker",
        "{name} is dead meat",
        "{name} was useless at this game anyway!",
    ];

    public static string Format(string playerName, byte victimCityId, byte killerCity, byte playerId)
    {
        var index = (playerId + killerCity * 31) % Templates.Length;
        var message = Templates[index].Replace("{name}", playerName, StringComparison.Ordinal);

        if (killerCity == byte.MaxValue)
        {
            return message;
        }

        if (victimCityId == killerCity)
        {
            message += " (Friendly Fire!)";
        }
        else if (CityCatalog.IsValidCityId(killerCity))
        {
            message += $" ({CityCatalog.GetName(killerCity)})";
        }

        return message;
    }
}
