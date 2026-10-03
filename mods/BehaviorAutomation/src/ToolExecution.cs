using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
namespace Sznine.BehaviorAutomation;

public sealed partial class WorkController
{
    private void Start(Farmer who, Approach next)
    {
        if (next.Target.Mode == ToolMode.Scythe && next.Target.Tool is MeleeWeapon scythe)
        {
            // Native walking stops within a few pixels of the tile center. Recheck the actual sweep after arrival.
            var live = ScythePlanner.AtStand(Board.Jobs.Where(t => t.Mode == ToolMode.Scythe && ReferenceEquals(t.Tool, scythe)
                && (next.Hits?.Contains(t) ?? ReferenceEquals(next.Target, t)) && WorldTargets.Pending(who.currentLocation, t, config())
                && WorldTargets.Unable(t, who, config()) is null),
                who, scythe, next.Facing, next.Coverage > 1 ? next.Coverage : int.MaxValue);
            if (live is null)
            {
                Retry(next.Target);
                return;
            }
            next = live;
        }
        var t = next.Target;
        if (t.Entity is BuildingDoor door)
        {
            UseBuildingDoor(who, door, t);
            return;
        }
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
        Board.Coordinator.Started(next);
        double before = WorldTargets.Progress(t);
        if (t.Kind == ActionKind.Machine && t.Entity is StardewValley.Object collector && AnimalCare.Grabber(collector) is not null)
        {
            who.Halt();
            who.faceDirection(next.Facing);
            State = "acting";
            AnimalCare.CollectGrabber(collector, who);
            CompleteImmediate(who, t, before, 100);
            return;
        }
        if (t.Entity is FeedSpot feed)
        {
            who.Halt();
            who.faceDirection(next.Facing);
            State = "acting";
            if (!AnimalCare.Feed(who, feed))
                Pause("hay", true);
            CompleteImmediate(who, t, before, 100);
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
            CompleteImmediate(who, t, before, 100);
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
                Pause(t.Mode switch { ToolMode.Place => "materials", ToolMode.Fertilizer => "fertilizer", _ => "seeds" }, true);
                return;
            }
            who.CurrentToolIndex = who.Items.IndexOf(stock);
            who.Halt();
            who.faceDirection(next.Facing);
            State = t.Mode switch { ToolMode.Place => "placing", ToolMode.Fertilizer => "fertilizing", _ => "planting" };
            // Native placement owns stack consumption and world state; the plan only selects the tile.
            if (t.Mode != ToolMode.Place && who.currentLocation.Objects.GetValueOrDefault(t.Origin.Tile) is IndoorPot pot)
            {
                if (pot.performObjectDropInAction(stock, false, who))
                    who.reduceActiveItemByOne();
            }
            else
                Utility.tryToPlaceItem(who.currentLocation, stock, (int)next.Aim.X, (int)next.Aim.Y);
            CompleteImmediate(who, t, before, 180);
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
            CompleteImmediate(who, t, before, 250);
            return;
        }
        who.toolPower.Value = 0;
        who.toolHold.Value = 0;
        Active = new()
        {
            Approach = next,
            Location = who.currentLocation,
            Before = WorldTargets.Progress(t),
            Generation = Board.Generation
        };
        who.BeginUsingTool();
    }
    private void CompleteImmediate(Farmer who, WorkTarget target, double before, int delay)
    {
        if (WorldTargets.Pending(who.currentLocation, target, config()) && Math.Abs(WorldTargets.Progress(target) - before) < .001)
        {
            if (++target.FailedActions >= 3)
            {
                Board.Reject(target);
                Pause("no-effect", true);
            }
        }
        else target.FailedActions = 0;
        Board.Prune(config());
        cooldown = delay;
    }

}
