namespace BattleCity.Shared.Data;

/// <summary>High-level client application states (legacy/client/CConstants.h States enum).</summary>
public enum ClientGameState
{
    Empty = 0,
    Login = 1,
    Recover = 2,
    Joining = 3,
    Game = 4,
    NewAccount = 5,
    Editing = 6,
    Personality = 7,
    Verify = 8,
    Meeting = 9,
    Interview = 10,
}
