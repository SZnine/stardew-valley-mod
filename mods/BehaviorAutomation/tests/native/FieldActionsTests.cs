using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
using SObject = StardewValley.Object;

namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private HoeDirt EmptySoil(int x, int y)
    {
        var soil = new HoeDirt(0, Map); Map.terrainFeatures[new(x, y)] = soil; return soil;
    }
    private static SObject? blockedFertilizer;
    private static int blockedFertilizerAttempts;
    private static bool BlockFertilizer(SObject __instance, ref bool __result)
    {
        if (!ReferenceEquals(__instance, blockedFertilizer)) return true;
        blockedFertilizerAttempts++; __result = false; return false;
    }
    private void FieldActionsTests()
    {
        foreach (string id in new[] { "47", "53", "66" })
            Check("left global scythe " + id + " overrides held hand axe pickaxe watering and food for harvesting", () =>
            {
                foreach (string heldKind in new[] { "hand", "axe", "pickaxe", "water", "food" })
                {
                    Reset();
                    Item? held = heldKind switch { "axe" => new Axe(), "pickaxe" => new Pickaxe(), "water" => new WateringCan { WaterLeft = 100 }, "food" => ItemRegistry.Create("(O)194", 3), _ => null };
                    Who.Items[0] = held!;
                    var scythe = new MeleeWeapon(id);
                    var chest = Store(scythe);
                    var ripe = CropAt(23, 20, true);
                    var forage = ObjectAt("(O)16", 24, 20); forage.isSpawnedObject.Value = true;
                    var weed = ObjectAt("(O)0", 25, 20);
                    var dead = CropAt(23, 21); dead.crop!.dead.Value = true;
                    var grass = new Grass(1, 3); Map.terrainFeatures[new(24, 21)] = grass;
                    var growing = CropAt(25, 21);
                    var torch = ObjectAt("(O)93", 26, 20);
                    var sprinkler = ObjectAt("(O)645", 26, 21);
                    DragWith(SButton.MouseLeft, 23, 20, 26, 21);
                    var gather = Control.Board.Jobs.Where(t => t.Kind is ActionKind.HarvestCrop or ActionKind.Forage or ActionKind.Weed or ActionKind.Grass or ActionKind.DeadCrop).ToArray();
                    Assert(gather.Length == 5 && gather.All(t => t.Mode == ToolMode.Scythe && ReferenceEquals(t.Tool, scythe)), "Held tool or hand replaced global scythe: " + heldKind);
                    int swings = 0; Operation? last = null;
                    Until(() =>
                    {
                        if (Control.Active is { } op && !ReferenceEquals(last, op)) { last = op; if (op.Approach.Target.Mode == ToolMode.Scythe) swings++; }
                        return Control.State == "idle";
                    });
                    Assert(swings > 0 && ripe.crop is null && dead.crop is null && !Map.Objects.ContainsKey(forage.TileLocation)
                        && !Map.Objects.ContainsKey(weed.TileLocation) && !Map.terrainFeatures.Values.Contains(grass), "Native sweep did not complete work");
                    Assert(growing.crop is { dead.Value: false } && ReferenceEquals(Map.Objects[torch.TileLocation], torch)
                        && ReferenceEquals(Map.Objects[sprinkler.TileLocation], sprinkler), "Sweep damaged protected crops or props");
                    Assert(chest.Items.Contains(scythe) && !Who.Items.Contains(scythe), "Borrowed scythe not returned");
                }
            });
        Check("global scythe preference preserves disabled extras and unavailable-tool fallback", () =>
        {
            var pick = new Pickaxe(); Who.Items[0] = pick;
            var scythe = new MeleeWeapon("66"); Store(scythe);
            var weed = ObjectAt("(O)0", 23, 20);
            Config.LeftActions.Clear();
            SelectModern(pick, new(23, 20, 1, 1));
            Assert(Control.Board.Jobs.Single().Tool == pick && Control.Board.Jobs.Single().Scope == WorkScope.Held, "Disabled extras still overrode held tool");
            Control.Clear(); Config.LeftActions.Add(ActionKind.Weed); Config.UseStoredTools = false;
            SelectModern(pick, new(23, 20, 1, 1));
            Assert(Control.Board.Jobs.Single().Tool == pick, "Used an unavailable stored scythe");
            Until(() => Control.State == "idle");
            Assert(!Map.Objects.ContainsKey(weed.TileLocation), "Held fallback stopped working");
        });
        Check("migration expands the former default left pool but preserves custom choices", () =>
        {
            var old = new ModConfig { ConfigVersion = 9 };
            old.LeftActions.ExceptWith(new[] { ActionKind.Weed, ActionKind.Grass, ActionKind.DeadCrop });
            old.Actions.Remove(ActionKind.Fertilize); old.Migrate();
            Assert(old.LeftActions.Contains(ActionKind.Weed) && old.LeftActions.Contains(ActionKind.Grass) && old.LeftActions.Contains(ActionKind.DeadCrop)
                && old.Actions.Contains(ActionKind.Fertilize) && !old.Actions.Contains(ActionKind.RemoveSoil) && !old.WaterEmptySoil, "Migration defaults are wrong");
            foreach (var pool in new[] { new HashSet<ActionKind>(), new() { ActionKind.Forage } })
            {
                var custom = new ModConfig { ConfigVersion = 9, LeftActions = pool.ToHashSet() }; custom.Migrate();
                Assert(custom.LeftActions.SetEquals(pool), "Migration overwrote a customized global pool");
            }
        });
        Check("empty soil removal defaults off and never enters automatic tool pools", () =>
        {
            EmptySoil(23, 20); var pick = new Pickaxe(); Who.Items[0] = pick;
            Select(pick, 23, 20); Assert(!Control.Board.HasSelection, "Empty soil removal enabled by default");
            Config.Actions.Add(ActionKind.RemoveSoil); Config.LeftActions.Add(ActionKind.RemoveSoil); Config.SmartActions.Add(ActionKind.RemoveSoil); Config.Normalize();
            Assert(!Config.LeftActions.Contains(ActionKind.RemoveSoil) && !Config.SmartActions.Contains(ActionKind.RemoveSoil), "Removal admitted to implicit pools");
            SmartSelect(pick, new(23, 20, 1, 1)); Assert(!Control.Board.HasSelection, "Right automatic work removed empty soil");
        });
        Check("native pickaxe clears only unoccupied empty soil inside selection", () =>
        {
            Config.Actions.Add(ActionKind.RemoveSoil);
            var clear = EmptySoil(23, 20); clear.fertilizer.Value = "368";
            var crop = CropAt(24, 20); crop.fertilizer.Value = "369";
            var covered = EmptySoil(25, 20); var sprinkler = ObjectAt("(O)645", 25, 20);
            var outside = EmptySoil(23, 21);
            Select(new Pickaxe(), 23, 20, 3, 1);
            Assert(Control.Board.Jobs.Count == 1 && Control.Board.Jobs.Single().Kind == ActionKind.RemoveSoil, "Protected soil entered removal queue");
            Until(() => Control.State == "idle");
            Assert(!Map.terrainFeatures.ContainsKey(new(23, 20)) && ReferenceEquals(Map.terrainFeatures[new(24, 20)], crop)
                && crop.crop is not null && crop.fertilizer.Value == "369" && ReferenceEquals(Map.terrainFeatures[new(25, 20)], covered)
                && ReferenceEquals(Map.Objects[sprinkler.TileLocation], sprinkler) && ReferenceEquals(Map.terrainFeatures[new(23, 21)], outside), "Clearing exceeded permission");
        });
        Check("planting a crop after pickaxe selection cancels soil removal at impact", () =>
        {
            Config.Actions.Add(ActionKind.RemoveSoil); var soil = EmptySoil(23, 20);
            Select(new Pickaxe(), 23, 20); Until(() => Control.Active is not null);
            var crop = new Crop("472", 23, 20, Map); soil.crop = crop;
            Until(() => Control.State == "idle");
            Assert(ReferenceEquals(Map.terrainFeatures[new(23, 20)], soil) && ReferenceEquals(soil.crop, crop) && !crop.dead.Value, "Late crop was destroyed");
        });
        Check("explicit soil removal supersedes global empty watering on the same tile", () =>
        {
            Config.Actions.Add(ActionKind.RemoveSoil); Config.LeftActions.Add(ActionKind.Water); Config.WaterEmptySoil = true;
            var can = new WateringCan { WaterLeft = 20 }; Who.Items[1] = can;
            EmptySoil(23, 20); Select(new Pickaxe(), 23, 20);
            Assert(Control.Board.Jobs.Single().Kind == ActionKind.RemoveSoil, "Conflicting watering remained queued");
            Until(() => Control.State == "idle");
            Assert(!Map.terrainFeatures.ContainsKey(new(23, 20)) && can.WaterLeft == 20, "Watered soil immediately before removing it");
        });
        Check("watering empty soil is opt-in and supports native charged refill around facilities", () =>
        {
            var source = Map.Map.GetLayer("Back").Tiles[19, 24]; source.Properties["WaterSource"] = "T";
            try
            {
                var soils = new List<HoeDirt>();
                for (int x = 23; x < 26; x++) for (int y = 20; y < 23; y++) soils.Add(EmptySoil(x, y));
                var can = new WateringCan { UpgradeLevel = 4, WaterLeft = 0 };
                Select(can, 23, 20, 3, 3); Assert(!Control.Board.HasSelection, "Watered empty soil by default");
                var sprinkler = ObjectAt("(O)645", 24, 21); var outside = EmptySoil(26, 20);
                Config.WaterEmptySoil = true;
                Select(can, 23, 20, 3, 3);
                bool charged = false, refill = false;
                Until(() => { charged |= Control.Active?.Approach.Power > 0; refill |= Control.Active?.Approach.Target.Entity is WaterSource; return Control.State == "idle"; });
                Assert(soils.All(s => s.state.Value == 1) && outside.state.Value == 0 && charged && refill && ReferenceEquals(Map.Objects[sprinkler.TileLocation], sprinkler), "Empty-bed watering failed or exceeded bounds");
                Select(can, 23, 20, 3, 3); Assert(!Control.Board.HasSelection, "Wet empty soil was selected again");
            }
            finally { source.Properties.Remove("WaterSource"); }
        });
        Check("turning off empty-soil watering after selection prevents the pending impact", () =>
        {
            var soil = EmptySoil(23, 20); Config.WaterEmptySoil = true;
            Select(new WateringCan { WaterLeft = 20 }, 23, 20); Until(() => Control.Active is not null);
            Config.WaterEmptySoil = false; Until(() => Control.State == "idle");
            Assert(soil.state.Value == 0, "Disabled empty watering still applied");
        });
        Check("held fertilizer follows native soil and pot rules and consumes exact matching stacks", () =>
        {
            Config.LeftActions.Clear();
            var fertilizer = ItemRegistry.Create<SObject>("(O)368", 1); Who.Items[0] = fertilizer;
            Who.Items[1] = ItemRegistry.Create<SObject>("(O)368", 4);
            var other = ItemRegistry.Create<SObject>("(O)369", 5); Who.Items[2] = other;
            var empty = EmptySoil(23, 20); var growing = CropAt(24, 20);
            var existing = EmptySoil(25, 20); existing.fertilizer.Value = "370";
            var late = CropAt(26, 20); late.crop!.currentPhase.Value = 2;
            var pot = new IndoorPot(new(23, 21)); Map.Objects[pot.TileLocation] = pot;
            var covered = EmptySoil(24, 21); var torch = ObjectAt("(O)93", 24, 21);
            var outside = EmptySoil(22, 20);
            DragWith(SButton.MouseLeft, 23, 20, 26, 21);
            Assert(Control.Board.Jobs.Count == 3 && Control.Board.Jobs.All(t => t.Kind == ActionKind.Fertilize && t.Mode == ToolMode.Fertilizer), "Fertilizer eligibility did not match native rules");
            Until(() => Control.State == "idle");
            bool Has(HoeDirt s, string id) => ItemRegistry.QualifyItemId(s.fertilizer.Value) == "(O)" + id;
            Assert(Has(empty, "368") && Has(growing, "368") && Has(pot.hoeDirt.Value, "368") && Has(existing, "370")
                && !late.HasFertilizer() && !covered.HasFertilizer() && !outside.HasFertilizer() && Map.Objects[torch.TileLocation] == torch, "Fertilizer bypassed restrictions");
            Assert(Who.Items.OfType<SObject>().Where(i => i.QualifiedItemId == "(O)368").Sum(i => i.Stack) == 2 && other.Stack == 5, "Fertilizer duplicated or wrong stack consumed");
        });
        Check("tree fertilizer skips grown and previously fertilized trees", () =>
        {
            Config.LeftActions.Clear(); var stock = ItemRegistry.Create<SObject>("(O)805", 5); Who.Items[0] = stock;
            var young = new Tree("1", 2); var grown = new Tree("1", 5); var fed = new Tree("1", 3); fed.fertilized.Value = true;
            Map.terrainFeatures[new(23, 20)] = young; Map.terrainFeatures[new(24, 20)] = grown; Map.terrainFeatures[new(25, 20)] = fed;
            var soil = EmptySoil(26, 20);
            DragWith(SButton.MouseLeft, 23, 20, 26, 20); Until(() => Control.State == "idle");
            Assert(young.fertilized.Value && !grown.fertilized.Value && fed.fertilized.Value && !soil.HasFertilizer() && stock.Stack == 4, "Incorrect tree fertilizer targets or stock");
        });
        Check("fertilizer depletion pauses without borrowing another material or retrying forever", () =>
        {
            Config.LeftActions.Clear(); Who.Items[0] = ItemRegistry.Create<SObject>("(O)368", 1);
            var other = ItemRegistry.Create<SObject>("(O)369", 5); Who.Items[1] = other;
            var a = EmptySoil(23, 20); var b = EmptySoil(24, 20);
            DragWith(SButton.MouseLeft, 23, 20, 24, 20); Until(() => Control.Paused);
            for (int i = 0; i < 400; i++) Frame();
            Assert(Control.State == "fertilizer" && new[] { a, b }.Count(s => s.HasFertilizer()) == 1 && other.Stack == 5 && Who.CanMove && !Who.UsingTool, "Fertilizer depletion did not stop cleanly");
        });
        Check("failed native fertilizer application stops after three attempts without consuming stock", () =>
        {
            Config.LeftActions.Clear(); var stock = ItemRegistry.Create<SObject>("(O)368", 10); Who.Items[0] = stock;
            var harmony = new HarmonyLib.Harmony("sznine.BehaviorProbe.NoFertilizerProgress");
            var method = HarmonyLib.AccessTools.Method(typeof(SObject), nameof(SObject.placementAction));
            blockedFertilizer = stock; blockedFertilizerAttempts = 0;
            harmony.Patch(method, prefix: new(typeof(ModEntry), nameof(BlockFertilizer)));
            try
            {
                var soil = EmptySoil(23, 20); DragWith(SButton.MouseLeft, 23, 20, 23, 20); Until(() => Control.Paused);
                for (int i = 0; i < 400; i++) Frame();
                Assert(Control.State == "no-effect" && blockedFertilizerAttempts == 3 && stock.Stack == 10 && !soil.HasFertilizer() && Who.CanMove,
                    $"Fertilizer failure: state={Control.State}, attempts={blockedFertilizerAttempts}, stack={stock.Stack}, applied={soil.HasFertilizer()}, move={Who.CanMove}");
            }
            finally { blockedFertilizer = null; harmony.Unpatch(method, HarmonyLib.HarmonyPatchType.All, harmony.Id); }
        });
        Check("empty-watering setting persists through the actual settings menu", () =>
        {
            var menu = new ActionMenu(Config, () => Behavior.Helper.WriteConfig(Config), k => Behavior.Helper.Translation.Get(k));
            menu.SetPage(ActionPage.Settings); ClickSetting(menu, "config.water-empty");
            Assert(Config.WaterEmptySoil && Behavior.Helper.ReadConfig<ModConfig>().WaterEmptySoil, "Empty watering setting did not save");
            Assert(ActionCatalog.All.Any(a => a.Kind == ActionKind.RemoveSoil) && !new ModConfig().Actions.Contains(ActionKind.RemoveSoil), "Missing opt-in soil removal control");
        });
    }
}
