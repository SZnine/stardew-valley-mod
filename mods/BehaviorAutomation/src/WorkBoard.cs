using Microsoft.Xna.Framework;
using StardewValley;

namespace Sznine.BehaviorAutomation;

/// <summary>A single replaceable rectangle and its live work. No persistent stacked layers.</summary>
public sealed class WorkBoard
{
    private static int nextGeneration;
    private sealed record Selection(ToolMode Mode, Tool? Tool, Rectangle Area, bool Smart,
        StardewValley.Object? Material);
    private Selection? selection;
    private bool includeBuildings;
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
        Rectangle area, ModConfig config, StardewValley.Object? material = null, bool smart = false, bool includeBuildings = true, bool keepEmpty = false)
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
        this.includeBuildings = includeBuildings;
        var targets = Scan(who, config);
        if (material is not null)
            targets.AddRange(mode == ToolMode.Place ? Placement.Scan(map, who, material, area, config) : Planting.Scan(map, material, mode, area, cells, config, Jobs));
        int added = Add(targets);
        if (added == 0 && !keepEmpty)
            Clear();
        return (added, removed);
    }

    private List<WorkTarget> Scan(Farmer who, ModConfig config, Rectangle? area = null, bool obstacles = false, GameLocation? interior = null)
    {
        if (selection is not { } s || Location is null)
            return new();
        var map = interior ?? Location;
        var targets = s.Smart ? SmartSelection.Scan(map, who, s.Mode, s.Tool, area ?? s.Area, config, obstacles)
            : SmartSelection.ScanLeft(map, who, s.Tool, area ?? s.Area, config);
        if (!s.Smart && (s.Mode is not (ToolMode.Hand or ToolMode.Auto or ToolMode.Seeds or ToolMode.TreeSeeds or ToolMode.Place) || s.Mode == ToolMode.Hand && LastSelectedItem is null))
        {
            var held = WorldTargets.Scan(map, who, s.Mode, s.Tool, area ?? s.Area, config, includeTill: !obstacles);
            // Prefer the selected tool for the same task, keeping distinct follow-up actions.
            targets.RemoveAll(t => held.Any(h => ReferenceEquals(h.Entity, t.Entity) && h.Kind == t.Kind));
            targets.AddRange(held);
        }
        if (includeBuildings && config.WorkInsideBuildings && interior is null && !obstacles)
            targets.AddRange(BuildingWork.Scan(map, area ?? s.Area, room =>
                Scan(who, config, new(0, 0, room.Map.Layers[0].LayerWidth, room.Map.Layers[0].LayerHeight), interior: room)
                    .Any(t => WorldTargets.Unable(t, who, config) is null)));
        return targets;
    }

    internal WorkBoard ForInterior(GameLocation room, Farmer who, ModConfig config)
    {
        var s = selection ?? throw new InvalidOperationException("No parent selection.");
        var child = new WorkBoard();
        child.Select(room, who, s.Mode, s.Tool, new(0, 0, room.Map.Layers[0].LayerWidth, room.Map.Layers[0].LayerHeight),
            config, s.Material, s.Smart, includeBuildings: false, keepEmpty: true);
        child.LastSelectedItem = LastSelectedItem;
        return child;
    }

    public bool AllowsObstacle(WorkTarget target, ModConfig config) => selection is { } s
        && config.Allows(target.Kind, target.Scope)
        && (s.Smart ? target.Scope == WorkScope.Smart : target.Scope == WorkScope.LeftExtra
            || target.Scope == WorkScope.Held && target.Mode == s.Mode && ReferenceEquals(target.Tool, s.Tool));

    // Clearance uses exactly the same held-tool/extra/smart selection rules as the selected area.
    // Scanning all stored tools here used to silently upgrade a left basic task's tool.
    public IEnumerable<WorkTarget> ObstacleCandidates(Farmer who, ModConfig config) => Location is null
        ? Enumerable.Empty<WorkTarget>()
        : Scan(who, config, new(0, 0, Location.Map.Layers[0].LayerWidth, Location.Map.Layers[0].LayerHeight), obstacles: true)
            .Where(t => AllowsObstacle(t, config) && ObstaclePlanner.Removable(t, config));

    private int Add(IEnumerable<WorkTarget> targets)
    {
        int added = 0;
        var existing = Jobs.Select(t => (t.Entity, t.Kind)).ToHashSet();
        foreach (var target in targets)
        {
            if (Jobs.Count >= WorkRules.MaxJobs)
                break;
            if (rejected.Contains((target.Entity, target.Kind)) || !existing.Add((target.Entity, target.Kind)))
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
        Generation = System.Threading.Interlocked.Increment(ref nextGeneration);
    }
}
