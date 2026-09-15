using StardewValley;

namespace Sznine.BehaviorAutomation;

public sealed partial class WorkController
{
    private sealed class BuildingJourney
    {
        public WorkBoard OutsideBoard = null!;
        public BuildingDoor Door = null!;
        public GameLocation? Expected;
        public bool Entered;
        public double WaitMilliseconds;
    }
    private BuildingJourney? buildingJourney;

    private void UseBuildingDoor(Farmer who, BuildingDoor door, WorkTarget task)
    {
        who.Halt();
        StopPath();
        ReturnTool();
        if (door.Exit)
        {
            if (buildingJourney is null) { Board.Reject(task); return; }
            buildingJourney.Expected = door.Outside;
            buildingJourney.WaitMilliseconds = 0;
            State = "leaving-building";
            var warp = door.Inside.warps[0];
            // The route ends beside the real exit; use its existing native destination.
            Game1.warpFarmer(warp.TargetName, warp.TargetX, warp.TargetY, false);
            return;
        }
        if (buildingJourney is not null) { Board.Reject(task); return; }
        buildingJourney = new() { OutsideBoard = Board, Door = door, Expected = door.Inside };
        Board.Reject(task); // A selected building is visited once per rectangle.
        State = "entering-building";
        int slot = who.CurrentToolIndex;
        var held = who.Items[slot];
        bool used;
        try
        {
            who.Items[slot] = null!;
            who.faceDirection(0);
            // Native door handling still owns construction, locks, cabin ownership and entry warps.
            used = door.Building.doAction(door.Tile.Tile, who);
        }
        finally { who.Items[slot] = held!; }
        if (!used) { buildingJourney = null; Resume(); }
    }

    /// <summary>Keep work only for our requested doorway transition; other warps cancel normally.</summary>
    public bool OnWarped(GameLocation from, GameLocation to, Farmer who)
    {
        if (buildingJourney is not { } journey || !ReferenceEquals(journey.Expected, to)) return false;
        StopPath();
        ReturnTool();
        Active = null;
        if (!journey.Entered && from == journey.Door.Outside && to == journey.Door.Inside)
        {
            Board = journey.OutsideBoard.ForInterior(to, who, config());
            journey.Entered = true;
            journey.Expected = null;
        }
        else if (journey.Entered && from == journey.Door.Inside && to == journey.Door.Outside)
        {
            Board = journey.OutsideBoard;
            buildingJourney = null;
            Board.Prune(config());
            Board.Refresh(who, config());
        }
        else return false;
        refreshIn = 0;
        emptyPasses = 0;
        quietMilliseconds = 0;
        cooldown = 0;
        revision = -1;
        Resume();
        return true;
    }
}
