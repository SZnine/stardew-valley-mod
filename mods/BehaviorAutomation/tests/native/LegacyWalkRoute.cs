using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using Sznine.BehaviorAutomation;
namespace BehaviorProbe;
public sealed class LegacyWalkRoute : PathFindController
{
    private readonly Farmer owner; private readonly GameLocation map; private Vector2 previous; private double still;
    private readonly Cell destination;
    private readonly bool centerEnd;
    public bool Failed
    {
        get; private set;
    }
    public bool Finished
    {
        get; private set;
    }
    public LegacyWalkRoute(Farmer owner, GameLocation map, List<Cell> route, bool centerEnd = false)
        : base(new Stack<Point>(route.AsEnumerable().Reverse().Select(p => new Point(p.X, p.Y))), map, owner, new Point(route[^1].X, route[^1].Y))
    {
        this.owner = owner;
        this.map = map;
        this.centerEnd = centerEnd;
        destination = route[^1];
        previous = owner.Position;
        nonDestructivePathing = false;
        finalFacingDirection = -1;
    }
    public override bool update(GameTime time)
    {
        if (Finished)
            return true;
        if (owner.currentLocation != map || owner.UsingTool || !owner.CanMove)
            return End(true);
        Rectangle tile = default;
        var body = owner.GetBoundingBox();
        // Consume reached intermediate tiles without the base controller's stop/return frame.
        // Use its arrival envelope, then retain the native collision-aware movement call.
        while (pathToEndPoint.Count > 0)
        {
            var p = pathToEndPoint.Peek();
            tile = new(p.X * 64, p.Y * 64, 64, 64);
            tile.Inflate(-2, 0);
            if ((tile.Contains(body) || body.Width > tile.Width && tile.Contains(body.Center)) && tile.Bottom - body.Bottom >= 2)
            {
                pathToEndPoint.Pop();
                continue;
            }
            if (!WorldTargets.CanStand(map, owner, new(p.X, p.Y)))
            {
                owner.movementDirections.Clear();
                still += time.ElapsedGameTime.TotalMilliseconds;
                return still > 350 && End(true);
            }
            break;
        }
        if (pathToEndPoint.Count == 0)
            return CenterOrFinish(time);
        owner.movementDirections.Clear();
        if (body.Left < tile.Left && body.Right < tile.Right)
            owner.SetMovingRight(true);
        else if (body.Right > tile.Right && body.Left > tile.Left)
            owner.SetMovingLeft(true);
        else if (body.Top <= tile.Top)
            owner.SetMovingDown(true);
        else if (body.Bottom >= tile.Bottom - 2)
            owner.SetMovingUp(true);
        owner.MovePosition(time, Game1.viewport, map);
        still = Vector2.DistanceSquared(previous, owner.Position) < .01f ? still + time.ElapsedGameTime.TotalMilliseconds : 0;
        previous = owner.Position;
        if (still > 1200)
            return End(true);
        return pathToEndPoint.Count == 0 && CenterOrFinish(time);
    }
    private bool CenterOrFinish(GameTime time)
    {
        if (!centerEnd)
            return End(false);
        var delta = destination.Center - owner.GetBoundingBox().Center.ToVector2();
        float tolerance = Math.Max(2, owner.getMovementSpeed() / 2f + 1);
        if (Math.Abs(delta.X) <= tolerance && Math.Abs(delta.Y) <= tolerance)
            return End(false);
        if (!WorldTargets.CanStand(map, owner, destination))
            return End(true);
        owner.movementDirections.Clear();
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
        still = Vector2.DistanceSquared(before, owner.Position) < .01f ? still + time.ElapsedGameTime.TotalMilliseconds : 0;
        return still > 1200 && End(true);
    }
    private bool End(bool failed)
    {
        Failed = failed;
        Finished = true;
        owner.Halt();
        return true;
    }
}
