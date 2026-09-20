using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

namespace Sznine.BehaviorAutomation;

public sealed record Approach(WorkTarget Target, Cell Stand, Vector2 Aim, int Facing, int Coverage = 1, HashSet<WorkTarget>? Hits = null, int Power = 0);
public sealed class RouteSearch
{
    private readonly PriorityQueue<Cell, (int Cost, int Turns, long Order)> frontier = new();
    private readonly Dictionary<Cell, Cell> previous = new();
    private readonly Dictionary<Cell, int> distance = new();
    private readonly Dictionary<Cell, int> turns = new();
    private long enqueueOrder;
    private readonly Dictionary<Cell, bool> walk = new();
    private readonly Dictionary<Cell, List<Approach>> goals = new();
    private readonly List<(Approach Plan, int Cost)> reached = new();
    private readonly int scytheTargets;
    private readonly int scytheStreak;
    private readonly int scytheDeferrals;
    private Approach? nearestWater;
    private double waterScore = double.MaxValue;
    private int waterCost = int.MaxValue;
    private Approach? nearestOther;
    private int nearestOtherCost = int.MaxValue;
    private readonly Func<Cell, bool> canStand;
    private readonly Func<Cell, int?>? clearanceCost;
    private readonly Cell start;
    private readonly bool batch;
    private readonly bool diagonal;
    private readonly int lookahead, swingCost;
    private int firstGoalCost = -1;
    private double bestScore = double.MaxValue;
    public bool Finished
    {
        get; private set;
    }
    public Approach? Result
    {
        get; private set;
    }
    public int Visited => previous.Count;
    public RouteSearch(Cell start, IEnumerable<WorkTarget> targets, Func<Cell, bool> canStand, Func<Cell, int?>? clearanceCost = null, Farmer? scytheFarmer = null, int scytheStreak = 0, int scytheDeferrals = 0, IEnumerable<Approach>? waterPlans = null, ModConfig? settings = null)
    {
        this.start = start;
        this.canStand = canStand;
        this.clearanceCost = clearanceCost;
        this.scytheStreak = scytheStreak;
        this.scytheDeferrals = scytheDeferrals;
        diagonal = settings?.AllowDiagonalMovement ?? true;
        lookahead = settings?.ScytheSearchTiles ?? 12;
        swingCost = settings?.ScytheSwingCost ?? 16;
        frontier.Enqueue(start, (0, 0, enqueueOrder++));
        previous[start] = start;
        distance[start] = 0;
        turns[start] = 0;
        var snapshot = targets.ToArray();
        var water = waterPlans?.ToArray() ?? Array.Empty<Approach>();
        batch = scytheFarmer is not null && snapshot.Any(t => t.Mode == ToolMode.Scythe) || water.Length > 0;
        foreach (var plan in water)
        {
            if (!goals.TryGetValue(plan.Stand, out var plans))
                goals[plan.Stand] = plans = new();
            plans.Add(plan);
        }
        var batchedWater = water.SelectMany(p => p.Hits ?? new HashSet<WorkTarget> { p.Target }).ToHashSet();
        scytheTargets = snapshot.Count(t => t.Mode == ToolMode.Scythe);
        if (batch)
            foreach (var plan in ScythePlanner.Approaches(snapshot, scytheFarmer!))
            {
                if (!goals.TryGetValue(plan.Stand, out var plans))
                    goals[plan.Stand] = plans = new();
                plans.Add(plan);
            }
        foreach (var target in snapshot)
        {
            if (batch && target.Mode == ToolMode.Scythe || batchedWater.Contains(target) && target.Kind == ActionKind.Water && target.Entity is not WaterSource)
                continue;
            var box = target.Bounds;
            var tiles = new Rectangle(box.Left / 64, box.Top / 64, Math.Max(1, (box.Right - 1) / 64 - box.Left / 64 + 1), Math.Max(1, (box.Bottom - 1) / 64 - box.Top / 64 + 1));
            void Add(Cell stand)
            {
                Vector2 aim = target.Entity is Character ? box.Center.ToVector2() : new Cell(Math.Clamp(stand.X, tiles.Left, tiles.Right - 1), Math.Clamp(stand.Y, tiles.Top, tiles.Bottom - 1)).Center;
                var delta = aim - stand.Center;
                int face = Math.Abs(delta.X) > Math.Abs(delta.Y) ? delta.X > 0 ? 1 : 3 : delta.Y > 0 ? 2 : 0;
                if (!goals.TryGetValue(stand, out var plans))
                    goals[stand] = plans = new();
                plans.Add(new(target, stand, aim, face));
            }
            for (int x = tiles.Left; x < tiles.Right; x++)
            {
                Add(new(x, tiles.Top - 1));
                Add(new(x, tiles.Bottom));
            }
            for (int y = tiles.Top; y < tiles.Bottom; y++)
            {
                Add(new(tiles.Left - 1, y));
                Add(new(tiles.Right, y));
            }
        }
    }
    public void Step(int budget = 160)
    {
        if (Finished)
            return;
        for (int i = 0; i < budget && frontier.Count > 0; i++)
        {
            frontier.TryDequeue(out var at, out var priority);
            int cost = priority.Cost;
            if (distance[at] != cost || turns[at] != priority.Turns)
                continue;
            if (batch && firstGoalCost >= 0 && cost > firstGoalCost + lookahead * 10)
            {
                Complete();
                return;
            }
            if (goals.TryGetValue(at, out var plans))
                foreach (var goal in plans)
                {
                    if (!batch)
                    {
                        Result = goal;
                        Finished = true;
                        return;
                    }
                    if (firstGoalCost < 0)
                        firstGoalCost = cost;
                    // Batch coverage is compared only within scythe work, never against unrelated
                    // one-target actions like petting, milk, watering or collecting a machine.
                    if (goal.Target.Kind == ActionKind.Water && goal.Target.Entity is not WaterSource && goal.Hits is not null)
                    {
                        double value = WorkCost(cost / 10d, 8 + goal.Power * 3, goal.Coverage);
                        if (value < waterScore)
                        {
                            waterScore = value;
                            nearestWater = goal;
                            waterCost = cost;
                        }
                        continue;
                    }
                    if (goal.Target.Mode != ToolMode.Scythe)
                    {
                        if (cost < nearestOtherCost)
                        {
                            nearestOtherCost = cost;
                            nearestOther = goal;
                        }
                        continue;
                    }
                    // A full native swing costs more than a few walking tiles. Avoid cheap edge swings
                    // that leave a nearby cluster to be approached and swung at again.
                    double score = WorkCost(cost / 10d, swingCost, goal.Coverage);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        Result = goal;
                    }
                    if (goal.Hits is not null)
                        reached.Add((goal, cost));
                }
            foreach (var direction in diagonal ? PathGeometry.Neighbors : Cell.Directions)
            {
                var next = at.Add(direction);
                if (!PathGeometry.OpenCorner(at, direction, CanWalk))
                    continue;
                int? step = CanWalk(next) ? PathGeometry.Cost(direction) : PathGeometry.Diagonal(direction) ? null : clearanceCost?.Invoke(next) * 10;
                if (step is null)
                    continue;
                int total = cost + step.Value;
                var from = previous[at];
                int bend = turns[at] + (at != start && (at.X - from.X != direction.X || at.Y - from.Y != direction.Y) ? 1 : 0);
                if (distance.TryGetValue(next, out int old) && (old < total || old == total && turns[next] <= bend))
                    continue;
                previous[next] = at;
                distance[next] = total;
                turns[next] = bend;
                frontier.Enqueue(next, (total, bend, enqueueOrder++));
            }
            if (previous.Count >= 32768)
            {
                Complete();
                return;
            }
        }
        if (frontier.Count == 0)
            Complete();
    }
    private void Complete()
    {
        Finished = true;
        if (nearestWater is not null && (nearestOther is null || waterCost + 20 < nearestOtherCost))
        {
            nearestOther = nearestWater;
            nearestOtherCost = waterCost;
        }
        if (Result is null)
        {
            Result = nearestOther;
            return;
        }
        if (Result.Target.Mode != ToolMode.Scythe || scytheTargets < 2)
        {
            ChooseNearbyWork();
            return;
        }
        // Bounded two-swing lookahead: account for the leftover shape, not just the first swing.
        // Only already reachable stands are considered; the next route is still verified afresh.
        // Keep distinct coverage sets: many equivalent stances around the first cluster must
        // not crowd all useful second swings out of the bounded lookahead.
        var shortlist = new List<(Approach Plan, int Cost)>();
        foreach (var candidate in reached.OrderBy(p => WorkCost(p.Cost / 10d, swingCost, p.Plan.Coverage)))
        {
            if (shortlist.Any(p => ReferenceEquals(p.Plan.Target.Tool, candidate.Plan.Target.Tool) && p.Plan.Hits!.SetEquals(candidate.Plan.Hits!)))
                continue;
            shortlist.Add(candidate);
            if (shortlist.Count == 64) break;
        }
        double best = double.MaxValue;
        Approach? chosen = null;
        foreach (var (first, cost) in shortlist)
        {
            if (first.Hits is null)
                continue;
            if (first.Coverage == scytheTargets)
            {
                double score = WorkCost(cost / 10d, swingCost, scytheTargets);
                if (score < best)
                {
                    best = score;
                    chosen = first;
                }
                continue;
            }
            foreach (var (second, _) in shortlist)
            {
                if (second.Hits is null || !ReferenceEquals(first.Target.Tool, second.Target.Tool))
                    continue;
                int extra = second.Hits.Count(t => !first.Hits.Contains(t));
                if (extra == 0)
                    continue;
                int covered = first.Coverage + extra, left = scytheTargets - covered;
                double travel = diagonal ? PathGeometry.Distance(first.Stand, second.Stand) : Math.Abs(first.Stand.X - second.Stand.X) + Math.Abs(first.Stand.Y - second.Stand.Y);
                double score = WorkCost(cost / 10d, swingCost * 2 + (first.Coverage == 1 ? 12 : 0) + (extra == 1 ? 12 : 0) + (left == 1 ? 16 : 0), covered) + travel / covered;
                if (score < best)
                {
                    best = score;
                    chosen = first;
                }
            }
        }
        if (chosen is not null)
            Result = chosen;
        ChooseNearbyWork();
    }
    private void ChooseNearbyWork()
    {
        if (nearestOther is null || Result is null)
            return;
        if (Result.Target.Mode == ToolMode.Scythe)
        {
            // A nearby one-tile task may interrupt a sweep, but it must not starve a
            // pending scythe cluster. This keeps mixed clearings responsive without
            // leaving the final weed/grass target until the end of the queue.
            const int maxDeferredNonScythe = 1;
            if (scytheDeferrals >= maxDeferredNonScythe)
                return;

            // A dense native sweep already amortizes its action cost. Only replace it
            // when the other task is strictly closer; otherwise keep the batch together.
            if (Result.Coverage >= 2 && nearestOtherCost + 10 > distance[Result.Stand])
                return;
        }
        // An adjacent task wins over a dense patch. After repeated sweeps, allow a small
        // additional detour so nearby other work doesn't wait for the entire weed layer.
        int allowance = 2 + Math.Min(3, scytheStreak) * 2;
        if (nearestOtherCost <= distance[Result.Stand] + allowance * 10)
            Result = nearestOther;
    }
    // Coverage can amortize swings, but shouldn't make a long detour almost free.
    private static double WorkCost(double walkingTiles, double actionCost, int coverage)
        => (walkingTiles + actionCost) / Math.Max(1, coverage) + Math.Max(0, walkingTiles - 6) * .25;
    private bool CanWalk(Cell p)
    {
        if (!walk.TryGetValue(p, out bool allowed))
            walk[p] = allowed = canStand(p);
        return allowed;
    }
    public List<Cell> Path()
    {
        var route = new List<Cell>();
        if (Result is null)
            return route;
        for (var p = Result.Stand; p != start; p = previous[p])
            route.Add(p);
        route.Reverse();
        return route;
    }
}
