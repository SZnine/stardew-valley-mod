using System.Diagnostics;

namespace Sznine.BehaviorAutomation;

/// <summary>Shared incremental Dijkstra traversal for work, clearance and refill routes.</summary>
internal sealed class TileSearch
{
    private readonly PriorityQueue<Cell, (int Cost, int Turns, long Order)> frontier = new();
    private readonly Dictionary<Cell, Cell> previous = new();
    private readonly Dictionary<Cell, int> distance = new(), turns = new();
    private readonly Dictionary<Cell, bool> walk = new();
    private readonly Func<Cell, bool> canStand;
    private readonly Func<Cell, int?>? clearanceCost;
    private readonly Cell start;
    private readonly bool diagonal;
    private long order;
    public int Visited => previous.Count;
    public bool Exhausted => frontier.Count == 0 || previous.Count >= 32768;

    public TileSearch(Cell start, Func<Cell, bool> canStand, bool diagonal, Func<Cell, int?>? clearanceCost = null)
    {
        this.start = start;
        this.canStand = canStand;
        this.diagonal = diagonal;
        this.clearanceCost = clearanceCost;
        frontier.Enqueue(start, (0, 0, order++));
        previous[start] = start;
        distance[start] = turns[start] = 0;
    }

    // One queue entry per call, including stale entries; callers own the frame budget.
    public bool Advance(out Cell at, out int cost)
    {
        frontier.TryDequeue(out at, out var priority);
        cost = priority.Cost;
        if (distance[at] != cost || turns[at] != priority.Turns) return false;
        foreach (var direction in diagonal ? PathGeometry.Neighbors : Cell.Directions)
        {
            var next = at.Add(direction);
            if (!PathGeometry.OpenCorner(at, direction, CanWalk)) continue;
            int? step = CanWalk(next) ? PathGeometry.Cost(direction)
                : PathGeometry.Diagonal(direction) ? null : clearanceCost?.Invoke(next) * 10;
            if (step is null) continue;
            int total = cost + step.Value;
            var from = previous[at];
            int bend = turns[at] + (at != start && (at.X - from.X != direction.X || at.Y - from.Y != direction.Y) ? 1 : 0);
            if (distance.TryGetValue(next, out int old) && (old < total || old == total && turns[next] <= bend)) continue;
            previous[next] = at;
            distance[next] = total;
            turns[next] = bend;
            frontier.Enqueue(next, (total, bend, order++));
        }
        return true;
    }

    public int Cost(Cell at) => distance[at];
    public bool TryCost(Cell at, out int cost) => distance.TryGetValue(at, out cost);
    public int NextCost => frontier.TryPeek(out _, out var priority) ? priority.Cost : int.MaxValue;
    private bool CanWalk(Cell at)
    {
        if (!walk.TryGetValue(at, out bool allowed)) walk[at] = allowed = canStand(at);
        return allowed;
    }
    public List<Cell> Path(Cell end)
    {
        var result = new List<Cell>();
        for (var at = end; at != start; at = previous[at]) result.Add(at);
        result.Reverse();
        return result;
    }
}

// Cooperative time cap; a single native collision/compatibility callback cannot be preempted.
internal readonly struct PlanningSlice
{
    private readonly long started;
    public PlanningSlice() => started = Stopwatch.GetTimestamp();
    public bool HasTime => Stopwatch.GetTimestamp() - started < Stopwatch.Frequency / 500;
}
