using Microsoft.Xna.Framework;
using StardewValley;

namespace Sznine.BehaviorAutomation;

public sealed record WorkDecision(Approach Main, Approach Chosen, bool Opportunity, int MainCost, int ChosenCost, int ExtraCost);

/// <summary>Owns a tool pass and a committed main leg. Opportunities never move that leg's corridor.</summary>
public sealed class WorkCoordinator
{
    private sealed record Pass(WorkTarget Seed, Dictionary<Cell, int>? Order)
    {
        public bool Contains(WorkTarget t) => Order is null ? ReferenceEquals(t.Entity, Seed.Entity)
            : ReferenceEquals(t.Tool, Seed.Tool) && t.Mode == Seed.Mode && Order.ContainsKey(t.Origin);
        public List<WorkTarget> Remaining(IEnumerable<WorkTarget> jobs) => jobs.Where(Contains)
            .OrderBy(t => Order?.GetValueOrDefault(t.Origin) ?? 0).ToList();
    }
    private sealed record Leg(Approach Plan, HashSet<Cell> Corridor);
    private Pass? pass;
    private Leg? leg;
    private WorkTarget? pinned;
    private object? repeatedSingle;
    public WorkTarget? MainTarget => leg?.Plan.Target ?? pass?.Seed ?? pinned;
    public WorkDecision? LastDecision { get; private set; }
    public void Clear() { pass = null; leg = null; pinned = null; repeatedSingle = null; LastDecision = null; }
    public void InvalidateRoute() => leg = null;
    public void Pin(WorkTarget target) => pinned = target;
    public void Started(Approach action)
    {
        if (LastDecision is { Opportunity: true } decision && ReferenceEquals(action.Target, decision.Chosen.Target)
            && ActionPlans.Style(action.Target) == WorkStyle.Repeated)
            repeatedSingle = action.Target.Entity;
        if (leg is { } current && (ReferenceEquals(current.Plan.Target, action.Target) || action.Hits?.Contains(current.Plan.Target) == true))
            leg = null;
    }
    public IReadOnlyList<WorkTarget> Candidates(IReadOnlyList<WorkTarget> jobs)
    {
        if (pass is not null)
        {
            var remaining = pass.Remaining(jobs);
            if (remaining.Count > 0) return remaining;
            pass = null;
            leg = null;
            repeatedSingle = null;
        }
        return jobs;
    }

    public WorkSearch Search(Farmer who, IReadOnlyList<WorkTarget> candidates, IReadOnlyList<WorkTarget> jobs,
        Rectangle area, ModConfig config, bool forced = false)
        => new(result => Plan(result, who, candidates.ToArray(), jobs.ToArray(), area, config, forced));

    private IEnumerable<bool> Plan(WorkSearch result, Farmer who, WorkTarget[] candidates, WorkTarget[] jobs,
        Rectangle area, ModConfig config, bool forced)
    {
        var start = Cell.Of(who);
        bool CanStand(Cell p) => WorldTargets.CanStand(who.currentLocation, who, p);
        if (forced)
        {
            using var route = new RouteSearch(start, ActionPlans.Native(candidates, who, area, config), CanStand, config);
            while (!route.Finished) { route.Step(); yield return false; }
            result.Set(route.Result, route.Path(), candidates);
            yield break;
        }
        if (pass is null)
        {
            WorkTarget? seed = pinned is { } selected && candidates.Contains(selected) ? selected : null;
            pinned = null;
            if (seed is null)
            {
                using var entry = new RouteSearch(start, ActionPlans.Adjacent(candidates, entryOnly: true), CanStand, config);
                while (!entry.Finished) { entry.Step(); yield return false; }
                seed = entry.Result?.Target ?? candidates.MinBy(t => PathGeometry.Distance(start, t.Origin));
            }
            if (seed is null) yield break;
            foreach (bool step in CreatePass(seed, candidates, CanStand)) yield return step;
        }
        var remaining = pass!.Remaining(candidates);
        if (remaining.Count == 0) yield break;
        var front = remaining[0];
        // A large connected field retains its order, but native geometry stays bounded.
        var window = remaining.Take(256).ToArray();
        bool areaWork = ActionPlans.IsArea(front);
        RouteSearch? mainRoute = null;
        try
        {
            if (leg is { } saved && remaining.Contains(saved.Plan.Target) && saved.Corridor.Contains(start)
                && (saved.Plan.Target.Tool is not StardewValley.Tools.WateringCan can || Watering.Affordable(who, can, saved.Plan.Power, config)))
            {
                mainRoute = new(start, new[] { saved.Plan }, p => saved.Corridor.Contains(p) && CanStand(p), config);
                while (!mainRoute.Finished) { mainRoute.Step(); yield return false; }
                if (mainRoute.Result is null) { mainRoute.Dispose(); mainRoute = null; leg = null; }
            }
            if (mainRoute is null)
            {
                leg = null;
                mainRoute = new(start, ActionPlans.Native(window, who, area, config), CanStand, config,
                    style: ActionPlans.Style(front), targetCount: window.Length, front: areaWork ? front : null);
                while (!mainRoute.Finished) { mainRoute.Step(); yield return false; }
            }
            var main = mainRoute.Result;
            var path = mainRoute.Path();
            if (main is null)
            {
                result.Set(null, path, new[] { front });
                yield break;
            }
            if (areaWork)
                leg ??= new(main, OpportunitySearch.Corridor(start, path));
            var chosen = main;
            var chosenPath = path;
            int chosenCost = mainRoute.TravelCost, extra = 0;
            if (areaWork && mainRoute.TravelCost > 0)
            {
                var singles = jobs.Where(t => !ActionPlans.IsArea(t) && !t.IsObstacle && t.Entity is not BuildingDoor and not WaterSource
                    && WorldTargets.Pending(who.currentLocation, t, config) && WorldTargets.Unable(t, who, config) is null).ToArray();
                if (repeatedSingle is { } entity)
                {
                    var unfinished = singles.Where(t => ReferenceEquals(t.Entity, entity)).ToArray();
                    if (unfinished.Length > 0) singles = unfinished;
                    else repeatedSingle = null;
                }
                using var opportunity = new OpportunitySearch(start, main, mainRoute.TravelCost, leg!.Corridor,
                    ActionPlans.Native(singles, who, area, config), CanStand, config);
                while (!opportunity.Finished) { opportunity.Step(); yield return false; }
                if (opportunity.Result is { } single)
                {
                    chosen = single;
                    chosenPath = opportunity.Path;
                    chosenCost = opportunity.ChosenCost;
                    extra = opportunity.ExtraCost;
                }
            }
            LastDecision = new(main, chosen, !ReferenceEquals(main, chosen), mainRoute.TravelCost, chosenCost, extra);
            result.Set(chosen, chosenPath, new[] { front });
        }
        finally { mainRoute?.Dispose(); }
    }

    private IEnumerable<bool> CreatePass(WorkTarget seed, WorkTarget[] candidates, Func<Cell, bool> canStand)
    {
        if (!ActionPlans.IsArea(seed))
        {
            pass = new(seed, null);
            yield break;
        }
        var byCell = candidates.Where(t => ReferenceEquals(t.Tool, seed.Tool) && t.Mode == seed.Mode)
            .GroupBy(t => t.Origin).ToDictionary(g => g.Key, g => g.First());
        var connected = new HashSet<Cell> { seed.Origin };
        var pending = new Queue<Cell>();
        pending.Enqueue(seed.Origin);
        while (pending.TryDequeue(out var at))
        {
            foreach (var direction in Cell.Directions)
            {
                var next = at.Add(direction);
                if (!byCell.ContainsKey(next) && canStand(next)) next = next.Add(direction);
                if (byCell.ContainsKey(next) && connected.Add(next)) pending.Enqueue(next);
            }
            yield return false;
        }
        int left = connected.Min(p => p.X), right = connected.Max(p => p.X);
        int top = connected.Min(p => p.Y), bottom = connected.Max(p => p.Y);
        bool horizontal = right - left >= bottom - top;
        bool reverseRows = horizontal ? seed.Origin.Y > (top + bottom) / 2 : seed.Origin.X > (left + right) / 2;
        bool reverseColumns = horizontal ? seed.Origin.X > (left + right) / 2 : seed.Origin.Y > (top + bottom) / 2;
        int Row(Cell p) => horizontal ? (reverseRows ? bottom - p.Y : p.Y - top) : (reverseRows ? right - p.X : p.X - left);
        int Column(Cell p)
        {
            bool reverse = reverseColumns ^ (Row(p) % 2 != 0);
            return horizontal ? (reverse ? right - p.X : p.X - left) : (reverse ? bottom - p.Y : p.Y - top);
        }
        var ordered = connected.OrderBy(Row).ThenBy(Column).ToArray();
        int entry = Array.IndexOf(ordered, seed.Origin);
        pass = new(seed, ordered.Select((cell, index) => (cell, rank: (index - entry + ordered.Length) % ordered.Length))
            .ToDictionary(p => p.cell, p => p.rank));
    }
}

/// <summary>Disposable cooperative planning operation, canceled together with its current nested search.</summary>
public sealed class WorkSearch : IDisposable
{
    private readonly IEnumerator<bool> steps;
    public bool Finished { get; private set; }
    public Approach? Result { get; private set; }
    public List<Cell> Path { get; private set; } = new();
    public IReadOnlyList<WorkTarget> FailedCandidates { get; private set; } = Array.Empty<WorkTarget>();
    internal WorkSearch(Func<WorkSearch, IEnumerable<bool>> factory) => steps = factory(this).GetEnumerator();
    internal void Set(Approach? result, List<Cell> path, IReadOnlyList<WorkTarget> candidates)
    { Result = result; Path = path; FailedCandidates = candidates; }
    public void Step()
    {
        if (Finished) return;
        var slice = new PlanningSlice();
        for (int i = 0; i < 1024 && slice.HasTime; i++)
            if (!steps.MoveNext()) { Finished = true; steps.Dispose(); return; }
    }
    public void Dispose() => steps.Dispose();
}
