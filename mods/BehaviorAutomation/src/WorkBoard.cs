using Microsoft.Xna.Framework;
using StardewValley;

namespace Sznine.BehaviorAutomation;

/// <summary>A single replaceable rectangle and its live work. No persistent stacked layers.</summary>
public sealed class WorkBoard
{
    private sealed record Selection(ToolMode Mode, Tool? Tool, Rectangle Area, bool Smart,
        StardewValley.Object? Material);
    private Selection? selection;
    private readonly HashSet<(object Entity, ActionKind Kind)> rejected = new();
    public readonly List<WorkTarget> Jobs = new();
    public GameLocation? Location
    {
        get; private set;
    }
    public Item? LastSelectedItem
    {
        get; private set;
    }
    public int Revision
    {
        get; private set;
    }
    public int Generation
    {
        get; private set;
    }
    public bool HasSelection => selection is not null;
    public bool Smart => selection?.Smart == true;
    public Rectangle? Area => selection?.Area;
    public bool OrderedPlanting => selection is { Smart: false, Material: not null };
    public Item? SelectionIcon => (Item?)selection?.Tool ?? selection?.Material;

    public (int Added, int Removed) Select(GameLocation map, Farmer who, ToolMode mode, Tool? tool,
        Rectangle area, ModConfig config, StardewValley.Object? material = null, bool smart = false)
    {
        int removed = Jobs.Count;
        Clear();
        if (map.Map is null)
            return (0, removed);
        area = Rectangle.Intersect(area, new(0, 0, map.Map.Layers[0].LayerWidth, map.Map.Layers[0].LayerHeight));
        if (area.Width <= 0 || area.Height <= 0)
            return (0, removed);
        area.Width = Math.Min(area.Width, WorkRules.MaxSelectionSize);
        area.Height = Math.Min(area.Height, WorkRules.MaxSelectionSize);
        var cells = new HashSet<Cell>();
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                cells.Add(new(x, y));
        Location = map;
        LastSelectedItem = who.CurrentItem;
        if (smart || mode is not (ToolMode.Seeds or ToolMode.TreeSeeds or ToolMode.Place))
            material = null;
        else
            material ??= who.CurrentItem?.getOne() as StardewValley.Object;
        selection = new(mode, tool, area, smart, material);
        var targets = Scan(who, config);
        if (material is not null)
            targets.AddRange(mode == ToolMode.Place ? Placement.Scan(map, who, material, area, config) : Planting.Scan(map, material, mode, area, cells, config, Jobs));
        int added = Add(targets);
        if (added == 0)
            Clear();
        return (added, removed);
    }

    private List<WorkTarget> Scan(Farmer who, ModConfig config)
    {
        if (selection is not { } s || Location is null)
            return new();
        if (s.Smart)
            return SmartSelection.Scan(Location, who, s.Mode, s.Tool, s.Area, config);
        var targets = SmartSelection.ScanLeft(Location, who, s.Tool, s.Area, config);
        if (s.Mode is not (ToolMode.Hand or ToolMode.Auto or ToolMode.Seeds or ToolMode.TreeSeeds or ToolMode.Place) || s.Mode == ToolMode.Hand && LastSelectedItem is null)
        {
            var held = WorldTargets.Scan(Location, who, s.Mode, s.Tool, s.Area, config);
            // Prefer the selected tool for the same task, keeping distinct follow-up actions.
            targets.RemoveAll(t => held.Any(h => ReferenceEquals(h.Entity, t.Entity) && h.Kind == t.Kind));
            targets.AddRange(held);
        }
        return targets;
    }

    public bool AllowsObstacle(WorkTarget target, ModConfig config) => selection is { } s
        && config.Allows(target.Kind, target.Scope)
        && (s.Smart ? target.Scope == WorkScope.Smart : target.Scope == WorkScope.LeftExtra || target.Mode == s.Mode);

    private int Add(IEnumerable<WorkTarget> targets)
    {
        int added = 0;
        foreach (var target in targets)
        {
            if (Jobs.Count >= WorkRules.MaxJobs)
                break;
            if (rejected.Contains((target.Entity, target.Kind)) || Jobs.Any(t => Equals(t.Entity, target.Entity) && t.Kind == target.Kind))
                continue;
            target.Group = target.Mode == ToolMode.Hand ? 1 : 2;
            Jobs.Add(target);
            added++;
        }
        if (added > 0)
            Revision++;
        return added;
    }

    public int Refresh(Farmer who, ModConfig config) => Location == who.currentLocation ? Add(Scan(who, config)) : 0;
    public void Prune(ModConfig config)
    {
        if (Location is not null)
            Jobs.RemoveAll(t => !WorldTargets.Pending(Location, t, config));
    }
    public void Reject(WorkTarget target)
    {
        rejected.Add((target.Entity, target.Kind));
        Jobs.Remove(target);
    }
    public void Clear()
    {
        selection = null;
        Jobs.Clear();
        rejected.Clear();
        Location = null;
        LastSelectedItem = null;
        Revision++;
        Generation++;
    }
}
