namespace BattleCity.Shared.Data;

/// <summary>Player connection states on the server (legacy/server/CConstants.h).</summary>
public enum PlayerConnectionState
{
    Disconnected = 0,
    Connected = 1,
    Verified = 2,
    Editing = 3,
    Chat = 4,
    Game = 5,
    Apply = 6,
}
