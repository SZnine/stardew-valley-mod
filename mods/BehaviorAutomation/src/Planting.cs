using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace Sznine.BehaviorAutomation;
public static class Planting
{
    public static ActionKind Kind(SObject seed) => seed.IsWildTreeSapling() ? ActionKind.PlantWildTree : seed.IsFruitTreeSapling() ? ActionKind.PlantFruitTree : seed.IsTeaSapling() ? ActionKind.PlantTea : ActionKind.PlantCrop;
    private static int Distance(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private static int Spacing(ActionKind kind, ModConfig c) => kind == ActionKind.PlantFruitTree ? c.FruitTreeSpacing : kind == ActionKind.PlantWildTree ? c.WildTreeSpacing : 0;
    private static bool Room(Cell at, ActionKind kind, Func<Cell, ActionKind?> occupied, ModConfig config)
    {
        int spacing = Spacing(kind, config);
        int radius = spacing > 0 ? Math.Max(config.WildTreeSpacing, config.FruitTreeSpacing) - 1 : 1;
        for (int y = at.Y - radius; y <= at.Y + radius; y++)
            for (int x = at.X - radius; x <= at.X + radius; x++)
            {
                var p = new Cell(x, y);
                var other = occupied(p);
                if (other is null)
                    continue;
                int otherSpacing = Spacing(other.Value, config);
                int required = spacing > 0 && otherSpacing > 0 ? Math.Max(spacing, otherSpacing) : kind == ActionKind.PlantFruitTree || other == ActionKind.PlantFruitTree ? 2 : 0;
                if (Distance(at, p) < required)
                    return false;
            }
        return true;
    }
    public static bool OpenSpot(GameLocation map, SObject seed, Cell at)
    {
        if (map.Map is null || at.X < 0 || at.Y < 0 || at.X >= map.Map.Layers[0].LayerWidth || at.Y >= map.Map.Layers[0].LayerHeight)
            return false;
        if (map.Objects.TryGetValue(at.Tile, out var obj))
            return seed.Category == -74 && !seed.IsFruitTreeSapling() && !seed.IsWildTreeSapling()
            && obj is IndoorPot pot && pot.bush.Value is null && pot.hoeDirt.Value.crop is null;
        var terrain = map.terrainFeatures.GetValueOrDefault(at.Tile);
        if (terrain is not null && (terrain is not HoeDirt dirt || dirt.crop is not null))
            return false;
        return seed.isSapling() || terrain is HoeDirt;
    }
    public static bool CanPlant(GameLocation map, SObject seed, Cell at, ModConfig config)
    {
        if (!OpenSpot(map, seed, at))
            return false;
        if (!seed.canBePlacedHere(map, at.Tile, showError: false))
            return false;
        // Native fruit-sapling placement has a ground check beyond canBePlacedHere.
        // Filter those tiles before routing so a rejected planting cannot stall nearby work.
        if (seed.IsFruitTreeSapling())
        {
            bool diggable = map.doesTileHaveProperty(at.X, at.Y, "Diggable", "Back") is not null;
            string type = map.doesTileHaveProperty(at.X, at.Y, "Type", "Back");
            bool explicitTrees = map.doesEitherTileOrTileIndexPropertyEqual(at.X, at.Y, "CanPlantTrees", "Back", "T");
            bool farmGround = map is Farm && (diggable || type is "Grass" or "Dirt" || explicitTrees)
                && (!map.IsNoSpawnTile(at.Tile, "Tree") || explicitTrees);
            if (!farmGround && !((diggable || type == "Stone") && map.CanPlantTreesHere(seed.ItemId, at.X, at.Y, out _)))
                return false;
        }
        return Room(at, Kind(seed), p => map.terrainFeatures.GetValueOrDefault(p.Tile) switch { FruitTree => ActionKind.PlantFruitTree, Tree => ActionKind.PlantWildTree, _ => null }, config);
    }
    public static List<WorkTarget> Scan(GameLocation map, SObject seed, ToolMode mode, Rectangle area, HashSet<Cell> added, ModConfig config, IEnumerable<WorkTarget> queued)
    {
        var kind = Kind(seed);
        var result = new List<WorkTarget>();
        if (!config.Allows(kind))
            return result;
        var reservations = queued.Where(t => t.Material is not null).GroupBy(t => t.Origin).ToDictionary(g => g.Key, g => g.First().Kind);
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
            {
                var at = new Cell(x, y);
                if (!added.Contains(at) || reservations.ContainsKey(at) || !CanPlant(map, seed, at, config) || !Room(at, kind, p => reservations.TryGetValue(p, out var k) ? k : null, config))
                    continue;
                var target = new WorkTarget { Mode = mode, Kind = kind, Entity = at, Origin = at, Area = new(x, y, 1, 1), Material = (SObject)seed.getOne() };
                result.Add(target);
                reservations.Add(at, kind);
            }
        return result;
    }
}
