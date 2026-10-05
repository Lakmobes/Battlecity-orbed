namespace BattleCity.Core.City;

/// <summary>
/// Who may load a <c>.city</c> design. Matches legacy <c>ProcessAutoBuild</c>:
/// an admin always may; a mayor may only while the city is not orbable and they are alive.
/// </summary>
public static class AutoBuildRules
{
    public static bool MayLoad(bool isAdmin, bool isMayor, bool isDead, bool cityIsOrbable)
    {
        if (isAdmin)
        {
            return true;
        }

        return isMayor && !isDead && !cityIsOrbable;
    }
}
