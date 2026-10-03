using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void WorkPassTests()
    {
        Check("region entry follows an animal's current position after selection", () =>
        {
            var animal = CareAnimal("White Chicken", 78001, 23, 20);
            SmartSelect(null, new(22, 19, 20, 6));
            animal.Position = new(36 * 64, 20 * 64);
            Until(() => Control.State == "idle");
            Assert(animal.wasPet.Value, "Region anchor stayed at the animal's stale selection origin");
        });
        Check("regional work finishes mixed nearby targets before advancing to another area", () =>
        {
            Who.Items[0] = new WateringCan { WaterLeft = 100 };
            Who.Items[1] = new Pickaxe();
            var a = CropAt(20, 20);
            var b = CropAt(26, 20);
            var stone = ObjectAt("(O)343", 24, 20);
            stone.MinutesUntilReady = 1;
            var far = CropAt(28, 20);
            var farther = CropAt(36, 20);
            SmartSelect(null, new(14, 19, 23, 3));
            var trace = new List<object>();
            Operation? previous = null;
            bool sawFirstRegion = false;
            Until(() =>
            {
                if (Control.Board.Coordinator.MainTarget?.Origin == new Cell(20, 20))
                    sawFirstRegion = true;
                if (Control.Active is { } op && !ReferenceEquals(op, previous))
                {
                    previous = op;
                    trace.Add(new
                    {
                        Anchor = Control.Board.Coordinator.MainTarget?.Origin,
                        Target = op.Approach.Target.Origin,
                        op.Approach.Target.Mode
                    });
                    if (op.Approach.Target.Origin.X > 26)
                        Assert(a.state.Value == 1 && b.state.Value == 1 && !Map.Objects.ContainsKey(stone.TileLocation),
                            "Left the current region with a different tool's nearby task unfinished");
                }
                return Control.State == "idle";
            });
            Assert(sawFirstRegion && far.state.Value == 1 && farther.state.Value == 1, "Region entry/advance failed");
            File.WriteAllText(Path.Combine(Evidence, "region-trace.json"), System.Text.Json.JsonSerializer.Serialize(trace));
        });
        Check("refill preserves the original region even when water lies beside later work", () =>
        {
            var source = Map.Map.GetLayer("Back").Tiles[36, 20];
            source.Properties["WaterSource"] = "T";
            try
            {
                var can = new WateringCan { WaterLeft = 1 };
                var first = CropAt(23, 20);
                var near = CropAt(26, 20);
                var far = CropAt(35, 23);
                SelectModern(can, new(23, 20, 13, 4));
                Until(() => Control.Active?.Approach.Target.Entity is WaterSource);
                var anchor = Control.Board.Coordinator.MainTarget?.Origin;
                Assert(anchor == new Cell(26, 20), "Refill lost the starting area");
                Until(() =>
                {
                    if (near.state.Value == 0 && Control.HasSession)
                        Assert(Control.Board.Coordinator.MainTarget?.Origin == anchor && far.state.Value == 0, "Refill switched to the distant area");
                    return Control.State == "idle";
                });
                Assert(first.state.Value == 1 && near.state.Value == 1 && far.state.Value == 1, "Incomplete refill route");
            }
            finally { source.Properties.Remove("WaterSource"); }
        });
        Check("unreachable work in one region does not discard later reachable work", () =>
        {
            Config.ClearObstacles = false;
            var near = CropAt(23, 20);
            var blocked = CropAt(25, 20);
            var far = CropAt(38, 20);
            foreach (var p in Cell.Directions.Select(d => new Cell(25, 20).Add(d)).Append(new(25, 20)))
                Map.Objects[p.Tile] = new Chest(true) { TileLocation = p.Tile };
            SelectModern(new WateringCan { WaterLeft = 20 }, new(23, 20, 16, 1));
            Until(() => Control.State == "idle");
            Assert(near.state.Value == 1 && blocked.state.Value == 0 && far.state.Value == 1, "Unreachable rejection escaped its region");
        });
        Check("work appearing while a route is being planned does not restart it", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            for (int x = 23; x < 30; x++)
                for (int y = 20; y < 27; y++)
                    CropAt(x, y);
            SelectModern(can, new(23, 20, 12, 7));
            Until(() => Control.Board.Coordinator.MainTarget?.Origin is not null);
            int added = 0;
            Until(() =>
            {
                if (added < 30)
                {
                    CropAt(30 + added % 5, 20 + added / 5);
                    Control.Board.Refresh(Who, Config);
                    added++;
                }
                return Control.Active is not null;
            }, 3000);
            Until(() => Control.State == "idle");
            Assert(Map.terrainFeatures.Values.OfType<HoeDirt>().All(t => t.state.Value == 1), "Refresh lost selected work");
        });
        Check("route construction and one-step goal generation are lazy and bounded", () =>
        {
            var soil = CropAt(23, 20);
            var can = new WateringCan { WaterLeft = 100 };
            Who.Items[0] = can;
            var target = WorldTargets.Scan(Map, Who, ToolMode.WateringCan, can, new(23, 20, 1, 1), Config).Single();
            int generated = 0, probes = 0;
            IEnumerable<Approach> Plans()
            {
                for (int i = 0; i < 5000; i++)
                {
                    generated++;
                    yield return new(target, new(22, 20), target.Origin.Center, 1, Hits: new() { target });
                }
            }
            var route = new RouteSearch(Cell.Of(Who), Plans(), p => { probes++; return p.X >= 0 && p.Y >= 0 && p.X < 40 && p.Y < 40; }, style: WorkStyle.Charged);
            Assert(generated == 0 && probes == 0, "Constructor did synchronous planning");
            route.Step(1);
            Assert(generated <= 1 && probes == 0 && !route.Finished, "One planning slice exceeded its goal budget");
            int steps = 0;
            while (!route.Finished && ++steps < 10000)
                route.Step();
            Assert(route.Finished && route.Result is not null && soil.state.Value == 0 && can.WaterLeft == 100,
                "Planning changed game state or failed to finish");
        });
        Check("large-field regional planning records cooperative frame costs and remains cancellable", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            for (int y = 10; y < 50; y++)
                for (int x = 10; x < 50; x++)
                    CropAt(x, y);
            SelectModern(can, new(10, 10, 40, 40));
            var samples = new List<double>();
            for (int i = 0; i < 600 && Control.Active is null; i++)
            {
                var watch = Stopwatch.StartNew();
                Frame();
                samples.Add(watch.Elapsed.TotalMilliseconds);
            }
            Assert(Control.Active is not null, "Large field never began work");
            Control.Clear();
            for (int i = 0; i < 300; i++)
                Frame();
            Assert(Control.State == "idle" && Who.controller is null && Control.Active is null, "Canceled planner kept operating");
            File.WriteAllText(Path.Combine(Evidence, "region-planning-cost.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Targets = 1600,
                FramesUntilAction = samples.Count,
                MaximumFrameMs = samples.Max(),
                MedianFrameMs = samples.OrderBy(x => x).ElementAt(samples.Count / 2)
            }));
        });
    }
}
