using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Sznine.BehaviorAutomation;

public sealed class ObstaclePlanner
{
    private readonly RouteSearch route;
    private readonly Dictionary<Cell, WorkTarget> blockers = new();
    public bool Finished => route.Finished;
    public static bool Removable(WorkTarget target, ModConfig config) => target.Entity is not GiantCrop && target.Kind switch
    {
        ActionKind.Stone or ActionKind.LargeRock or ActionKind.Twig or ActionKind.LargeWood or ActionKind.Weed or ActionKind.Grass or ActionKind.TreeStump or ActionKind.Sapling => true,
        ActionKind.WildTree => config.Allows(ActionKind.TreeStump, target.Scope),
        _ => false
    };
    public ObstaclePlanner(GameLocation map, Farmer who, IEnumerable<WorkTarget> goals, IEnumerable<WorkTarget> candidates, ModConfig config, ISet<object>? failed = null)
    {
        // One bounded world snapshot; no per-node scans of every inventory or entity.
        foreach (var target in candidates.Where(t => failed?.Contains(t.Entity) != true && WorldTargets.Unable(t, who, config) is null))
            for (int y = target.Area.Top; y < target.Area.Bottom; y++)
                for (int x = target.Area.Left; x < target.Area.Right; x++)
                {
                    var p = new Cell(x, y);
                    if (WorldTargets.CanStand(map, who, p) || !map.isTilePassable(p.Tile) || map.doesTileHaveProperty(x, y, "Water", "Back") is not null)
                        continue;
                    if (map.warps.Any(w => w.X == x && w.Y == y) || map.doesTileHaveProperty(x, y, "TouchAction", "Back") is not null)
                        continue;
                    if (map.Objects.TryGetValue(p.Tile, out var obj) && !ReferenceEquals(obj, target.Entity))
                        continue;
                    if (map.terrainFeatures.TryGetValue(p.Tile, out var terrain) && !ReferenceEquals(terrain, target.Entity) && terrain is not HoeDirt { crop: null })
                        continue;
                    if (map.buildings.Any(b => b.occupiesTile(p.Tile)))
                        continue;
                    var box = new Rectangle(x * 64 + 8, y * 64 + 16, 48, 32);
                    if (map.furniture.Any(f => !f.isPassable() && f.GetBoundingBox().Intersects(box)))
                        continue;
                    if (map.characters.Any(n => n.GetBoundingBox().Intersects(box)) || map.animals.Values.Any(a => a.GetBoundingBox().Intersects(box)))
                        continue;
                    if (map.largeTerrainFeatures.Any(f => !ReferenceEquals(f, target.Entity) && f.getBoundingBox().Intersects(box)))
                        continue;
                    blockers.TryAdd(p, target);
                }
        route = new(Cell.Of(who), goals, p => WorldTargets.CanStand(map, who, p), p => blockers.ContainsKey(p) ? 40 : null, settings: config);
    }
    public void Step() => route.Step();
    public WorkTarget? FirstObstacle()
    {
        foreach (var cell in route.Path())
            if (blockers.TryGetValue(cell, out var target))
                return target;
        return null;
    }
}
