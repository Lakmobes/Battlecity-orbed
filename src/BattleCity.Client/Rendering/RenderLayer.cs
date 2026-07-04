namespace BattleCity.Client.Rendering;

/// <summary>Draw order matching legacy/client/CDrawing.cpp.</summary>
public enum RenderLayer
{
    Ground = 0,
    Terrain = 1,
    Buildings = 2,
    GroundItems = 3,
    Entities = 4,
    Effects = 5,
    Ui = 6,
    MiniMap = 7,
}
