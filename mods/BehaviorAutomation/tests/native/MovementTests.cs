using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void MovementTests()
    {
        Check("reproduce old narrow arrival oscillation and verify new route finishes", () =>
        {
            bool reproduced = false;
            var evidence = new List<object>();
            foreach (int milliseconds in new[] { 33, 50, 80 })
                foreach (int offset in new[] { 0, 3, 7, 11 })
                {
                    Reset();
                    Who.setRunning(true, true);
                    Who.Position += new Vector2(offset, 0);
                    var route = Enumerable.Range(21, 6).Select(x => new Cell(x, 20)).ToList();
                    var legacy = new LegacyWalkRoute(Who, Map, route);
                    Who.controller = legacy;
                    int oldReverse = 0;
                    for (int i = 0; i < 300 && !legacy.Finished; i++)
                    {
                        var before = Who.Position;
                        WalkFrame(legacy, milliseconds);
                        if (Who.Position.X < before.X - .01f)
                            oldReverse++;
                    }
                    bool oldFailed = !legacy.Finished || legacy.Failed || oldReverse > 1;
                    reproduced |= oldFailed;
                    Reset();
                    Who.setRunning(true, true);
                    Who.Position += new Vector2(offset, 0);
                    var current = new WalkRoute(Who, Map, route);
                    Who.controller = current;
                    int reverse = 0, frames = 0;
                    while (!current.Finished && frames++ < 300)
                    {
                        var before = Who.Position;
                        WalkFrame(current, milliseconds);
                        if (Who.Position.X < before.X - .01f)
                            reverse++;
                    }
                    Assert(current.Finished && !current.Failed && reverse == 0, $"New route oscillation: dt={milliseconds},offset={offset},reverse={reverse}");
                    evidence.Add(new
                    {
                        milliseconds,
                        offset,
                        oldReverse,
                        oldFailed,
                        newReverse = reverse,
                        newFrames = frames
                    });
                }
            Assert(reproduced, "Old fault was not reproduced; do not claim a confirmed cause");
            File.WriteAllText(Path.Combine(Evidence, "movement-before-after.json"), System.Text.Json.JsonSerializer.Serialize(evidence));
        });
        Check("native walking animation agrees with movement across all directions", () =>
        {
            foreach (int direction in Enumerable.Range(0, 4))
            {
                Reset();
                Who.setRunning(true, true);
                var start = Cell.Of(Who);
                var d = Cell.Directions[direction];
                var route = new WalkRoute(Who, Map, Enumerable.Range(1, 5).Select(i => new Cell(start.X + d.X * i, start.Y + d.Y * i)).ToList());
                Who.controller = route;
                int count = 0;
                while (!route.Finished && count++ < 300)
                {
                    var before = Who.Position;
                    WalkFrame(route, 16);
                    var change = Who.Position - before;
                    if (change.LengthSquared() > .01f && !route.Finished)
                    {
                        int actual = Math.Abs(change.X) > Math.Abs(change.Y) ? change.X > 0 ? 1 : 3 : change.Y > 0 ? 2 : 0;
                        bool diagonal = Math.Abs(change.X) > .01f && Math.Abs(change.Y) > .01f;
                        bool facing = diagonal ? Who.FacingDirection == (change.X > 0 ? 1 : 3) || Who.FacingDirection == (change.Y > 0 ? 2 : 0) : Who.FacingDirection == actual;
                        Assert(facing && !Who.UsingTool && !Who.FarmerSprite.PauseForSingleAnimation, "Animation/facing stuck during walking");
                    }
                }
                Assert(route.Finished && !route.Failed, "Directional walk did not finish");
            }
        });
        Check("right-angle native route remains stable at increased movement speed", () =>
        {
            Who.setRunning(true, true);
            Who.addedSpeed = 5;
            var path = Enumerable.Range(21, 7).Select(x => new Cell(x, 20)).Concat(Enumerable.Range(21, 7).Select(y => new Cell(27, y))).ToList();
            var route = new WalkRoute(Who, Map, path);
            Who.controller = route;
            int count = 0;
            while (!route.Finished && count++ < 400)
                WalkFrame(route, 33);
            Assert(route.Finished && !route.Failed && Cell.Of(Who) == new Cell(27, 27), "Fast corner path oscillated");
            Assert(Who.GetBoundingBox().Center == new Point(27 * 64 + 32, 27 * 64 + 32), "Final stance differs from planned tool geometry");
        });
        Check("manual walking and unrelated controllers remain unmodified", () =>
        {
            var route = new PathFindController(new Stack<Point>(new[] { new Point(21, 20) }), Map, Who, new Point(21, 20));
            Who.controller = route;
            Control.Clear();
            Assert(ReferenceEquals(Who.controller, route), "Clear stole another controller");
            Who.controller = null;
            Who.SetMovingRight(true);
            var position = Who.Position;
            var time = new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(16));
            Game1.currentGameTime = time;
            Who.MovePosition(time, Game1.viewport, Map);
            Assert(Who.Position.X > position.X, "Manual movement blocked");
        });
    }
    private static void WalkFrame(PathFindController route, int milliseconds)
    {
        var time = new GameTime(Game1.currentGameTime.TotalGameTime + TimeSpan.FromMilliseconds(milliseconds), TimeSpan.FromMilliseconds(milliseconds));
        Game1.currentGameTime = time;
        if (route.update(time))
            Who.controller = null;
        Who.updateMovementAnimation(time);
    }
}
