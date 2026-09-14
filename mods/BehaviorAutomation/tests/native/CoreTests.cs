using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void CoreTests()
    {
        Check("near-reserve pickaxe impact applies after the native stamina debit", () =>
        {
            Who.Stamina = 12;
            Config.ReserveStamina = 10;
            var stone = ObjectAt("(O)343", 24, 20);
            stone.MinutesUntilReady = 1;
            Select(new Pickaxe(), 24, 20);
            Until(() => Control.State == "idle" || Control.Paused);
            Assert(Who.Stamina == 10 && !Map.Objects.ContainsKey(new(24, 20)), "Prepaid native action was blocked after charging stamina");
        });
        Check("left tool scope and right smart pool are independent under global switches", () =>
        {
            var pick = new Pickaxe();
            var axe = new Axe();
            Who.Items[0] = pick;
            Who.Items[1] = axe;
            var stone = ObjectAt("(O)343", 24, 20);
            stone.MinutesUntilReady = 1;
            Map.terrainFeatures[new(26, 20)] = new Tree("1", 5);
            SelectModern(pick, new(23, 19, 5, 3));
            Assert(Control.Board.Jobs.Count == 1 && Control.Board.Jobs[0].Kind == ActionKind.Stone, "Left selected another work tool");
            Config.SmartActions.Remove(ActionKind.Stone);
            SmartSelect(pick, new(23, 19, 5, 3));
            Assert(Control.Board.Jobs.All(t => t.Kind != ActionKind.Stone) && Control.Board.Jobs.Any(t => t.Kind == ActionKind.WildTree), "Smart pool ignored");
            Config.Actions.Remove(ActionKind.WildTree);
            SmartSelect(pick, new(23, 19, 5, 3));
            Assert(Control.Board.Jobs.Any(t => t.Kind == ActionKind.WildTree), "Held page disabled independent smart work");
            Config.SmartActions.Remove(ActionKind.WildTree);
            SmartSelect(pick, new(23, 19, 5, 3));
            Assert(!Control.Board.HasSelection, "Right pool exclusion ignored");
            SelectModern(pick, new(24, 20, 1, 1));
            Finished();
            Assert(!Map.Objects.ContainsKey(new(24, 20)), "Right pool disabled left tool");
        });
        Check("right never selects hoe or seeds even with explicit held items", () =>
        {
            Who.Items[0] = new Hoe();
            var tile = Map.Map.GetLayer("Back").Tiles[24, 20];
            tile.Properties["Diggable"] = "T";
            try
            {
                ObjectAt("(O)590", 25, 20);
                SmartSelect(Who.Items[0], new(24, 20, 2, 1));
                Assert(!Control.Board.HasSelection, "Right inferred hoe work");
                SelectModern((Tool)Who.Items[0], new(24, 20, 2, 1));
                Assert(Control.Board.Jobs.Count == 2, "Held hoe missing till/spot work");
                Control.Clear();
                Map.terrainFeatures[new(24, 20)] = new HoeDirt(0, Map);
                var seeds = ItemRegistry.Create("(O)472", 10);
                Who.Items[1] = seeds;
                SmartSelect(seeds, new(24, 20, 1, 1));
                Assert(!Control.Board.HasSelection && seeds.Stack == 10, "Right planted seeds");
            }
            finally { tile.Properties.Remove("Diggable"); }
        });
        Check("new rectangle replaces all old work and repeated selection stays selected", () =>
        {
            var crop = CropAt(30, 20);
            SelectModern(new WateringCan { WaterLeft = 30 }, new(30, 20, 1, 1));
            var stone = ObjectAt("(O)343", 24, 20);
            stone.MinutesUntilReady = 1;
            var pick = new Pickaxe();
            SelectModern(pick, new(24, 20, 1, 1));
            SelectModern(pick, new(24, 20, 1, 1));
            Assert(Control.Board.Jobs.Count == 1 && Control.Board.Area == new Rectangle(24, 20, 1, 1), "Replacement retained old state or XOR canceled");
            Finished();
            Assert(crop.state.Value == 0 && !Map.Objects.ContainsKey(new(24, 20)), "Old work executed");
        });
        Check("empty selection ends immediately and cannot watch future targets", () =>
        {
            CropAt(30, 20);
            SelectModern(new WateringCan { WaterLeft = 20 }, new(30, 20, 1, 1));
            Until(() => Who.controller is not null);
            SelectModern(null, new(24, 20, 1, 1));
            Assert(!Control.HasSession && !Control.Board.HasSelection && Who.controller is null && Control.State == "idle", "Empty rectangle retained control");
            var forage = ObjectAt("(O)16", 24, 20);
            forage.isSpawnedObject.Value = true;
            for (int i = 0; i < 160; i++)
                Frame();
            Assert(Map.Objects.ContainsKey(new(24, 20)), "Empty selection watched new targets");
        });
        Check("held seed planting consumes native seed stock in the selected bed", () =>
        {
            var seed = ItemRegistry.Create("(O)472", 3);
            Who.Items[0] = seed;
            for (int x = 23; x < 26; x++)
                Map.terrainFeatures[new(x, 20)] = new HoeDirt(0, Map);
            var outside = new HoeDirt(0, Map);
            Map.terrainFeatures[new(26, 20)] = outside;
            DragWith(SButton.MouseLeft, 23, 20, 25, 20);
            Until(() => Control.State == "idle");
            Assert(Enumerable.Range(23, 3).All(x => ((HoeDirt)Map.terrainFeatures[new(x, 20)]).crop is not null) && outside.crop is null, "Planting crossed bed");
            Assert(!Who.Items.Any(i => i?.QualifiedItemId == "(O)472"), "Native seed debit missing");
        });
        Check("tree planting plans maintain native growth spacing", () =>
        {
            foreach (string id in new[] { "(O)309", "(O)628" })
            {
                Reset();
                var seed = ItemRegistry.Create<StardewValley.Object>(id, 99);
                Who.Items[0] = seed;
                var area = new Rectangle(24, 18, 8, 8);
                var cells = new HashSet<Cell>();
                for (int x = area.Left; x < area.Right; x++)
                    for (int y = area.Top; y < area.Bottom; y++)
                        cells.Add(new(x, y));
                var plans = Planting.Scan(Map, seed, ToolMode.TreeSeeds, area, cells, Config, Array.Empty<WorkTarget>());
                int gap = id == "(O)628" ? 3 : 2;
                Assert(plans.Count > 1 && plans.All(a => plans.All(b => a == b || Math.Max(Math.Abs(a.Origin.X - b.Origin.X), Math.Abs(a.Origin.Y - b.Origin.Y)) >= gap)), "Tree spacing too tight");
            }
        });
        Check("global and right switches still apply when tree turns into stump", () =>
        {
            var axe = new Axe { UpgradeLevel = 4 };
            Who.Items[0] = axe;
            var tree = new Tree("1", 5);
            Map.terrainFeatures[new(24, 20)] = tree;
            Config.SmartActions.Remove(ActionKind.TreeStump);
            SmartSelect(axe, new(24, 20, 1, 1));
            Until(() => tree.stump.Value);
            Until(() => Control.State == "idle");
            Assert(Map.terrainFeatures.ContainsKey(new(24, 20)) && tree.health.Value > -99, "Excluded stump was cut using stale tree classification");
        });
        CheckWithMod("sznine.SmartWateringCan", "old standalone watering does not take over outside a selection", () =>
        {
            var water = Installed<object>("sznine.SmartWateringCan");
            var controller = HarmonyLib.AccessTools.Property(water.GetType(), "Controller").GetValue(water)!;
            Who.Items[0] = new WateringCan { WaterLeft = 20 };
            var crop = CropAt(22, 20);
            for (int i = 0; i < 120; i++)
                HarmonyLib.AccessTools.Method(controller.GetType(), "Tick").Invoke(controller, new object[] { Who, 16d, true, false });
            Assert(crop.state.Value == 0 && Who.controller is null && HarmonyLib.AccessTools.Property(controller.GetType(), "Active").GetValue(controller) is null, "Duplicate controller took over");
        });
    }
}
