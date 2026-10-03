namespace Sznine.BehaviorAutomation;

/// <summary>Reachable stances for one strategy. Mixed-work arbitration is deliberately outside this search.</summary>
public sealed class RouteSearch : IDisposable
{
    private readonly TileSearch grid;
    private readonly IEnumerator<bool> planning;
    private readonly Dictionary<Cell, List<Approach>> goals = new();
    private readonly List<(Approach Plan, int Cost)> reached = new();
    private readonly WorkStyle style;
    private readonly WorkTarget? front;
    private readonly bool diagonal;
    private readonly int lookahead, actionCost, targetCount;
    private double bestScore = double.MaxValue;
    public bool Finished { get; private set; }
    public Approach? Result { get; private set; }
    public int Visited => grid.Visited;
    public int TravelCost => Result is null ? int.MaxValue : grid.Cost(Result.Stand);

    public RouteSearch(Cell start, IEnumerable<Approach?> plans, Func<Cell, bool> canStand,
        ModConfig? settings = null, Func<Cell, int?>? clearanceCost = null,
        WorkStyle style = WorkStyle.Point, int targetCount = 0, WorkTarget? front = null)
    {
        this.style = style;
        this.targetCount = targetCount;
        this.front = front;
        diagonal = settings?.AllowDiagonalMovement ?? true;
        lookahead = settings?.ScytheSearchTiles ?? 12;
        actionCost = settings?.ScytheSwingCost ?? 16;
        grid = new(start, canStand, diagonal, clearanceCost);
        planning = Plan(plans).GetEnumerator();
    }

    private bool Leads(Approach plan) => front is null || ReferenceEquals(plan.Target, front) || plan.Hits?.Contains(front) == true;

    private IEnumerable<bool> Plan(IEnumerable<Approach?> source)
    {
        foreach (var plan in source)
        {
            if (plan is not null)
            {
                if (!goals.TryGetValue(plan.Stand, out var list)) goals[plan.Stand] = list = new();
                list.Add(plan);
            }
            yield return false;
        }
        if (goals.Count == 0) yield break;
        int firstCost = -1;
        while (!grid.Exhausted)
        {
            yield return false;
            if (!grid.Advance(out var at, out int cost)) continue;
            if (firstCost >= 0 && cost > firstCost + lookahead * 10) break;
            if (!goals.TryGetValue(at, out var plans)) continue;
            foreach (var plan in plans)
            {
                yield return false;
                bool leads = Leads(plan);
                if (leads)
                {
                    if (firstCost < 0) firstCost = cost;
                    if (style is WorkStyle.Point or WorkStyle.Repeated)
                    {
                        Result = plan;
                        yield break;
                    }
                    double score = WorkCost(cost / 10d, style == WorkStyle.Charged ? 8 + plan.Power * 3 : actionCost, plan.Coverage);
                    if (score < bestScore) { bestScore = score; Result = plan; }
                }
                if (style == WorkStyle.Sweep && plan.Hits is not null) reached.Add((plan, cost));
            }
        }
        if (style == WorkStyle.Sweep && Result is not null && targetCount > 1)
            foreach (bool step in RankSweep()) yield return step;
    }

    public void Step(int budget = 1024)
    {
        if (Finished) return;
        var slice = new PlanningSlice();
        for (int i = 0; i < budget && slice.HasTime; i++)
            if (!planning.MoveNext())
            {
                Finished = true;
                planning.Dispose();
                return;
            }
    }

    private IEnumerable<bool> RankSweep()
    {
        // Bounded two-swing lookahead: account for the leftover shape, not just the first swing.
        // Only already reachable stands are considered; the next route is still verified afresh.
        // Keep distinct coverage sets: many equivalent stances around the first cluster must
        // not crowd all useful second swings out of the bounded lookahead.
        var shortlist = new List<(Approach Plan, int Cost)>();
        foreach (var candidate in reached.OrderBy(p => WorkCost(p.Cost / 10d, actionCost, p.Plan.Coverage)))
        {
            yield return false;
            if (shortlist.Any(p => ReferenceEquals(p.Plan.Target.Tool, candidate.Plan.Target.Tool) && p.Plan.Hits!.SetEquals(candidate.Plan.Hits!)))
                continue;
            shortlist.Add(candidate);
            if (shortlist.Count == 64) break;
        }
        double best = double.MaxValue;
        Approach? chosen = null;
        foreach (var (first, cost) in shortlist)
        {
            if (first.Hits is null || !Leads(first))
                continue;
            if (first.Coverage == targetCount)
            {
                double score = WorkCost(cost / 10d, actionCost, targetCount);
                if (score < best)
                {
                    best = score;
                    chosen = first;
                }
                continue;
            }
            foreach (var (second, _) in shortlist)
            {
                yield return false;
                if (second.Hits is null || !ReferenceEquals(first.Target.Tool, second.Target.Tool))
                    continue;
                int extra = second.Hits.Count(t => !first.Hits.Contains(t));
                if (extra == 0)
                    continue;
                int covered = first.Coverage + extra, left = targetCount - covered;
                double travel = diagonal ? PathGeometry.Distance(first.Stand, second.Stand) : Math.Abs(first.Stand.X - second.Stand.X) + Math.Abs(first.Stand.Y - second.Stand.Y);
                double score = WorkCost(cost / 10d, actionCost * 2 + (first.Coverage == 1 ? 12 : 0) + (extra == 1 ? 12 : 0) + (left == 1 ? 16 : 0), covered) + travel / covered;
                if (score < best)
                {
                    best = score;
                    chosen = first;
                }
            }
        }
        if (chosen is not null)
            Result = chosen;
    }
    private static double WorkCost(double walk, double action, int coverage)
        => (walk + action) / Math.Max(1, coverage) + Math.Max(0, walk - 6) * .25;
    public List<Cell> Path() => Result is null ? new() : grid.Path(Result.Stand);
    public void Dispose() => planning.Dispose();
}
