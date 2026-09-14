using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private static bool failPassabilityProbe;
    private static void ThrowProbeCollision()
    {
        if (failPassabilityProbe && PassabilityProbe.Active)
        {
            failPassabilityProbe = false;
            throw new InvalidOperationException("probe fixture failure");
        }
    }
    private void PassabilityTests()
    {
        if (!RequireTestMod("NCarigon.PassableCrops", "Passable Crops compatibility checks"))
            return;
        var mod = Installed<object>("NCarigon.PassableCrops");
        var settings = AccessTools.Property(mod.GetType(), "Config").GetValue(mod)!;
        void Set(string name, object value) => AccessTools.Property(settings.GetType(), name).SetValue(settings, value);
        Set("PassableTreeGrowth", 3);
        Set("PassableFruitTreeGrowth", 2);
        Set("PassableTeaBushes", true);
        Set("PassableSprinklers", true);
        try
        {
            Check("planning probes preserve sapling animation speed and RNG with Passable Crops", () =>
            {
                foreach (int stage in new[] { 1, 2, 3 })
                {
                    var tree = new Tree("1", stage);
                    Map.terrainFeatures[new(20, 25)] = tree;
                    Who.temporarySpeedBuff = 0;
                    var random = Game1.random;
                    var expected = new Random(317);
                    Game1.random = new Random(317);
                    try
                    {
                        Assert(WorldTargets.CanStand(Map, Who, new(20, 25)), "Passable sapling became blocked");
                        Assert(tree.maxShake == 0 && tree.shakeRotation == 0 && Who.temporarySpeedBuff == 0, "Remote probe shook sapling or slowed farmer: stage " + stage);
                        Assert(Game1.random.Next() == expected.Next(), "Probe consumed gameplay RNG");
                    }
                    finally { Game1.random = random; }
                }
            });
            Check("planning probes preserve fruit saplings and tea bush animation", () =>
            {
                var fruit = new FruitTree("628", 1);
                Map.terrainFeatures[new(20, 25)] = fruit;
                var tea = new Bush(new(22, 25), Bush.greenTeaBush, Map);
                Map.terrainFeatures[new(22, 25)] = tea;
                Who.temporarySpeedBuff = 0;
                Assert(WorldTargets.CanStand(Map, Who, new(20, 25)) && WorldTargets.CanStand(Map, Who, new(22, 25)), "Compatible foliage blocked");
                Assert(fruit.maxShake == 0 && tea.shakeTimer == 0 && Who.temporarySpeedBuff == 0, "Probe mutated foliage");
            });
            Check("planning probes do not write object shake metadata even under farmer", () =>
            {
                var sprinkler = ObjectAt("(BC)599", 20, 20);
                Who.temporarySpeedBuff = 0;
                var before = sprinkler.modData.Pairs.ToArray();
                Assert(WorldTargets.CanStand(Map, Who, new(20, 20)), "Passable sprinkler blocked");
                Assert(before.SequenceEqual(sprinkler.modData.Pairs) && Who.temporarySpeedBuff == 0, "Probe changed object metadata or speed");
            });
            Check("repeated route searches leave unselected distant saplings untouched", () =>
            {
                var trees = new List<Tree>();
                for (int x = 22; x < 28; x++)
                    for (int y = 22; y < 26; y++)
                    {
                        var tree = new Tree("1", 2);
                        Map.terrainFeatures[new(x, y)] = tree;
                        trees.Add(tree);
                    }
                var soil = CropAt(30, 20);
                var can = new WateringCan { WaterLeft = 30 };
                Who.Items[0] = can;
                Who.temporarySpeedBuff = 0;
                var targets = WorldTargets.Scan(Map, Who, ToolMode.WateringCan, can, new(30, 20, 1, 1), Config);
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    var search = new RouteSearch(Cell.Of(Who), targets, p => WorldTargets.CanStand(Map, Who, p));
                    while (!search.Finished)
                        search.Step();
                    Assert(search.Result is not null, "No route");
                }
                Assert(trees.All(t => t.maxShake == 0 && t.health.Value == 10) && Who.temporarySpeedBuff == 0, "Path exploration touched remote saplings");
            });
            Check("native collision retains actual passage shake and slowdown", () =>
            {
                var tree = new Tree("1", 2);
                Map.terrainFeatures[new(21, 20)] = tree;
                Who.temporarySpeedBuff = 0;
                bool collision = Map.isCollidingPosition(new Rectangle(21 * 64 + 8, 20 * 64 + 16, 48, 32), Game1.viewport, true, 0, false, Who, pathfinding: false);
                Assert(!collision && tree.maxShake > 0 && Who.temporarySpeedBuff < 0, "Actual collision effects were disabled");
            });
            Check("passability respects disabled compatibility option and mature trees", () =>
            {
                var tree = new Tree("1", 5);
                Map.terrainFeatures[new(24, 20)] = tree;
                Assert(!WorldTargets.CanStand(Map, Who, new(24, 20)), "Mature tree became passable");
                tree.growthStage.Value = 2;
                Set("PassableTreeGrowth", 0);
                try
                {
                    Assert(!WorldTargets.CanStand(Map, Who, new(24, 20)), "Disabled tree option ignored");
                }
                finally { Set("PassableTreeGrowth", 3); }
            });
            Check("selected axe target keeps native damage and shake with compatibility", () =>
            {
                Config.Actions.Add(ActionKind.Sapling);
                var tree = new Tree("1", 3);
                Map.terrainFeatures[new(24, 20)] = tree;
                Select(new Axe(), 24, 20);
                Until(() => tree.health.Value < 10);
                Assert(tree.maxShake > 0, "Selected axe feedback suppressed");
                Finished();
                Assert(!Map.terrainFeatures.ContainsKey(new(24, 20)), "Selected tree not removed");
            });
            Check("watering and scythe sessions leave unselected nearby saplings still", () =>
            {
                foreach (bool scythe in new[] { false, true })
                {
                    Reset();
                    Modern();
                    var trees = new List<Tree>();
                    for (int x = 22; x <= 28; x++)
                    {
                        var tree = new Tree("1", 2);
                        Map.terrainFeatures[new(x, 23)] = tree;
                        trees.Add(tree);
                    }
                    var soil = CropAt(30, 20, scythe);
                    Tool tool = scythe ? new MeleeWeapon("53") : new WateringCan { WaterLeft = 30 };
                    SelectModern(tool, new(30, 20, 1, 1));
                    Until(() =>
                    {
                        Assert(trees.All(t => t.maxShake == 0 && t.shakeRotation == 0 && t.health.Value == 10), "Unselected sapling animated during " + (scythe ? "scythe" : "watering"));
                        return Control.State == "idle";
                    });
                    Assert(scythe ? soil.crop is null : soil.state.Value == 1, "Actual selected work was not completed");
                }
            });
            Check("nested query scopes restore actual collision effects", () =>
            {
                var tree = new Tree("1", 2);
                Map.terrainFeatures[new(21, 20)] = tree;
                Who.temporarySpeedBuff = 0;
                using (PassabilityProbe.Enter())
                {
                    Assert(WorldTargets.CanStand(Map, Who, new(21, 20)) && PassabilityProbe.Active, "Nested scope cleared early");
                    Assert(tree.isPassable(Who) && tree.maxShake == 0 && Who.temporarySpeedBuff == 0, "Outer query lost effect isolation");
                }
                Assert(!PassabilityProbe.Active && tree.isPassable(Who) && tree.maxShake > 0, "Scope leaked into manual interaction");
            });
            Check("failed native collision query restores probe scope", () =>
            {
                var patcher = new Harmony("sznine.BehaviorProbe.CollisionFailure");
                var target = AccessTools.Method(typeof(GameLocation), "isCollidingPosition", new[] { typeof(Rectangle), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(int), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                patcher.Patch(target, prefix: new HarmonyMethod(typeof(ModEntry), nameof(ThrowProbeCollision)));
                bool threw = false;
                failPassabilityProbe = true;
                try
                {
                    WorldTargets.CanStand(Map, Who, new(21, 20));
                }
                catch (InvalidOperationException e) when (e.Message == "probe fixture failure") { threw = true; }
                finally { failPassabilityProbe = false; patcher.Unpatch(target, HarmonyPatchType.Prefix, patcher.Id); }
                Assert(threw && !PassabilityProbe.Active, "Exception leaked query context");
                var tree = new Tree("1", 2);
                Map.terrainFeatures[new(21, 20)] = tree;
                Assert(tree.isPassable(Who) && tree.maxShake > 0, "Post-error manual feedback disabled");
            });
        }
        finally { Set("PassableTreeGrowth", 0); Set("PassableFruitTreeGrowth", 0); Set("PassableTeaBushes", false); Set("PassableSprinklers", false); Who.temporarySpeedBuff = 0; }
    }
}
