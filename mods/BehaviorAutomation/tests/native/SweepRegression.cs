using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private static int immatureHarvests, petAttempts;
    private static FarmAnimal? retryAnimal;
    private readonly List<object> swingTrace = new();
    private static void ObserveHarvest(Crop __instance)
    {
        if (!__instance.dead.Value && (__instance.currentPhase.Value < __instance.phaseDays.Count - 1 || __instance.fullyGrown.Value && __instance.dayOfCurrentPhase.Value > 0))
            immatureHarvests++;
    }
    private static bool DelayFirstPet(FarmAnimal __instance)
    {
        return !ReferenceEquals(__instance, retryAnimal) || ++petAttempts > 1;
    }
    private sealed class StickySoil : HoeDirt
    {
        public int Attempts;
        public StickySoil(GameLocation map) : base(0, map) { }
        public override bool performUseAction(Vector2 tile)
        {
            Attempts++;
            return false;
        }
    }
    private int FinishCountingSwings()
    {
        int swings = 0;
        swingTrace.Clear();
        Operation? last = null;
        Until(() =>
        {
            if (Control.Active is { } op && !ReferenceEquals(op, last))
            {
                swings++;
                swingTrace.Add(new { op.Approach.Stand, op.Approach.Facing, op.Approach.Coverage, Remaining = Control.Board.Jobs.Count, Position = Who.Position.ToString() });
                last = op;
            }
            return Control.State == "idle" && Control.Active is null;
        });
        return swings;
    }
    private void SweepTests()
    {
        Check("all three native scythes cover multiple selected crops per swing", () =>
        {
            var metrics = new List<object>();
            foreach (string id in new[] { "47", "53", "66" })
            {
                Reset();
                var crops = new List<HoeDirt>();
                for (int y = 20; y < 24; y++)
                    for (int x = 23; x < 29; x++)
                        crops.Add(CropAt(x, y, true, "483"));
                var scythe = new MeleeWeapon(id);
                Select(scythe, 23, 20, 6, 4);
                int swings = FinishCountingSwings();
                Assert(crops.All(c => c.crop is null), "Unharvested batch " + id);
                Assert(swings > 0 && swings <= 2, "Scythe fragmented a two-sweep bed: " + id + " swings=" + swings);
                Assert(Who.Stamina == 1000, "Scythe consumed stamina");
                metrics.Add(new
                {
                    Scythe = id,
                    SelectedCrops = 24,
                    Swings = swings
                    , Trace = swingTrace.ToArray()
                });
            }
            File.WriteAllText(Path.Combine(Evidence, "scythe-metrics.json"), System.Text.Json.JsonSerializer.Serialize(metrics));
        });

        Check("native sweep planner preserves RNG state and uses a reachable high-coverage stance", () =>
        {
            var scythe = new MeleeWeapon("47");
            Who.Items[0] = scythe;
            for (int x = 23; x < 28; x++)
                CropAt(x, 20, true, "483");
            var targets = WorldTargets.Scan(Map, Who, ToolMode.Scythe, scythe, new(23, 20, 5, 1), Config);
            var original = Game1.random;
            var probe = new Random(173);
            Game1.random = probe;
            try
            {
                var plans = ScythePlanner.Approaches(targets, Who).ToArray();
                Assert(ReferenceEquals(Game1.random, probe) && probe.Next() == new Random(173).Next(), "Planner altered native random stream");
                var route = new RouteSearch(Cell.Of(Who), targets, p => WorldTargets.CanStand(Map, Who, p), scytheFarmer: Who);
                while (!route.Finished)
                    route.Step();
                Assert(route.Result is { Coverage: > 1 } && route.Path().All(p => WorldTargets.CanStand(Map, Who, p)), "No reachable group stance");
            }
            finally { Game1.random = original; }
        });
        Check("no native harvest attempts on young or regrowing crops including during iridium swings", () =>
        {
            var harmony = new Harmony("sznine.BehaviorProbe.HarvestReadiness");
            immatureHarvests = 0;
            harmony.Patch(AccessTools.Method(typeof(Crop), nameof(Crop.harvest)), prefix: new HarmonyMethod(typeof(ModEntry), nameof(ObserveHarvest)));
            try
            {
                foreach (string id in new[] { "47", "53", "66" })
                {
                    Reset();
                    var ripe = CropAt(23, 20, true, "473");
                    var young = CropAt(24, 20, false, "483");
                    var regrow = CropAt(25, 20, true, "473");
                    regrow.crop!.fullyGrown.Value = true;
                    regrow.crop.dayOfCurrentPhase.Value = 3;
                    var outside = CropAt(23, 21, true, "483");
                    var pot = new IndoorPot(new(24, 21));
                    pot.hoeDirt.Value.crop = new Crop("472", 24, 21, Map);
                    Map.Objects[new(24, 21)] = pot;
                    var scythe = new MeleeWeapon(id);
                    Select(scythe, 23, 20, 3, 1);
                    Assert(Control.Board.Jobs.Count == 1, "Selected immature crop");
                    FinishCountingSwings();
                    Assert(ripe.crop is { } crop && crop.fullyGrown.Value && crop.dayOfCurrentPhase.Value > 0, "Regrow state damaged");
                    Assert(young.crop is not null && !young.crop.dead.Value && regrow.crop.dayOfCurrentPhase.Value == 3 && outside.crop is not null && pot.hoeDirt.Value.crop is not null, "Young or outside crop damaged");
                }
                Assert(immatureHarvests == 0, "Crop.harvest invoked on immature crop: " + immatureHarvests);
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        });
        Check("target readiness changing after selection is checked before any scythe action", () =>
        {
            var ripe = CropAt(28, 20, true, "473");
            Select(new MeleeWeapon("66"), 28, 20);
            ripe.crop!.fullyGrown.Value = true;
            ripe.crop.dayOfCurrentPhase.Value = 4;
            int swings = FinishCountingSwings();
            Assert(swings == 0 && ripe.crop.dayOfCurrentPhase.Value == 4, "Stale target caused a swing");
        });
        Check("left selection allows hand interaction with food weapons seeds and every tool", () =>
        {
            Item[] held = { ItemRegistry.Create("(O)194", 3), ItemRegistry.Create("(O)286", 3), new MeleeWeapon("0"), new Pickaxe(), new Axe(), new Hoe(), new WateringCan(), new MilkPail(), new Shears(), new MeleeWeapon("47"), ItemRegistry.Create("(O)472", 3), ItemRegistry.Create("(O)628", 3) };
            foreach (var item in held)
            {
                Reset();
                Who.Items[0] = item;
                var cow = new FarmAnimal("White Cow", 99501, Who.UniqueMultiplayerID) { Position = new(24 * 64, 20 * 64), currentLocation = Map };
                cow.wasPet.Value = false;
                Map.animals.Add(cow.myID.Value, cow);
                DragWith(SButton.MouseLeft, 23, 19, 26, 22);
                Assert(Control.Board.Jobs.Any(t => t.Kind == ActionKind.Pet && t.Tool is null), "Missing left hand interaction for " + item.Name);
                try { Until(() => cow.wasPet.Value); }
                catch (Exception ex) { throw new Exception("Held " + item.QualifiedItemId + ": " + ex.Message, ex); }
                Assert(!Who.isEating && Game1.activeClickableMenu is null && item.Stack > 0, "Held item used instead of petting: " + item.Name);
            }
        });
        Check("left held tool gathers crops forage and machine output without switching selection tools", () =>
        {
            var pick = new Pickaxe();
            Who.Items[0] = pick;
            var crop = CropAt(23, 20, true);
            var forage = ObjectAt("(O)16", 24, 20);
            forage.isSpawnedObject.Value = true;
            var machine = ObjectAt("(BC)12", 25, 20);
            machine.heldObject.Value = ItemRegistry.Create<StardewValley.Object>("(O)344");
            machine.readyForHarvest.Value = true;
            DragWith(SButton.MouseLeft, 23, 20, 25, 20);
            Assert(Control.Board.Jobs.Count == 3 && Control.Board.Jobs.All(t => t.Mode == ToolMode.Hand), "Hand actions not combined");
            Until(() => Control.State == "idle");
            Assert(crop.crop is null && !Map.Objects.ContainsKey(new(24, 20)) && machine.heldObject.Value is null && ReferenceEquals(Who.CurrentItem, pick), "Hand collection or held tool restoration failed");
        });

        Check("daily animal interaction is retried after one ineffective native call without opening management", () =>
        {
            var cow = new FarmAnimal("White Cow", 99502, Who.UniqueMultiplayerID) { Position = new(24 * 64, 20 * 64), currentLocation = Map };
            cow.wasPet.Value = false;
            Map.animals.Add(cow.myID.Value, cow);
            var harmony = new Harmony("sznine.BehaviorProbe.PetRetry");
            petAttempts = 0;
            retryAnimal = cow;
            harmony.Patch(AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.pet)), prefix: new HarmonyMethod(typeof(ModEntry), nameof(DelayFirstPet)));
            try
            {
                SmartSelect(null, new(23, 19, 4, 4));
                Until(() => Control.State == "idle");
                Assert(cow.wasPet.Value && petAttempts == 2 && Game1.activeClickableMenu is null, "Daily retry or management guard failed");
            }
            finally { retryAnimal = null; harmony.UnpatchAll(harmony.Id); }
        });
        Check("selected animal is rescanned for produce appearing after petting", () =>
        {
            var cow = new FarmAnimal("White Cow", 99503, Who.UniqueMultiplayerID) { Position = new(24 * 64, 20 * 64), currentLocation = Map };
            cow.age.Value = 30;
            cow.wasPet.Value = false;
            cow.currentProduce.Value = null;
            Map.animals.Add(cow.myID.Value, cow);
            Who.Items[1] = new MilkPail();
            SmartSelect(null, new(23, 19, 4, 4));
            Until(() => cow.wasPet.Value);
            cow.currentProduce.Value = "184";
            Until(() => Control.State == "idle");
            Assert(cow.currentProduce.Value is null && Who.Items.Any(i => i?.QualifiedItemId == "(O)184") && Game1.activeClickableMenu is null, "Follow-up milk not detected");
        });
        Check("region refresh observes moving animals and respects category switches", () =>
        {
            Who.Items[0] = new Pickaxe();
            var cow = new FarmAnimal("White Cow", 99504, Who.UniqueMultiplayerID) { Position = new(30 * 64, 20 * 64), currentLocation = Map };
            cow.wasPet.Value = false;
            Map.animals.Add(cow.myID.Value, cow);
            var initial = ObjectAt("(O)16", 25, 21);
            initial.isSpawnedObject.Value = true;
            DragWith(SButton.MouseLeft, 23, 19, 26, 22);
            cow.Position = new(24 * 64, 20 * 64);
            Until(() => Control.State == "idle");
            Assert(cow.wasPet.Value, "Animal entering active region missed");
            Control.Clear();
            cow.wasPet.Value = false;
            Config.Actions.Remove(ActionKind.Pet);
            Config.LeftActions.Remove(ActionKind.Pet);
            DragWith(SButton.MouseLeft, 23, 19, 26, 22);
            Until(() => Control.State == "idle");
            Assert(!cow.wasPet.Value, "Disabled petting reintroduced");
        });
        Check("no-effect targets stay rejected through final region rescans", () =>
        {
            var soil = new StickySoil(Map) { crop = new Crop("472", 24, 20, Map) };
            soil.crop.currentPhase.Value = soil.crop.phaseDays.Count - 1;
            Map.terrainFeatures[new(24, 20)] = soil;
            Select(null, 24, 20);
            Until(() => Control.Paused);
            Assert(soil.Attempts == 3, "Wrong no-effect bound");
            Control.Resume();
            Until(() => Control.State == "idle");
            Assert(soil.Attempts == 3 && Control.Board.Refresh(Who, Config) == 0, "Failed target was requeued");
        });
        Check("finished regions stop watching and canceled regions cannot resurrect tasks", () =>
        {
            var cow = new FarmAnimal("White Cow", 99505, Who.UniqueMultiplayerID) { Position = new(24 * 64, 20 * 64), currentLocation = Map };
            cow.wasPet.Value = false;
            Map.animals.Add(cow.myID.Value, cow);
            Select(null, 23, 19, 4, 4);
            Until(() => Control.State == "idle");
            cow.wasPet.Value = false;
            for (int i = 0; i < 60; i++)
                Frame();
            Assert(!cow.wasPet.Value && Control.Board.Jobs.Count == 0, "Completed region kept running");
            Control.Clear();
            Select(null, 23, 19, 4, 4);
            Control.Clear();
            for (int i = 0; i < 60; i++)
                Frame();
            Assert(!cow.wasPet.Value && !Control.Board.HasSelection, "Canceled region revived");
        });
    }
}
