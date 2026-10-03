namespace Sznine.BehaviorAutomation;

/// <summary>Two actual distance fields compare P-S-B against P-B inside an immutable main-route corridor.</summary>
public sealed class OpportunitySearch : IDisposable
{
    private readonly IEnumerator<bool> steps;
    public bool Finished { get; private set; }
    public Approach? Result { get; private set; }
    public List<Cell> Path { get; private set; } = new();
    public int ChosenCost { get; private set; } = int.MaxValue;
    public int ExtraCost { get; private set; }
    public static HashSet<Cell> Corridor(Cell start, IEnumerable<Cell> path)
    {
        var result = new HashSet<Cell>();
        foreach (var tile in path.Prepend(start))
        {
            result.Add(tile);
            foreach (var d in Cell.Directions) result.Add(tile.Add(d));
        }
        return result;
    }
    public OpportunitySearch(Cell start, Approach main, int mainCost, ISet<Cell> corridor,
        IEnumerable<Approach?> singles, Func<Cell, bool> canStand, ModConfig config)
        => steps = Plan(start, main, mainCost, corridor, singles, canStand, config).GetEnumerator();

    private IEnumerable<bool> Plan(Cell start, Approach main, int mainCost, ISet<Cell> corridor,
        IEnumerable<Approach?> singles, Func<Cell, bool> canStand, ModConfig config)
    {
        var goals = new Dictionary<Cell, List<Approach>>();
        foreach (var plan in singles)
        {
            if (plan is not null && corridor.Contains(plan.Stand) && !ReferenceEquals(main.Target, plan.Target)
                && main.Hits?.Contains(plan.Target) != true)
            {
                if (!goals.TryGetValue(plan.Stand, out var list)) goals[plan.Stand] = list = new();
                list.Add(plan);
            }
            yield return false;
        }
        if (goals.Count == 0) yield break;
        bool Walk(Cell p) => corridor.Contains(p) && canStand(p);
        var back = new TileSearch(main.Stand, Walk, config.AllowDiagonalMovement);
        while (!back.Exhausted && back.NextCost <= mainCost + WorkRules.OpportunityDetourCost)
        { back.Advance(out _, out _); yield return false; }
        var forward = new TileSearch(start, Walk, config.AllowDiagonalMovement);
        while (!forward.Exhausted && forward.NextCost < mainCost && forward.NextCost <= ChosenCost)
        {
            yield return false;
            if (!forward.Advance(out var at, out int cost) || !goals.TryGetValue(at, out var plans)
                || !back.TryCost(at, out int rest)) continue;
            int extra = cost + rest - mainCost;
            if (extra > WorkRules.OpportunityDetourCost) continue;
            foreach (var plan in plans)
                if (Result is null || cost < ChosenCost || cost == ChosenCost && extra < ExtraCost)
                { Result = plan; ChosenCost = cost; ExtraCost = Math.Max(0, extra); }
        }
        if (Result is not null) Path = forward.Path(Result.Stand);
    }
    public void Step()
    {
        if (Finished) return;
        var slice = new PlanningSlice();
        for (int i = 0; i < 256 && slice.HasTime; i++)
            if (!steps.MoveNext()) { Finished = true; steps.Dispose(); return; }
    }
    public void Dispose() => steps.Dispose();
}
