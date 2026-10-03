using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private static WorkTarget PointTarget(int x, int y) => new()
    { Entity = new object(), Origin = new(x, y), Area = new(x, y, 1, 1), Kind = ActionKind.Forage, Mode = ToolMode.Hand };

    private void CoordinatorTests()
    {
        foreach (var (label, position, expected) in new[]
        {
            ("forward single", new Cell(24, 20), true),
            ("small side step", new Cell(24, 21), true),
            ("behind starting point", new Cell(16, 20), false),
            ("large detour", new Cell(24, 23), false),
            ("equal distance", new Cell(30, 20), false),
            ("past main destination", new Cell(31, 20), false)
        })
            Check("opportunity arbitration " + label, () =>
            {
                var start = new Cell(20, 20);
                var main = new Approach(PointTarget(30, 20), new(30, 20), new Cell(30, 20).Center, 1);
                var path = Enumerable.Range(21, 10).Select(x => new Cell(x, 20)).ToList();
                var single = new Approach(PointTarget(position.X, position.Y), position, position.Center, 1);
                using var search = new OpportunitySearch(start, main, 100, OpportunitySearch.Corridor(start, path), new[] { single },
                    p => p.X >= 0 && p.Y >= 0 && p.X < 80 && p.Y < 60, Config);
                while (!search.Finished) search.Step();
                Assert((search.Result is not null) == expected, "Incorrect insertion policy: " + label);
                if (expected) Assert(search.ChosenCost < 100 && search.ExtraCost <= 20, "Distance contract failed");
            });
        Check("wall-separated single uses real walking distance and cannot steal the main route", () =>
        {
            Config.AllowDiagonalMovement = false;
            var start = new Cell(20, 20);
            var main = new Approach(PointTarget(30, 20), new(30, 20), new Cell(30, 20).Center, 1);
            var single = new Approach(PointTarget(21, 22), new(21, 22), new Cell(21, 22).Center, 1);
            var corridor = new HashSet<Cell>();
            for (int x = 19; x <= 31; x++) for (int y = 19; y <= 23; y++) corridor.Add(new(x, y));
            using var search = new OpportunitySearch(start, main, 100, corridor, new[] { single },
                p => p.Y != 21 || p.X >= 29, Config);
            while (!search.Finished) search.Step();
            Assert(search.Result is null, "Geometric proximity bypassed a wall");
        });
        Check("chained opportunities cannot enlarge the original route corridor", () =>
        {
            var main = new Approach(PointTarget(30, 20), new(30, 20), new Cell(30, 20).Center, 1);
            var corridor = OpportunitySearch.Corridor(new(20, 20), Enumerable.Range(21, 10).Select(x => new Cell(x, 20)));
            var deeper = new Approach(PointTarget(24, 22), new(24, 22), new Cell(24, 22).Center, 1);
            using var search = new OpportunitySearch(new(24, 21), main, 64, corridor, new[] { deeper }, _ => true, Config);
            while (!search.Finished) search.Step();
            Assert(search.Result is null, "Second single shifted the corridor away from the main route");
        });
        Check("targets already completed by the next area operation are not inserted again", () =>
        {
            var target = PointTarget(23, 20);
            var main = new Approach(PointTarget(30, 20), new(30, 20), new Cell(30, 20).Center, 1, 2, new() { target });
            var single = new Approach(target, new(23, 20), target.Origin.Center, 1);
            using var search = new OpportunitySearch(new(20, 20), main, 100,
                OpportunitySearch.Corridor(new(20, 20), Enumerable.Range(21, 10).Select(x => new Cell(x, 20))), new[] { single }, _ => true, Config);
            while (!search.Finished) search.Step();
            Assert(search.Result is null, "Duplicated an already covered task");
        });
        Check("native charged pass keeps its main destination across a real single-target insertion", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            Who.Items[0] = can;
            var crops = new List<HoeDirt>();
            for (int x = 23; x < 35; x++) for (int y = 20; y < 23; y++) crops.Add(CropAt(x, y));
            SmartSelect(can, new(20, 18, 18, 8));
            Until(() => Control.Active is not null);
            // Spawn a legitimate pickup after the pass is established, outside the first cast.
            var forage = ObjectAt("(O)16", 25, 20);
            forage.isSpawnedObject.Value = true;
            var decisions = new List<object>();
            WorkDecision? last = null;
            Cell? insertedMain = null;
            bool resumedMain = false;
            Until(() =>
            {
                var decision = Control.Board.Coordinator.LastDecision;
                if (decision is not null && !ReferenceEquals(last, decision))
                {
                    last = decision;
                    decisions.Add(new { decision.Opportunity, Main = decision.Main.Stand, Chosen = decision.Chosen.Stand,
                        decision.MainCost, decision.ChosenCost, decision.ExtraCost });
                    if (decision.Opportunity)
                    {
                        Assert(decision.ChosenCost < decision.MainCost && decision.ExtraCost <= 20, "Live coordinator broke its distance contract");
                        insertedMain = decision.Main.Stand;
                    }
                    else if (insertedMain is { } committed)
                        resumedMain |= decision.Chosen.Stand == committed;
                }
                return Control.State == "idle";
            });
            Assert(crops.All(c => c.state.Value == 1) && !Map.Objects.ContainsKey(forage.TileLocation), "Mixed pass left native work unfinished");
            Assert(insertedMain is not null && resumedMain, "No actual insertion and return to the committed main stance occurred");
            File.WriteAllText(Path.Combine(Evidence, "coordinator-decisions.json"), System.Text.Json.JsonSerializer.Serialize(decisions));
        });
        Check("inserted repeated-hit work finishes before resuming the charged pass", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            Who.Items[0] = can; Who.Items[1] = new Pickaxe();
            var crops = new List<HoeDirt>();
            for (int x = 23; x < 35; x++) for (int y = 20; y < 23; y++) crops.Add(CropAt(x, y));
            SmartSelect(can, new(20, 18, 18, 8));
            Until(() => Control.Active is not null);
            var stone = ObjectAt("(O)343", 25, 19); stone.MinutesUntilReady = 3;
            int hits = 0; Operation? last = null;
            Until(() =>
            {
                if (Control.Active is { } op && !ReferenceEquals(last, op))
                {
                    last = op;
                    if (ReferenceEquals(op.Approach.Target.Entity, stone)) hits++;
                    else if (hits > 0) Assert(!Map.Objects.ContainsKey(stone.TileLocation), "Left an inserted stone half-finished");
                }
                return Control.State == "idle";
            });
            Assert(hits == 3 && crops.All(c => c.state.Value == 1), "Repeated hit insertion did not complete all work");
        });
    }
}
