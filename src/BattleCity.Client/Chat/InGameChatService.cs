using BattleCity.Client.Network;

using BattleCity.Shared.Chat;

using BattleCity.Shared.Constants;

using BattleCity.Shared.Network.Packets;



using Microsoft.Xna.Framework;



namespace BattleCity.Client.Chat;



public static class ChatColorResolver

{

    public static Color ForLocalMessage(bool isDead) =>

        isDead ? ToColor(UiColors.Red) : ToColor(UiColors.Green);



    public static Color ForRemoteMessage(byte localCityId, byte senderCityId, bool senderIsDead)

    {

        if (senderIsDead)

        {

            return ToColor(UiColors.Red);

        }



        return senderCityId == localCityId

            ? ToColor(UiColors.Green)

            : ToColor(UiColors.OffWhite);

    }



    public static Color System => ToColor(UiColors.Yellow);



    public static Color Global => ToColor(UiColors.White);



    public static Color Whisper => ToColor(UiColors.White);



    private static Color ToColor((byte R, byte G, byte B) rgb) => new(rgb.R, rgb.G, rgb.B);

}



public static class InGameChatService

{

    public static void AppendIncoming(

        InGameChatLog log,

        RemotePlayerSync remotePlayers,

        GameClient client,

        string localPlayerName,

        byte localCityId,

        in ServerChatMessagePacket packet,

        Func<byte, bool> isPlayerDead)

    {

        var senderName = ResolveSenderName(packet.SenderId, client, localPlayerName, remotePlayers);

        var senderCityId = ResolveSenderCityId(packet.SenderId, client, localCityId, remotePlayers);

        var isDead = isPlayerDead(packet.SenderId);

        var color = packet.SenderId == client.PlayerId

            ? ChatColorResolver.ForLocalMessage(isDead)

            : ChatColorResolver.ForRemoteMessage(localCityId, senderCityId, isDead);



        log.Append($"{senderName}: {packet.Message}", color);

    }



    public static void AppendGlobal(

        InGameChatLog log,

        RemotePlayerSync remotePlayers,

        GameClient client,

        string localPlayerName,

        in ServerChatMessagePacket packet)

    {

        var senderName = ResolveSenderName(packet.SenderId, client, localPlayerName, remotePlayers);

        log.Append($"{senderName} (Global): {packet.Message}", ChatColorResolver.Global);

    }



    public static void AppendWhisper(

        InGameChatLog log,

        RemotePlayerSync remotePlayers,

        GameClient client,

        string localPlayerName,

        in ServerChatMessagePacket packet)

    {

        var senderName = ResolveSenderName(packet.SenderId, client, localPlayerName, remotePlayers);

        log.Append($"{senderName} (PM): {packet.Message}", ChatColorResolver.Whisper);

    }



    public static void AppendLocalOutgoing(

        InGameChatLog log,

        string localPlayerName,

        string message,

        bool isDead)

    {

        log.Append($"{localPlayerName}: {message}", ChatColorResolver.ForLocalMessage(isDead));

    }



    public static void AppendLocalGlobal(InGameChatLog log, string localPlayerName, string message)

    {

        log.Append($"{localPlayerName} (Global): {message}", ChatColorResolver.Global);

    }



    public static void AppendLocalWhisper(

        InGameChatLog log,

        string localPlayerName,

        string recipientName,

        string message)

    {

        log.Append($"{localPlayerName} (to {recipientName}): {message}", ChatColorResolver.Whisper);

    }



    public static void AppendSystem(InGameChatLog log, string message) =>

        log.Append(message, ChatColorResolver.System);



    public static void AppendDeath(

        InGameChatLog log,

        RemotePlayerSync remotePlayers,

        GameClient client,

        string localPlayerName,

        byte localCityId,

        in ServerDeathPacket death)

    {

        var playerName = ResolveSenderName(death.PlayerId, client, localPlayerName, remotePlayers);

        var victimCityId = death.PlayerId == client.PlayerId

            ? localCityId

            : remotePlayers.TryGetCityId(death.PlayerId, out var cityId)

                ? cityId

                : localCityId;



        var message = DeathChatMessages.Format(playerName, victimCityId, death.KillerCity, death.PlayerId);

        log.Append(message, ChatColorResolver.System);

    }



    private static string ResolveSenderName(

        byte senderId,

        GameClient client,

        string localPlayerName,

        RemotePlayerSync remotePlayers) =>

        remotePlayers.GetChatDisplayName(senderId);



    private static byte ResolveSenderCityId(

        byte senderId,

        GameClient client,

        byte localCityId,

        RemotePlayerSync remotePlayers) =>

        senderId == client.PlayerId

            ? localCityId

            : remotePlayers.TryGetCityId(senderId, out var cityId)

                ? cityId

                : localCityId;

}


