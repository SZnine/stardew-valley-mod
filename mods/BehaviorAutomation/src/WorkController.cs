using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

public sealed class Operation
{
    public Approach Approach = null!;
    public double Elapsed, Before, Charge;
    public bool SawUsing, Released, ImpactApproved;
    public int Generation;
    public readonly HashSet<WorkTarget> Gather = new();
}

public sealed partial class WorkController
{
    public WorkBoard Board { get; private set; } = new();
    private readonly Func<ModConfig> config;
    private readonly Action<string> notice;
    private Farmer? owner;
    private RouteSearch? search;
    private RefillSearch? refillSearch;
    private WalkRoute? walk;
    private Approach? approach;
    private int revision;
    private List<WorkTarget>? routeCandidates;
    private int scytheStreak;
    // Number of non-scythe operations completed while scythe work remained pending.
    // It is a small starvation guard, not a global tool phase.
    private int scytheDeferrals;
    private double routeAge;
    private double cooldown;
    private double refreshIn;
    private int emptyPasses;
    private double quietMilliseconds;
    private WorkTarget? displayedTask;
    private ToolLoan? loan;
    private ObstaclePlanner? clearance;
    private readonly HashSet<object> failedObstacles = new(ReferenceEqualityComparer.Instance);
    public Operation? Active
    {
        get; private set;
    }
    public bool Paused
    {
        get; private set;
    }
    public string State { get; private set; } = "idle";
    public bool Editing
    {
        get; set;
    }
    public bool ManualMovement
    {
        get; private set;
    }
    private double movementMilliseconds;
    public bool HasSession => buildingJourney is not null || Board.HasSelection && State != "idle";
    public WorkTarget? DisplayedTask => Active is { } active && active.Generation == Board.Generation ? active.Approach.Target : approach?.Target ?? (displayedTask is { } shown && (Board.Jobs.Contains(shown) || Board.Jobs.Count == 0) ? shown : Board.Jobs.OrderBy(t => t.Group).FirstOrDefault());
    public bool OwnsSession => Editing || Active is not null || !Paused && HasSession;
    public WorkController(Func<ModConfig> config, Action<string> notice)
    {
        this.config = config;
        this.notice = notice;
    }
    public void Pause(string reason = "paused", bool notify = false)
    {
        Paused = true;
        State = reason;
        StopPath();
        CancelCharge();
        if (notify)
            notice(reason);
    }
    public void Resume()
    {
        if (!Board.HasSelection)
        {
            Clear();
            return;
        }
        Paused = false;
        State = "planning";
        refreshIn = 0;
        emptyPasses = 0;
        quietMilliseconds = 0;
        displayedTask = null;
        StopPath();
    }
    public void Clear()
    {
        StopPath();
        CancelCharge();
        Board.Clear();
        if (buildingJourney is { } journey)
        {
            journey.OutsideBoard.Clear();
            Board = journey.OutsideBoard;
            buildingJourney = null;
        }
        failedObstacles.Clear();
        scytheStreak = 0;
        scytheDeferrals = 0;
        Paused = false;
        ManualMovement = false;
        movementMilliseconds = 0;
        State = "idle";
        displayedTask = null;
        refreshIn = 0;
        emptyPasses = 0;
        quietMilliseconds = 0;
        if (owner?.UsingTool != true)
            ReturnTool();
    }
    public void ObserveMovement(Farmer who, bool down, double milliseconds)
    {
        owner = who;
        if (!down)
        {
            if (ManualMovement)
            {
                refreshIn = 0;
                quietMilliseconds = 0;
            }
            ManualMovement = false;
            movementMilliseconds = 0;
            return;
        }
        if (!Board.HasSelection)
            return;
        if (!ManualMovement)
        {
            StopPath();
            CancelCharge();
        }
        ManualMovement = true;
        movementMilliseconds += Math.Clamp(milliseconds, 0, 100);
        if (movementMilliseconds >= config().MovementCancelSeconds * 1000d)
            Clear();
    }
    public void Abandon()
    {
        Clear();
        Active = null;
        Editing = false;
        owner = null;
    }
    private void StopPath()
    {
        if (owner is not null && walk is not null && ReferenceEquals(owner.controller, walk))
        {
            owner.controller = null;
            owner.Halt();
        }
        walk = null;
        search = null;
        refillSearch = null;
        approach = null;
        clearance = null;
        routeCandidates = null;
        routeAge = 0;
    }
    internal bool OwnsWalking(Farmer who) => ReferenceEquals(who, owner) && walk is { Finished: false }
        && ReferenceEquals(who.controller, walk) && Board.HasSelection && !Paused && !Editing && !ManualMovement;
    private void ReturnTool()
    {
        if (loan is not { } borrowed)
            return;
        loan = null;
        if (!borrowed.Return())
        {
            notice("tool-kept");
            return;
        }
        // A replacement shortcut can see a loaned tool in the backpack before the old animation ends.
        // Keep the new plan's reference usable after that exact tool returns to its source chest.
        foreach (var target in Board.Jobs.Where(t => ReferenceEquals(t.Tool, borrowed.Source.Tool)))
            target.Storage = borrowed.Source;
    }
    public bool DeferGather(object entity, Tool? tool)
    {
        if (Active is not { } op || op.Generation != Board.Generation || tool is null || tool != op.Approach.Target.Tool || owner?.currentLocation != Board.Location)
            return false;
        var target = Board.Jobs.FirstOrDefault(t => t.Mode == ToolMode.Scythe &&
            (ReferenceEquals(entity, t.Entity) || t.Entity is IndoorPot pot && ReferenceEquals(entity, pot.hoeDirt.Value)));
        if (target is null || !WorldTargets.NeedsGather(target, tool) || !WorldTargets.Pending(Board.Location!, target, config()))
            return false;
        op.Gather.Add(target);
        return true;
    }
    public bool TryToolTarget(Character who, out Vector2 aim)
    {
        aim = default;
        if (Active is not { } op || op.Generation != Board.Generation || !ReferenceEquals(owner, who) || owner is null || owner.currentLocation != Board.Location || !ReferenceEquals(owner.CurrentTool, op.Approach.Target.Tool))
            return false;
        aim = op.Approach.Aim;
        return true;
    }
    public bool AllowTool(Tool tool, Farmer who, GameLocation map)
    {
        if (Active is not { } op || !ReferenceEquals(owner, who) || !ReferenceEquals(op.Approach.Target.Tool, tool))
            return true;
        if (map != Board.Location || op.Generation != Board.Generation)
            return false;
        bool Approved(bool allowed)
        {
            op.ImpactApproved = allowed;
            return allowed;
        }
        if (tool is WateringCan)
            return Approved(Watering.Valid(op.Approach, Board, who, config()) && who.toolPower.Value == op.Approach.Power);
        if (op.Approach.Target.Mode == ToolMode.Scythe)
            return Approved((op.Approach.Hits ?? new() { op.Approach.Target }).Any(t => Board.Jobs.Contains(t)
                && WorldTargets.Pending(map, t, config()) && WorldTargets.Unable(t, who, config()) is null));
        return Approved(WorldTargets.Pending(map, op.Approach.Target, config()) && WorldTargets.Unable(op.Approach.Target, who, config()) is null);
    }
    public bool AllowEntity(object entity, Tool? tool)
    {
        if (Active is not { } op || tool is null || !ReferenceEquals(op.Approach.Target.Tool, tool) || !ReferenceEquals(tool.getLastFarmerToUse(), owner))
            return true;
        if (owner?.currentLocation != Board.Location || op.Generation != Board.Generation || !op.ImpactApproved)
            return false;
        if (!ToolRequirements.Allows(entity, tool, duringImpact: true))
            return false;
        bool Matches(object selected) => ReferenceEquals(entity, selected) || selected is IndoorPot pot && ReferenceEquals(entity, pot.hoeDirt.Value);
        // Native DoFunction pays stamina before calling each entity. Affordability was checked
        // once in AllowTool; checking it again here would charge without applying the action.
        return Board.Jobs.Any(t => t.Mode == op.Approach.Target.Mode && Matches(t.Entity) && WorldTargets.Pending(Board.Location!, t, config()));
    }
    public FarmAnimal? SelectedAnimal(Tool tool) => Active is { } op && ReferenceEquals(tool, op.Approach.Target.Tool) ? op.Approach.Target.Entity as FarmAnimal : null;
    // Some harvest mods enter Crop.harvest directly. Guard the final native entry point too,
    // scoped to our exact scythe operation; never intercept ordinary manual harvesting.
    internal bool AllowHarvest(Crop crop, HoeDirt soil, int x, int y)
    {
        if (Active is not { } op || op.Approach.Target.Mode != ToolMode.Scythe || owner is null
            || owner != Game1.player || !ReferenceEquals(owner.CurrentTool, op.Approach.Target.Tool))
            return true;
        return op.Generation == Board.Generation && WorldTargets.HarvestReady(crop) && ReferenceEquals(soil.crop, crop)
            && Board.Location == owner.currentLocation && Board.Jobs.Any(t => t.Mode == ToolMode.Scythe
                && t.Kind == ActionKind.HarvestCrop && t.Origin == new Cell(x, y)
                && (ReferenceEquals(t.Entity, soil) || t.Entity is IndoorPot pot && ReferenceEquals(pot.hoeDirt.Value, soil))
                && WorldTargets.Pending(owner.currentLocation, t, config()));
    }
    public void Tick(Farmer who, double milliseconds, bool eligible)
    {
        owner = who;
        milliseconds = Math.Clamp(milliseconds, 0, 100);
        if (buildingJourney?.Expected is not null)
        {
            buildingJourney.WaitMilliseconds += milliseconds;
            if (buildingJourney.WaitMilliseconds > 5000) Clear();
            return;
        }
        if (config().Enabled && eligible && !Editing && !Paused && !ManualMovement && HasSession && Board.Location == who.currentLocation)
        {
            refreshIn -= milliseconds;
            if (refreshIn <= 0)
            {
                Board.Prune(config());
                Board.Refresh(who, config());
                refreshIn = config().RefreshIntervalSeconds * 1000d;
                emptyPasses = Board.Jobs.Count == 0 && Active is null ? emptyPasses + 1 : 0;
            }
            quietMilliseconds = Board.Jobs.Count > 0 || Active is not null ? 0 : quietMilliseconds + milliseconds;
        }
        if (Active is { } op)
        {
            op.Elapsed += milliseconds;
            op.SawUsing |= who.UsingTool;
            if (who.UsingTool && who.CurrentTool == op.Approach.Target.Tool && who.canReleaseTool && !op.Released && who.CurrentTool is WateringCan or Hoe)
            {
                if (op.Generation != Board.Generation || Paused || !eligible || Editing || ManualMovement)
                {
                    CancelCharge();
                    return;
                }
                AdvanceCharge(who, op, milliseconds);
            }
            if (!who.UsingTool && (op.SawUsing || op.Elapsed > 120))
            {
                var t = op.Approach.Target;
                if (op.Generation != Board.Generation)
                {
                    Active = null;
                    ReturnTool();
                    return;
                }
                // Keep the owned-operation harvest guard through deferred crop collection.
                try
                {
                    foreach (var gathered in op.Gather)
                        if (Board.Location == who.currentLocation && Board.Jobs.Contains(gathered) && WorldTargets.Pending(who.currentLocation, gathered, config()))
                            WorldTargets.Gather(who.currentLocation, who, gathered);
                }
                finally { Active = null; ReturnTool(); }
                if (Board.Location == who.currentLocation && WorldTargets.Pending(who.currentLocation, t, config()))
                {
                    if (Math.Abs(WorldTargets.Progress(t) - op.Before) < .001 && ++t.FailedActions >= 3)
                    {
                        Board.Reject(t);
                        if (t.IsObstacle)
                            failedObstacles.Add(t.Entity);
                        notice("no-effect");
                    }
                    else if (Math.Abs(WorldTargets.Progress(t) - op.Before) >= .001)
                        t.FailedActions = 0;
                }
                Board.Prune(config());
                Board.Refresh(who, config());
                cooldown = t.Mode == ToolMode.Scythe ? 0 : 60;
                // Native trees expose the stump as soon as the trunk starts falling.
                if (t.Mode == ToolMode.Axe && t.Entity is Tree && Board.Jobs.Contains(t) && Cell.Of(who) == op.Approach.Stand && Math.Abs(WorldTargets.Progress(t) - op.Before) >= .001)
                {
                    approach = op.Approach;
                    cooldown = 0;
                }
            }
            else if (op.Elapsed > 12000 && !Paused)
                Pause("busy", true);
            return;
        }
        if (Board.Location is not null && who.currentLocation != Board.Location)
        {
            Clear();
            return;
        }
        if (!config().Enabled || !eligible || Editing || Paused || ManualMovement)
        {
            StopPath();
            return;
        }
        if (who.UsingTool || !who.CanMove || who.FarmerSprite.PauseForSingleAnimation)
            return;
        cooldown -= milliseconds;
        if (cooldown > 0)
            return;
        Board.Prune(config());
        if (Board.Jobs.Count == 0)
        {
            StopPath();
            if (State != "idle" && Board.HasSelection && (emptyPasses < 2 || quietMilliseconds < config().CompletionDelaySeconds * 1000d))
            {
                State = "checking";
                return;
            }
            if (buildingJourney is { Entered: true } journey)
            {
                Board.Jobs.Add((journey.Door with { Exit = true }).Target());
                State = "leaving-building";
                return;
            }
            if (State != "idle" && Board.Location is not null)
            {
                if (Board.LastSelectedItem is { } item && who.Items.Contains(item))
                    who.CurrentToolIndex = who.Items.IndexOf(item);
                else if (Board.LastSelectedItem is null)
                {
                    int empty = ToolStorage.EmptySlot(who);
                    if (empty >= 0)
                        who.CurrentToolIndex = empty;
                }
            }
            ReturnTool();
            if (Board.HasSelection)
                Board.Clear();
            State = "idle";
            displayedTask = null;
            return;
        }
        if (approach is { } planned && !Board.Jobs.Contains(planned.Target))
            StopPath();
        if (Board.Revision != revision)
        {
            // A new task doesn't invalidate a committed valid route. Refresh pending searches
            // only; explicit replacement/cancel and invalid goals still stop immediately.
            if (walk is null && approach is null)
            {
                search = null;
                clearance = null;
                routeCandidates = null;
            }
            revision = Board.Revision;
        }
        if (walk is not null)
        {
            routeAge += milliseconds;
            if (approach?.Target.Entity is Character && routeAge >= 350 && Vector2.DistanceSquared(approach.Aim, approach.Target.Aim) > 64 * 64)
            {
                StopPath();
                return;
            }
            if (!walk.Finished && ReferenceEquals(who.controller, walk))
            {
                State = "walking";
                return;
            }
            if (!walk.Finished || walk.Failed)
            {
                if (approach is { } failed)
                    Retry(failed.Target);
                else
                    StopPath();
                return;
            }
            if (ReferenceEquals(who.controller, walk))
                who.controller = null;
            walk = null;
        }
        if (who.controller is not null)
        {
            Pause("busy");
            return;
        }
        if (refillSearch is not null)
        {
            refillSearch.Step();
            if (!refillSearch.Finished)
                return;
            var result = refillSearch.Result;
            var refillPath = refillSearch.Path();
            refillSearch = null;
            if (result is null)
            {
                Pause("no-water-source", true);
                return;
            }
            Board.Jobs.Add(result.Target);
            approach = result;
            displayedTask = result.Target;
            if (refillPath.Count > 0)
            {
                walk = new(who, who.currentLocation, refillPath);
                routeAge = 0;
                who.controller = walk;
                State = "refill-walk";
            }
            return;
        }
        if (clearance is not null)
        {
            clearance.Step();
            if (!clearance.Finished)
                return;
            var obstacle = clearance.FirstObstacle();
            clearance = null;
            if (obstacle is not null)
            {
                obstacle.IsObstacle = true;
                obstacle.Group = Board.Jobs.Min(t => t.Group) - 1;
                if (!Board.Jobs.Any(t => ReferenceEquals(t.Entity, obstacle.Entity)))
                    Board.Jobs.Add(obstacle);
                return;
            }
            SkipUnreachable();
            return;
        }
        if (approach is { } next)
        {
            approach = null;
            search = null;
            if (!Board.Jobs.Contains(next.Target) || !WorldTargets.Pending(who.currentLocation, next.Target, config()))
                return;
            if (next.Target.Entity is Character && Vector2.Distance(next.Aim, next.Target.Aim) > 24)
            {
                StopPath();
                cooldown = 80;
                return;
            }
            if (Cell.Of(who) != next.Stand)
            {
                Retry(next.Target);
                return;
            }
            Start(who, next);
            return;
        }
        if (search is null)
        {
            var candidates = Board.Jobs.Any(t => t.IsObstacle) ? Board.Jobs.Where(t => t.IsObstacle).ToList() : Board.Jobs.ToList();
            string? blockedReason = null;
            foreach (var t in candidates.ToArray())
            {
                var reason = WorldTargets.Unable(t, who, config());
                if (reason is "upgrade" or "sleeping")
                {
                    Board.Reject(t);
                    candidates.Remove(t);
                    notice(reason);
                }
                else if (reason is not null)
                {
                    blockedReason ??= reason;
                    candidates.Remove(t);
                }
            }
            if (candidates.Count == 0)
            {
                var thirsty = Board.Jobs.FirstOrDefault(t => WorldTargets.Unable(t, who, config()) == "water");
                if (thirsty is not null && config().AutoRefillWateringCan)
                {
                    refillSearch = new(who, thirsty, config().AllowDiagonalMovement);
                    State = "refill-planning";
                    return;
                }
                if (blockedReason is not null)
                    Pause(blockedReason, true);
                return;
            }
            // Pattern reservations determine spacing, not travel order. Start at a reachable
            // nearby seed/floor tile instead of always walking to the rectangle's top-left.
            routeCandidates = candidates;
            search = new(Cell.Of(who), candidates, p => WorldTargets.CanStand(who.currentLocation, who, p), scytheFarmer: who, scytheStreak: scytheStreak, scytheDeferrals: scytheDeferrals, waterPlans: Board.Area is { } area ? Watering.Approaches(candidates, who, area, config()) : null, settings: config());
        }
        State = "planning";
        search.Step();
        if (!search.Finished)
            return;
        if (search.Result is null)
        {
            search = null;
            if (config().ClearObstacles && !Board.Jobs.Any(t => t.IsObstacle))
            {
                clearance = new(who.currentLocation, who, routeCandidates!, Board.ObstacleCandidates(who, config()), config(), failedObstacles);
                return;
            }
            SkipUnreachable();
            return;
        }
        approach = search.Result;
        displayedTask = approach.Target;
        var path = search.Path();
        search = null;
        bool center = approach.Target.Mode == ToolMode.Scythe;
        if (center && path.Count == 0)
            path.Add(approach.Stand);
        if (path.Count > 0)
        {
            walk = new(who, who.currentLocation, path);
            routeAge = 0;
            who.controller = walk;
            State = "walking";
        }
    }
    private void SkipUnreachable()
    {
        foreach (var t in (routeCandidates ?? new()).Where(Board.Jobs.Contains).ToArray())
        {
            if (t.IsObstacle)
                failedObstacles.Add(t.Entity);
            Board.Reject(t);
        }
        StopPath();
        notice("unreachable");
    }
    private void Retry(WorkTarget target)
    {
        StopPath();
        if (++target.FailedRoutes >= 3)
        {
            Board.Reject(target);
            if (target.IsObstacle)
                failedObstacles.Add(target.Entity);
            notice("unreachable");
        }
    }
}
