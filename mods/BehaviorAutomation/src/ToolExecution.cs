using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
namespace Sznine.BehaviorAutomation;

public sealed partial class WorkController
{
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
        owner.FarmerSprite.StopAnimation();
        owner.FarmerSprite.PauseForSingleAnimation = false;
        owner.UsingTool = false;
        owner.canReleaseTool = false;
        owner.CanMove = !Game1.freezeControls && owner.freezePause <= 0 && !Game1.eventUp && !owner.isEating;
        owner.toolPower.Value = 0;
        owner.toolHold.Value = 0;
        owner.stopJittering();
        owner.lastClick = Vector2.Zero;
        Active = null;
        ReturnTool();
    }
    private void Start(Farmer who, Approach next)
    {
        if (next.Target.Mode == ToolMode.Scythe && next.Target.Tool is MeleeWeapon scythe)
        {
            // Native walking stops within a few pixels of the tile center. Recheck the actual sweep after arrival.
            var live = ScythePlanner.AtStand(Board.Jobs.Where(t => WorldTargets.Pending(who.currentLocation, t, config()) && WorldTargets.Unable(t, who, config()) is null),
                who, scythe, next.Facing, next.Coverage > 1 ? next.Coverage : int.MaxValue);
            if (live is null)
            {
                Retry(next.Target);
                return;
            }
            next = live;
        }
        var t = next.Target;
        if (t.Tool is WateringCan && !Watering.Valid(next, Board, who, config()))
        {
            Retry(t);
            return;
        }
        displayedTask = t;
        var reason = WorldTargets.Unable(t, who, config());
        if (reason is not null)
        {
            Pause(reason, true);
            return;
        }
        scytheStreak = t.Mode == ToolMode.Scythe ? Math.Min(3, scytheStreak + 1) : 0;
        if (t.Kind == ActionKind.Machine && t.Entity is StardewValley.Object collector && AnimalCare.Grabber(collector) is not null)
        {
            who.Halt();
            who.faceDirection(next.Facing);
            State = "acting";
            AnimalCare.CollectGrabber(collector, who);
            Board.Prune(config());
            cooldown = 100;
            return;
        }
        if (t.Entity is FeedSpot feed)
        {
            who.Halt();
            who.faceDirection(next.Facing);
            State = "acting";
            if (!AnimalCare.Feed(who, feed))
                Pause("hay", true);
            Board.Prune(config());
            cooldown = 100;
            return;
        }
        if (t.Kind == ActionKind.Pet && t.Entity is Character animal)
        {
            who.Halt();
            who.faceDirection(next.Facing);
            State = "acting";
            // Scoped empty hand prevents crackers, hats or butterfly powder being used by pet().
            int heldSlot = who.CurrentToolIndex;
            var held = who.Items[heldSlot];
            who.Items[heldSlot] = null!;
            try
            {
                if (animal is FarmAnimal farmAnimal)
                    farmAnimal.pet(who);
                else if (animal is Pet pet)
                    pet.checkAction(who, who.currentLocation);
            }
            finally { who.Items[heldSlot] = held!; }
            if (WorldTargets.Pending(who.currentLocation, t, config()) && ++t.FailedActions >= 3)
            {
                Board.Reject(t);
                Pause("no-effect", true);
            }
            Board.Prune(config());
            cooldown = 100;
            return;
        }
        if (t.Tool is { } needed && !who.Items.Contains(needed))
        {
            loan = t.Storage is { } source ? ToolLoan.Borrow(source, who) : null;
            if (loan is null)
            {
                Pause("missing-tool", true);
                return;
            }
        }
        if (t.Material is { } template)
        {
            if (!(t.Mode == ToolMode.Place ? Placement.CanPlace(who.currentLocation, template, t.Origin) : Planting.CanPlant(who.currentLocation, template, t.Origin, config())))
            {
                Board.Reject(t);
                return;
            }
            var stock = Placement.FindStock(who, template);
            if (stock is null)
            {
                Pause(t.Mode == ToolMode.Place ? "materials" : "seeds", true);
                return;
            }
            who.CurrentToolIndex = who.Items.IndexOf(stock);
            who.Halt();
            who.faceDirection(next.Facing);
            State = t.Mode == ToolMode.Place ? "placing" : "planting";
            // Native placement owns stack consumption and world state; the plan only selects the tile.
            if (t.Mode != ToolMode.Place && who.currentLocation.Objects.GetValueOrDefault(t.Origin.Tile) is IndoorPot pot)
            {
                if (pot.performObjectDropInAction(stock, false, who))
                    who.reduceActiveItemByOne();
            }
            else
                Utility.tryToPlaceItem(who.currentLocation, stock, (int)next.Aim.X, (int)next.Aim.Y);
            if (WorldTargets.Pending(who.currentLocation, t, config()) && ++t.FailedActions >= 3)
            {
                Board.Reject(t);
                Pause("no-effect", true);
            }
            Board.Prune(config());
            cooldown = 180;
            return;
        }
        int slot = t.Tool is null ? ToolStorage.EmptySlot(who) : who.Items.IndexOf(t.Tool);
        if (slot < 0)
        {
            Pause(t.Tool is null ? "empty-slot" : "missing-tool", true);
            return;
        }
        who.CurrentToolIndex = slot;
        who.Halt();
        who.faceDirection(next.Facing);
        who.lastClick = next.Aim;
        State = "acting";
        if (t.Mode == ToolMode.Hand)
        {
            switch (t.Entity)
            {
                case FarmAnimal a:
                    a.pet(who);
                    break;
                case HoeDirt soil:
                    soil.performUseAction(t.Origin.Tile);
                    break;
                case FruitTree fruit:
                    fruit.performUseAction(t.Origin.Tile);
                    break;
                case Bush bush:
                    bush.performUseAction(t.Origin.Tile);
                    break;
                default:
                    who.currentLocation.checkAction(new xTile.Dimensions.Location(t.Origin.X, t.Origin.Y), Game1.viewport, who);
                    break;
            }
            if (WorldTargets.Pending(who.currentLocation, t, config()) && ++t.FailedActions >= 3)
            {
                Board.Reject(t);
                Pause("no-effect", true);
            }
            Board.Prune(config());
            cooldown = 250;
            return;
        }
        who.toolPower.Value = 0;
        who.toolHold.Value = 0;
        Active = new()
        {
            Approach = next,
            Before = WorldTargets.Progress(t),
            Generation = Board.Generation
        };
        who.BeginUsingTool();
    }
}
