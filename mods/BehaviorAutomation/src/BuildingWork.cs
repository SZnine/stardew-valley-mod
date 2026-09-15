using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;

namespace Sznine.BehaviorAutomation;

/// <summary>A selected building's native human entrance or the matching return warp.</summary>
public sealed record BuildingDoor(Building Building, GameLocation Outside, GameLocation Inside, bool Exit = false)
{
    public Item Icon => ItemRegistry.Create("(BC)130");
    public Cell Tile => Exit ? new(Inside.warps[0].X, Inside.warps[0].Y)
        : new(Building.tileX.Value + Building.humanDoor.X, Building.tileY.Value + Building.humanDoor.Y);
    public WorkTarget Target() => new()
    {
        Entity = this, Kind = ActionKind.BuildingInterior, Mode = ToolMode.Hand,
        Origin = Tile, Area = new(Tile.X, Tile.Y, 1, 1)
    };
}

internal static class BuildingWork
{
    public static IEnumerable<WorkTarget> Scan(GameLocation map, Rectangle area, Func<GameLocation, bool> hasWork)
    {
        foreach (var building in map.buildings)
        {
            if (building.daysOfConstructionLeft.Value > 0 || building.humanDoor.X < 0
                || !area.Intersects(new(building.tileX.Value, building.tileY.Value, building.tilesWide.Value, building.tilesHigh.Value)))
                continue;
            var room = building.GetIndoors();
            if (room?.Map is null || room == map || room.warps.Count == 0 || !hasWork(room)) continue;
            yield return new BuildingDoor(building, map, room).Target();
        }
    }
}
