using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

public static class SmartSelection
{
    // Animal care and produce are independent tasks; other entities retain a single chosen action.
    public static List<WorkTarget> Scan(GameLocation map, Farmer who, ToolMode preferred, Tool? held, Rectangle area, ModConfig config, bool obstacles = false, WorkScope scope = WorkScope.Smart)
        => ScanCore(map, who, preferred, held, area, config, obstacles, scope);
    public static List<WorkTarget> ScanLeft(GameLocation map, Farmer who, Tool? held, Rectangle area, ModConfig config)
        => ScanCore(map, who, ModeInfo.From(held) ?? ToolMode.Hand, held, area, config, false, WorkScope.LeftExtra);
    private static List<WorkTarget> ScanCore(GameLocation map, Farmer who, ToolMode preferred, Tool? held, Rectangle area, ModConfig config, bool obstacles, WorkScope scope)
    {
        var choices = new Dictionary<(object Entity, ActionKind? Care), (WorkTarget Target, int Score)>();
        var sources = new List<(ToolMode Mode, Tool? Tool, StoredTool? Storage)> { (ToolMode.Hand, null, null) };
        foreach (var (tool, storage) in ToolStorage.Tools(who, config))
            if (ModeInfo.From(tool) is { } mode && mode != ToolMode.Hoe)
                sources.Add((mode, tool, storage));
        foreach (var (mode, tool, storage) in sources)
        {
            foreach (var target in WorldTargets.Scan(map, who, mode, tool, area, config, includeTill: !obstacles, scope: scope))
            {
                if (!config.Allows(target.Kind, scope))
                    continue;
                target.Scope = scope;
                if (obstacles && !ObstaclePlanner.Removable(target, config))
                    continue;
                target.Storage = storage;
                var unavailable = WorldTargets.Unable(target, who, config);
                if (unavailable is "upgrade" or "missing-tool" or "sleeping")
                    continue;
                int rank = target.Kind switch
                {
                    ActionKind.HarvestCrop or ActionKind.Fruit or ActionKind.Bush or ActionKind.Forage or ActionKind.Machine => 0,
                    ActionKind.Milk or ActionKind.Shear => 10,
                    ActionKind.Pet => 20,
                    ActionKind.Weed when mode == ToolMode.Scythe => 0,
                    _ => 30
                };
                int score = rank * 100;
                if (mode == preferred)
                    score -= 10000;
                // Cutting weeds and green-rain foliage never needs the held pickaxe's stamina cost.
                if (target.Kind == ActionKind.Weed && mode == ToolMode.Scythe)
                    score -= 20000;
                if (unavailable is not null)
                    score += 500;
                if (ReferenceEquals(tool, held) && mode == preferred)
                    score -= 100;
                else if (tool is not null)
                    score -= Math.Clamp(tool.UpgradeLevel, 0, 10) * 5;
                if (storage is not null)
                    score += 30;
                var key = (target.Entity, target.Entity is FarmAnimal ? (ActionKind?)target.Kind : null);
                if (!choices.TryGetValue(key, out var old) || score < old.Score)
                    choices[key] = (target, score);
            }
        }
        return choices.Values.Select(v => v.Target).ToList();
    }
}
