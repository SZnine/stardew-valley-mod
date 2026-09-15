using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

public static class ScythePlanner
{
    private static readonly Dictionary<(int Type, int Extra, Rectangle Body), Cell[][]> footprints = new();
    public static Cell[][] Footprint(MeleeWeapon scythe, Farmer who, bool centered = false)
    {
        var at = Cell.Of(who);
        var body = who.GetBoundingBox();
        body.Offset(-at.X * 64, -at.Y * 64);
        if (centered)
        {
            body.X = 32 - body.Width / 2;
            body.Y = 32 - body.Height / 2;
        }
        var key = (scythe.type.Value, scythe.addedAreaOfEffect.Value, body);
        if (footprints.TryGetValue(key, out var cached))
            return cached;
        if (footprints.Count >= 64)
            footprints.Clear();
        var origin = new Cell(16, 16);
        body.Offset(origin.X * 64, origin.Y * 64);
        var result = new Cell[4][];
        // Native area calculation chooses unused probe points with Game1.random. Do not advance the game's RNG while planning.
        var random = Game1.random;
        Game1.random = new Random(0);
        try
        {
            for (int facing = 0; facing < 4; facing++)
            {
                var aim = origin.Add(Cell.Directions[facing]).Center;
                var tiles = new HashSet<Cell>();
                for (int frame = 0; frame < 6; frame++)
                {
                    var first = Vector2.Zero;
                    var second = Vector2.Zero;
                    var hit = scythe.getAreaOfEffect((int)aim.X, (int)aim.Y, facing, ref first, ref second, body, frame);
                    foreach (var tile in Utility.getListOfTileLocationsForBordersOfNonTileRectangle(hit))
                        tiles.Add(new((int)tile.X - origin.X, (int)tile.Y - origin.Y));
                }
                result[facing] = tiles.ToArray();
            }
        }
        finally { Game1.random = random; }
        return footprints[key] = result;
    }
    public static Approach? AtStand(IEnumerable<WorkTarget> targets, Farmer who, MeleeWeapon tool, int? preferredFacing = null, int minimumPreferredCoverage = int.MaxValue)
    {
        var list = targets.Where(t => t.Mode == ToolMode.Scythe && ReferenceEquals(t.Tool, tool)).ToArray();
        var at = Cell.Of(who);
        var sweep = Footprint(tool, who);
        Approach? best = null;
        for (int face = 0; face < 4; face++)
        {
            var hits = list.Where(t => sweep[face].Any(p => t.Area.Contains(at.X + p.X, at.Y + p.Y))).ToArray();
            if (hits.Length == 0)
                continue;
            var plan = new Approach(hits[0], at, at.Add(Cell.Directions[face]).Center, face, hits.Length, hits.ToHashSet());
            if (face == preferredFacing && hits.Length >= minimumPreferredCoverage)
                return plan;
            if (best is null || hits.Length > best.Coverage || hits.Length == best.Coverage && face == preferredFacing)
                best = plan;
        }
        return best;
    }
    public static IEnumerable<Approach> Approaches(IEnumerable<WorkTarget> targets, Farmer who)
    {
        foreach (var group in targets.Where(t => t.Mode == ToolMode.Scythe && t.Tool is MeleeWeapon).GroupBy(t => t.Tool))
        {
            // Future stands use the arrival center, not the player's arbitrary sub-tile offset at planning time.
            var sweep = Footprint((MeleeWeapon)group.Key!, who, centered: true);
            var coverage = new Dictionary<(Cell Stand, int Facing), HashSet<WorkTarget>>();
            foreach (var target in group)
                for (int y = target.Area.Top; y < target.Area.Bottom; y++)
                    for (int x = target.Area.Left; x < target.Area.Right; x++)
                        for (int face = 0; face < 4; face++)
                            foreach (var offset in sweep[face])
                            {
                                var key = (new Cell(x - offset.X, y - offset.Y), face);
                                if (!coverage.TryGetValue(key, out var hits))
                                    coverage[key] = hits = new();
                                hits.Add(target);
                            }
            foreach (var (point, hits) in coverage)
                yield return new(hits.First(), point.Stand, point.Stand.Add(Cell.Directions[point.Facing]).Center, point.Facing, hits.Count, hits);
        }
    }
}
