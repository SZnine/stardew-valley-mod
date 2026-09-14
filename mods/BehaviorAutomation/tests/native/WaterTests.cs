using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void WaterTests()
    {
        Check("charged watering respects a disabled pet bowl inside its footprint", () =>
        {
            var can = new WateringCan { UpgradeLevel = 2, WaterLeft = 50 };
            Who.Items[0] = can;
            var bowl = new StardewValley.Buildings.PetBowl(new(25, 20));
            Map.buildings.Add(bowl);
            var target = WorldTargets.Scan(Map, Who, ToolMode.WateringCan, can, new(24, 19, 5, 5), Config).Single(t => t.Kind == ActionKind.WaterBowl);
            var p = target.Origin;
            var left = CropAt(p.X - 1, p.Y);
            var right = CropAt(p.X + 1, p.Y);
            Config.Actions.Remove(ActionKind.WaterBowl);
            Config.LeftActions.Remove(ActionKind.WaterBowl);
            SelectModern(can, new(p.X - 1, p.Y, 3, 1));
            Until(() => Control.State == "idle");
            Assert(left.state.Value == 1 && right.state.Value == 1 && !bowl.watered.Value, "Disabled bowl changed during a neighboring cast");
        });
        Check("large crop fields bound charged candidates while retaining all selected jobs", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            Who.Items[0] = can;
            for (int y = 10; y < 50; y++)
                for (int x = 10; x < 50; x++)
                    CropAt(x, y);
            var area = new Rectangle(10, 10, 40, 40);
            var targets = WorldTargets.Scan(Map, Who, ToolMode.WateringCan, can, area, Config);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var plans = Watering.Approaches(targets, Who, area, Config).ToArray();
            timer.Stop();
            Assert(targets.Count == 1600 && plans.Length > 0 && plans.SelectMany(p => p.Hits!).Distinct().Count() <= 256, "Charged planner not bounded or lost source jobs");
            File.WriteAllText(Path.Combine(Evidence, "water-planner-budget.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                targets = targets.Count,
                poses = plans.Length,
                milliseconds = timer.ElapsedMilliseconds
            }));
        });
        foreach (int level in Enumerable.Range(0, 5))
            Check("native watering charge level " + level + " batches, costs and protects bounds", () =>
            {
                var can = new WateringCan { UpgradeLevel = level, WaterLeft = 100 };
                var crops = new List<HoeDirt>();
                for (int y = 20; y < 23; y++)
                    for (int x = 24; x < 29; x++)
                        crops.Add(CropAt(x, y));
                var outside = CropAt(29, 21);
                SelectModern(can, new(24, 20, 5, 3));
                var powers = new List<int>();
                Operation? last = null;
                Until(() => { if (Control.Active is { } op && !ReferenceEquals(op, last)) { powers.Add(op.Approach.Power); last = op; } return Control.State == "idle"; });
                Assert(crops.All(c => c.state.Value == 1) && outside.state.Value == 0, "Water coverage/boundary failed");
                Assert(level == 0 ? powers.Count == 15 : powers.Count < 15 && powers.Any(p => p > 0), "Upgraded can still uses only single tiles: " + string.Join(",", powers));
                Assert(can.WaterLeft == 100 - powers.Sum(p => p + 1) && Who.Stamina == 1000 - powers.Sum(p => (p + 1) * 2), "Native water/energy debit differs");
            });
        Check("reaching enchantment supplies native 5x5 footprint", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            can.enchantments.Add(new ReachingToolEnchantment());
            Assert(Watering.MaxPower(can) == 5 && Watering.Patterns(can).Any(p => p.Power == 5 && p.Offsets.Length == 25), "Reaching shape missing");
            var crops = new List<HoeDirt>();
            for (int y = 20; y < 25; y++)
                for (int x = 24; x < 29; x++)
                    crops.Add(CropAt(x, y));
            SelectModern(can, new(24, 20, 5, 5));
            int casts = FinishCountingSwings();
            Assert(crops.All(c => c.state.Value == 1) && casts == 1 && can.WaterLeft == 94, "Reaching did not finish in one native cast");
        });
        Check("empty can finds reachable water outside selection and resumes watering", () =>
        {
            var source = Map.Map.GetLayer("Back").Tiles[16, 25];
            source.Properties["WaterSource"] = "T";
            try
            {
                var can = new WateringCan { UpgradeLevel = 2, WaterLeft = 0 };
                var crop = CropAt(28, 20);
                SelectModern(can, new(28, 20, 1, 1));
                bool refilled = false;
                Until(() => { refilled |= can.WaterLeft > 1; return Control.State == "idle"; });
                Assert(refilled && crop.state.Value == 1 && can.WaterLeft == can.waterCanMax - 1, "Refill or resumed use failed");
            }
            finally { source.Properties.Remove("WaterSource"); }
        });
        Check("refill route chooses a reachable shore instead of the closest trapped water", () =>
        {
            var near = Map.Map.GetLayer("Back").Tiles[23, 20];
            var far = Map.Map.GetLayer("Back").Tiles[17, 26];
            near.Properties["WaterSource"] = far.Properties["WaterSource"] = "T";
            try
            {
                foreach (var d in Cell.Directions)
                {
                    var p = new Cell(23, 20).Add(d);
                    Map.Objects[p.Tile] = new Chest(true) { TileLocation = p.Tile };
                }
                var can = new WateringCan { WaterLeft = 0 };
                var crop = CropAt(28, 20);
                SelectModern(can, new(28, 20, 1, 1));
                Until(() => Control.Active?.Approach.Target.Entity is WaterSource);
                Assert(((WaterSource)Control.Active!.Approach.Target.Entity).Tile == new Cell(17, 26), "Unreachable nearest source chosen");
                Until(() => Control.State == "idle");
                Assert(crop.state.Value == 1, "Distant refill failed");
            }
            finally { near.Properties.Remove("WaterSource"); far.Properties.Remove("WaterSource"); }
        });
        Check("missing water source pauses without fake refill or stamina loss", () =>
        {
            var can = new WateringCan { WaterLeft = 0 };
            var crop = CropAt(27, 20);
            SelectModern(can, new(27, 20, 1, 1));
            Until(() => Control.Paused);
            Assert(Control.State == "no-water-source" && can.WaterLeft == 0 && crop.state.Value == 0 && Who.Stamina == 1000, "Invented water or wrong failure");
        });
        Check("low reserve selects affordable casts and never spends past reserve", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            Who.Stamina = 15;
            Config.ReserveStamina = 10;
            var crops = new[] { CropAt(24, 20), CropAt(25, 20), CropAt(26, 20) };
            SelectModern(can, new(24, 20, 3, 1));
            Until(() => Control.State == "idle" || Control.Paused);
            Assert(Who.Stamina >= 10 && crops.Any(c => c.state.Value == 1), $"Stamina reserve or affordable work failed: stamina={Who.Stamina},state={Control.State},water={can.WaterLeft},crops={string.Join(",", crops.Select(c => c.state.Value))}");
        });
        Check("cancel held watering charge restores movement and does not water later", () =>
        {
            var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 100 };
            var crops = new List<HoeDirt>();
            for (int y = 20; y < 23; y++)
                for (int x = 24; x < 29; x++)
                    crops.Add(CropAt(x, y));
            SelectModern(can, new(24, 20, 5, 3));
            Until(() => Control.Active is { Released: false } op && op.Approach.Power > 0 && Who.UsingTool && Who.canReleaseTool);
            Control.Clear();
            Assert(!Who.UsingTool && Who.CanMove && Who.toolHold.Value == 0 && Who.toolPower.Value == 0 && Who.jitterStrength == 0, "Canceled charge left abnormal pose");
            for (int i = 0; i < 120; i++)
                Frame();
            Assert(crops.All(c => c.state.Value == 0) && can.WaterLeft == 100, "Canceled charge struck later");
        });
        Check("stored empty can returns to its chest after refill and work", () =>
        {
            var source = Map.Map.GetLayer("Back").Tiles[17, 25];
            source.Properties["WaterSource"] = "T";
            try
            {
                var can = new WateringCan { UpgradeLevel = 1, WaterLeft = 0 };
                var chest = Store(can);
                var crop = CropAt(25, 20);
                SmartSelect(null, new(25, 20, 1, 1));
                Until(() => Control.State == "idle");
                Assert(crop.state.Value == 1 && chest.Items.Contains(can) && !Who.Items.Contains(can) && can.WaterLeft == can.waterCanMax - 1, "Loan/refill state lost");
            }
            finally { source.Properties.Remove("WaterSource"); }
        });
    }
}
