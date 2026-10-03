using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

public enum WorkStyle { Point, Charged, Sweep, Repeated }

public sealed record Approach(WorkTarget Target, Cell Stand, Vector2 Aim, int Facing, int Coverage = 1,
    HashSet<WorkTarget>? Hits = null, int Power = 0);

/// <summary>Native tool capabilities generate stances; task ordering belongs to the coordinator.</summary>
public static class ActionPlans
{
    public static WorkStyle Style(WorkTarget target) => target.Mode switch
    {
        ToolMode.Scythe => WorkStyle.Sweep,
        ToolMode.WateringCan when target.Kind == ActionKind.Water && target.Entity is not WaterSource
            && target.Tool is WateringCan can && Watering.MaxPower(can) > 0 => WorkStyle.Charged,
        ToolMode.Axe or ToolMode.Pickaxe => WorkStyle.Repeated,
        _ => WorkStyle.Point
    };
    public static bool IsArea(WorkTarget target) => Style(target) is WorkStyle.Charged or WorkStyle.Sweep;

    public static IEnumerable<Approach?> Native(IReadOnlyList<WorkTarget> targets, Farmer who, Rectangle area, ModConfig config)
    {
        foreach (var plan in Watering.PlanSteps(targets, who, area, config)) yield return plan;
        foreach (var plan in ScythePlanner.PlanSteps(targets, who)) yield return plan;
        foreach (var plan in Adjacent(targets.Where(t => t.Mode != ToolMode.Scythe && (t.Kind != ActionKind.Water || t.Entity is WaterSource)))) yield return plan;
    }

    // Entry probes only choose a work pass. Never execute their approximate stances.
    public static IEnumerable<Approach?> Adjacent(IEnumerable<WorkTarget> targets, bool entryOnly = false)
    {
        foreach (var target in targets)
        {
            var box = target.Bounds;
            var tiles = new Rectangle(box.Left / 64, box.Top / 64,
                Math.Max(1, (box.Right - 1) / 64 - box.Left / 64 + 1), Math.Max(1, (box.Bottom - 1) / 64 - box.Top / 64 + 1));
            Approach At(Cell stand)
            {
                var aim = target.Entity is Character ? box.Center.ToVector2()
                    : new Cell(Math.Clamp(stand.X, tiles.Left, tiles.Right - 1), Math.Clamp(stand.Y, tiles.Top, tiles.Bottom - 1)).Center;
                var delta = aim - stand.Center;
                int face = Math.Abs(delta.X) > Math.Abs(delta.Y) ? delta.X > 0 ? 1 : 3 : delta.Y > 0 ? 2 : 0;
                return new(target, stand, aim, face);
            }
            if (entryOnly) yield return At(new(box.Center.X / 64, box.Center.Y / 64));
            for (int x = tiles.Left; x < tiles.Right; x++)
            {
                yield return At(new(x, tiles.Top - 1));
                yield return At(new(x, tiles.Bottom));
            }
            for (int y = tiles.Top; y < tiles.Bottom; y++)
            {
                yield return At(new(tiles.Left - 1, y));
                yield return At(new(tiles.Right, y));
            }
        }
    }
}
