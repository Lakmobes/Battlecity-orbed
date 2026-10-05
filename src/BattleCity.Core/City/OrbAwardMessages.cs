namespace BattleCity.Core.City;

/// <summary>Legacy <c>CProcess::ProcessOrbed</c> chat line (blue).</summary>
public static class OrbAwardMessages
{
    public static string Format(string orberCity, string victimCity, uint points, uint orberCityPoints) =>
        $"{orberCity} has orbed {victimCity}!  ({points} Points).  {orberCity} is now worth {orberCityPoints} Points!";
}
