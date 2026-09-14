using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

public sealed record WaterPattern(int Facing, int Power, Cell[] Offsets);
public sealed record WaterSource(Cell Tile, WateringCan Can);

/// <summary>Native charged footprints and bounded coverage planning, shared by both input modes.</summary>
public static class Watering
{
    private static readonly MethodInfo Affected = typeof(Tool).GetMethod("tilesAffected", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private sealed class Cache
    {
        public int Level = -1; public WaterPattern[] Patterns = Array.Empty<WaterPattern>();
    }
    private static readonly ConditionalWeakTable<WateringCan, Cache> patterns = new();
    public static int MaxPower(WateringCan can) => Math.Clamp(can.UpgradeLevel + (can.hasEnchantmentOfType<ReachingToolEnchantment>() ? 1 : 0), 0, 5);
    public static float Energy(Farmer who, WateringCan can, int power) => can.IsEfficient ? 0 : Math.Max(0, 2 * (power + 1) - who.FarmingLevel * .1f);
    public static WaterPattern[] Patterns(WateringCan can)
    {
        var cached = patterns.GetOrCreateValue(can);
        int level = MaxPower(can);
        if (cached.Level == level)
            return cached.Patterns;
        var result = new List<WaterPattern>();
        var probe = new Farmer();
        for (int facing = 0; facing < 4; facing++)
        {
            probe.FacingDirection = facing;
            for (int power = 0; power <= level; power++)
            {
                var tiles = (List<Vector2>)Affected.Invoke(can, new object[] { Vector2.Zero, power, probe })!;
                result.Add(new(facing, power, tiles.Select(Cell.At).Distinct().ToArray()));
            }
        }
        cached.Level = level;
        return cached.Patterns = result.ToArray();
    }

    public static IEnumerable<Approach> Approaches(IEnumerable<WorkTarget> targets, Farmer who, Rectangle area, ModConfig config)
    {
        var refill = new Dictionary<Cell, bool>();
        foreach (var group in targets.Where(t => t.Kind == ActionKind.Water && t.Entity is not WaterSource && t.Tool is WateringCan).GroupBy(t => t.Tool))
        {
            var can = (WateringCan)group.Key!;
            var at = Cell.Of(who);
            // Keep charged planning local and bounded; remaining targets retain simple reachable goals.
            var byTile = group.OrderBy(t => Math.Abs(t.Origin.X - at.X) + Math.Abs(t.Origin.Y - at.Y)).Take(256).GroupBy(t => t.Origin).ToDictionary(g => g.Key, g => g.First());
            foreach (var pattern in Patterns(can))
            {
                if (!who.hasWateringCanEnchantment && !can.IsBottomless && can.WaterLeft < pattern.Power + 1)
                    continue;
                if (who.Stamina - Energy(who, can, pattern.Power) < config.ReserveStamina)
                    continue;
                var aims = new HashSet<Cell>();
                foreach (var tile in byTile.Keys)
                    foreach (var delta in pattern.Offsets)
                        aims.Add(new(tile.X - delta.X, tile.Y - delta.Y));
                foreach (var aim in aims)
                {
                    if (!area.Contains(aim.X, aim.Y))
                        continue;
                    if (!refill.TryGetValue(aim, out bool isWater))
                        refill[aim] = isWater = who.currentLocation.CanRefillWateringCanOnTile(aim.X, aim.Y);
                    if (isWater)
                        continue;
                    var footprint = pattern.Offsets.Select(aim.Add).ToArray();
                    if (footprint.Any(p => !area.Contains(p.X, p.Y)))
                        continue;
                    var hits = footprint.Where(byTile.ContainsKey).Select(p => byTile[p]).ToHashSet();
                    if (hits.Count == 0)
                        continue;
                    var direction = Cell.Directions[pattern.Facing];
                    foreach (var stand in new[] { aim, new Cell(aim.X - direction.X, aim.Y - direction.Y) })
                        yield return new(hits.First(), stand, aim.Center, pattern.Facing, hits.Count, hits, pattern.Power);
                }
            }
        }
    }

    public static bool Valid(Approach plan, WorkBoard board, Farmer who, ModConfig config)
    {
        if (plan.Target.Tool is not WateringCan can || board.Location != who.currentLocation || Cell.Of(who) != plan.Stand)
            return false;
        var aim = Cell.At(plan.Aim / 64);
        if (plan.Target.Entity is WaterSource)
            return who.currentLocation.CanRefillWateringCanOnTile(aim.X, aim.Y);
        if (who.currentLocation.CanRefillWateringCanOnTile(aim.X, aim.Y))
            return false;
        if (!who.hasWateringCanEnchantment && can.WaterLeft <= 0)
            return false;
        if (who.Stamina - Energy(who, can, plan.Power) < config.ReserveStamina)
            return false;
        if (plan.Target.Kind == ActionKind.WaterBowl)
            return WorldTargets.Pending(who.currentLocation, plan.Target, config);
        var pattern = Patterns(can).FirstOrDefault(p => p.Facing == plan.Facing && p.Power == plan.Power);
        return pattern is not null && board.Area is { } area && pattern.Offsets.All(o => area.Contains(aim.Add(o).X, aim.Add(o).Y))
            && (plan.Hits ?? new() { plan.Target }).Any(t => board.Jobs.Contains(t) && WorldTargets.Pending(who.currentLocation, t, config));
    }
}

/// <summary>Incremental BFS finds the nearest reachable shore/well across the current map.</summary>
public sealed class RefillSearch
{
    private readonly Farmer who;
    private readonly GameLocation map;
    private readonly WorkTarget work;
    private readonly Queue<Cell> frontier = new();
    private readonly Dictionary<Cell, Cell> parents = new();
    private readonly HashSet<Cell> examined = new();
    private readonly Cell start;
    public bool Finished
    {
        get; private set;
    }
    public Approach? Result
    {
        get; private set;
    }
    public int Visited => examined.Count;

    public RefillSearch(Farmer who, WorkTarget work)
    {
        this.who = who;
        map = who.currentLocation;
        this.work = work;
        start = Cell.Of(who);
        frontier.Enqueue(start);
        parents[start] = start;
        examined.Add(start);
    }

    public void Step(int budget = 160)
    {
        if (Finished)
            return;
        for (int i = 0; i < budget && frontier.TryDequeue(out var stand); i++)
        {
            for (int facing = 0; facing < 4; facing++)
            {
                var water = stand.Add(Cell.Directions[facing]);
                if (water.X < 0 || water.Y < 0 || water.X >= map.Map.Layers[0].LayerWidth || water.Y >= map.Map.Layers[0].LayerHeight)
                    continue;
                if (map.CanRefillWateringCanOnTile(water.X, water.Y))
                {
                    var target = new WorkTarget
                    {
                        Kind = work.Kind,
                        Mode = ToolMode.WateringCan,
                        Tool = work.Tool,
                        Storage = work.Storage,
                        Entity = new WaterSource(water, (WateringCan)work.Tool!),
                        Origin = water,
                        Area = new(water.X, water.Y, 1, 1),
                        Scope = work.Scope
                    };
                    Result = new(target, stand, water.Center, facing);
                    Finished = true;
                    return;
                }
            }
            foreach (var delta in Cell.Directions)
            {
                var next = stand.Add(delta);
                if (!examined.Add(next) || !WorldTargets.CanStand(map, who, next))
                    continue;
                parents[next] = stand;
                frontier.Enqueue(next);
            }
            if (examined.Count >= 32768)
            {
                Finished = true;
                return;
            }
        }
        if (frontier.Count == 0)
            Finished = true;
    }
    public List<Cell> Path()
    {
        var result = new List<Cell>();
        if (Result is null)
            return result;
        for (var p = Result.Stand; p != start; p = parents[p])
            result.Add(p);
        result.Reverse();
        return result;
    }
}
