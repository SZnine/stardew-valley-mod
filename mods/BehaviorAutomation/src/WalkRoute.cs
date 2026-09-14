using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

namespace Sznine.BehaviorAutomation;

/// <summary>Follow stable tile centers through native collision and movement; never set world position.</summary>
public sealed class WalkRoute : PathFindController
{
    private readonly Farmer owner;
    private readonly GameLocation map;
    private double blockedMilliseconds;
    public bool Failed
    {
        get; private set;
    }
    public bool Finished
    {
        get; private set;
    }

    public WalkRoute(Farmer owner, GameLocation map, List<Cell> route)
        : base(new Stack<Point>(route.AsEnumerable().Reverse().Select(p => new Point(p.X, p.Y))), map, owner, new(route[^1].X, route[^1].Y))
    {
        this.owner = owner;
        this.map = map;
        nonDestructivePathing = false;
        finalFacingDirection = -1;
    }

    public override bool update(GameTime time)
    {
        if (Finished)
            return true;
        if (owner.currentLocation != map || owner.UsingTool || !owner.CanMove)
            return End(true);
        owner.movementDirections.Clear();
        // The old full-body containment window was narrower than a long frame's movement step.
        // A speed-aware center tolerance cannot oscillate forever across that narrow window.
        float tolerance = Math.Max(2, owner.getMovementSpeed() / 2 + 1);
        var center = owner.GetBoundingBox().Center.ToVector2();
        while (pathToEndPoint.Count > 0)
        {
            var point = pathToEndPoint.Peek();
            var cell = new Cell(point.X, point.Y);
            var delta = cell.Center - center;
            if (Math.Abs(delta.X) <= tolerance && Math.Abs(delta.Y) <= tolerance)
            {
                pathToEndPoint.Pop();
                continue;
            }
            if (!WorldTargets.CanStand(map, owner, cell))
            {
                blockedMilliseconds += time.ElapsedGameTime.TotalMilliseconds;
                return blockedMilliseconds > 350 && End(true);
            }
            if (Math.Abs(delta.X) > tolerance)
            {
                if (delta.X > 0)
                    owner.SetMovingRight(true);
                else
                    owner.SetMovingLeft(true);
            }
            else if (delta.Y > 0)
                owner.SetMovingDown(true);
            else
                owner.SetMovingUp(true);
            var before = owner.Position;
            owner.MovePosition(time, Game1.viewport, map);
            blockedMilliseconds = Vector2.DistanceSquared(before, owner.Position) < .01f ? blockedMilliseconds + time.ElapsedGameTime.TotalMilliseconds : 0;
            return blockedMilliseconds > 1200 && End(true);
        }
        return End(false);
    }

    private bool End(bool failed)
    {
        Failed = failed;
        Finished = true;
        owner.Halt();
        return true;
    }
}
