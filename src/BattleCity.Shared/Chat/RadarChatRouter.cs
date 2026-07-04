using BattleCity.Shared.Constants;

namespace BattleCity.Shared.Chat;

public enum RadarChatDeliveryMode
{
    RadarOnly,
    RadarAndTeam,
}

public static class RadarChatRouter
{
    public static bool ShouldDeliver(
        RadarChatDeliveryMode mode,
        int senderX,
        int senderY,
        byte senderCityId,
        int recipientX,
        int recipientY,
        byte recipientCityId)
    {
        var onRadar = IsWithinRadar(senderX, senderY, recipientX, recipientY);
        return mode switch
        {
            RadarChatDeliveryMode.RadarOnly => onRadar,
            RadarChatDeliveryMode.RadarAndTeam => onRadar || senderCityId == recipientCityId,
            _ => false,
        };
    }

    public static bool IsWithinRadar(int senderX, int senderY, int recipientX, int recipientY) =>
        Math.Abs(recipientX - senderX) < ServerConstants.RadarSize
        && Math.Abs(recipientY - senderY) < ServerConstants.RadarSize;
}
