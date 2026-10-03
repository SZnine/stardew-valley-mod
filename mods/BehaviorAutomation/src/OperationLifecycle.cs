using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

public sealed class Operation
{
    public Approach Approach = null!;
    public GameLocation Location = null!;
    public double Elapsed, Before, Charge;
    public bool SawUsing, Released, ImpactApproved;
    public int Generation;
    public readonly HashSet<WorkTarget> Gather = new();
}

/// <summary>Ownership, completion, cancellation and recovery for one native tool action.</summary>
public sealed partial class WorkController
{
    private void TickOperation(Farmer who, Operation op, double milliseconds, bool eligible)
    {
        if (eligible && who.currentLocation == op.Location)
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
        bool timedOut = eligible && op.Elapsed > WorkRules.OperationTimeoutMilliseconds;
        if (timedOut)
            diagnostic?.Invoke($"Tool action timed out: {op.Approach.Target.Kind} at {op.Approach.Target.Origin.X},{op.Approach.Target.Origin.Y}; "
                + $"tool={op.Approach.Target.Tool?.QualifiedItemId}; using={who.UsingTool}; canMove={who.CanMove}; "
                + $"animation={who.FarmerSprite.CurrentFrame}/{who.FarmerSprite.PauseForSingleAnimation}; released={op.Released}; impact={op.ImpactApproved}.");
        if (timedOut && ReferenceEquals(who.CurrentTool, op.Approach.Target.Tool) && who.currentLocation == op.Location)
            ReleaseOwnedAnimation(who);
        if ((!who.UsingTool && who.CanMove && !who.FarmerSprite.PauseForSingleAnimation || timedOut)
            && (op.SawUsing || op.Elapsed > 120))
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
                if (timedOut || Math.Abs(WorldTargets.Progress(t) - op.Before) < .001 && ++t.FailedActions >= 3)
                {
                    if (!timedOut) diagnostic?.Invoke($"Tool action had no effect after {t.FailedActions} attempts: {t.Kind} at {t.Origin.X},{t.Origin.Y}.");
                    Board.Reject(t);
                    if (t.IsObstacle)
                        failedObstacles.Add(t.Entity);
                    notice("no-effect");
                    // A refill has no persistent world entity. Stop here so recreating a
                    // shore target cannot reset its failure count and loop forever.
                    if (t.Entity is WaterSource) Pause("no-water-source", true);
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
    }

    private void AdvanceCharge(Farmer who, Operation op, double milliseconds)
    {
        int power = op.Approach.Power;
        if (who.CurrentTool is not WateringCan || op.Approach.Target.Entity is WaterSource)
            power = 0;
        if (who.toolPower.Value >= power)
        {
            op.Released = true;
            who.EndUsingTool();
            return;
        }
        double interval = 600 * who.CurrentTool!.AnimationSpeedModifier;
        op.Charge += milliseconds;
        who.toolHoldStartTime.Value = (int)interval;
        who.toolHold.Value = (int)Math.Max(1, interval - op.Charge);
        if (op.Charge >= interval)
        {
            op.Charge -= interval;
            who.toolPowerIncrease();
        }
    }

    // Only cancel an owned, still-held charge. Released native swings finish through their normal callbacks.
    private void CancelCharge()
    {
        if (Active is not { Released: false } op || owner is null || !owner.UsingTool || !owner.canReleaseTool
            || !ReferenceEquals(owner.CurrentTool, op.Approach.Target.Tool))
            return;
        ReleaseOwnedAnimation(owner);
        Active = null;
        ReturnTool();
    }

    // Do not call forceCanMove/completelyStopAnimatingOrDoingAction: they also clear
    // global/event locks or queue EndUsingTool, which could execute a second impact.
    private static void ReleaseOwnedAnimation(Farmer owner)
    {
        owner.FarmerSprite.StopAnimation();
        owner.FarmerSprite.PauseForSingleAnimation = false;
        owner.UsingTool = false;
        owner.canReleaseTool = false;
        owner.CanMove = !Game1.freezeControls && owner.freezePause <= 0 && !Game1.eventUp && !owner.isEating;
        owner.toolPower.Value = 0;
        owner.toolHold.Value = 0;
        owner.stopJittering();
        owner.lastClick = Vector2.Zero;
    }
}
