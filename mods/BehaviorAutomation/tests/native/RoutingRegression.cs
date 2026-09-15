using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using StardewValley.Objects;
using Sznine.BehaviorAutomation;
using HarmonyLib;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void RoutingRegression()
    {
        Check("gold mixed forage and regrowing bed never starts an unowned follow-up swing", () =>
        {
            var ripe = CropAt(23, 20, true, "473");
            var young = CropAt(25, 20, false, "483");
            var cooldown = CropAt(24, 21, true, "473"); cooldown.crop!.fullyGrown.Value = true; cooldown.crop.dayOfCurrentPhase.Value = 3;
            var forage = ObjectAt("(O)16", 24, 20); forage.isSpawnedObject.Value = true;
            var harmony = new Harmony("sznine.BehaviorProbe.GoldMixedHarvest");
            immatureHarvests = 0;
            harmony.Patch(AccessTools.Method(typeof(Crop), nameof(Crop.harvest)), prefix: new HarmonyMethod(typeof(ModEntry), nameof(ObserveHarvest)));
            try
            {
                Select(new MeleeWeapon("53"), 23, 20, 3, 2);
                int unowned = 0;
                for (int i = 0; i < 1500; i++)
                {
                    Frame();
                    if (Who.UsingTool && Control.Active is null) unowned++;
                    if (Control.State == "idle" && !Who.UsingTool) break;
                }
                Assert(unowned == 0 && immatureHarvests == 0, $"Unowned tool frames={unowned}, immature harvest attempts={immatureHarvests}");
                Assert(ripe.crop!.dayOfCurrentPhase.Value > 0 && young.crop is not null && cooldown.crop.dayOfCurrentPhase.Value == 3 && !Map.Objects.ContainsKey(new(24, 20)), "Mixed-bed collection failed");
                Assert(Who.Items.Any(i => i?.QualifiedItemId == "(O)16") && Who.Items.Any(i => i?.QualifiedItemId == "(W)53") && !Who.netItemStowed.Value, "Pickup or held scythe lost");
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        });
        Check("open diagonal destination uses a shorter diagonal route", () =>
        {
            var stone = ObjectAt("(O)343", 29, 29);
            var pick = new Pickaxe(); Who.Items[0] = pick;
            var targets = WorldTargets.Scan(Map, Who, ToolMode.Pickaxe, pick, new(29, 29, 1, 1), Config);
            var search = new RouteSearch(Cell.Of(Who), targets, p => WorldTargets.CanStand(Map, Who, p));
            while (!search.Finished) search.Step();
            var path = search.Path(); var previous = Cell.Of(Who); double length = 0; bool diagonal = false;
            foreach (var point in path)
            {
                int dx = point.X - previous.X, dy = point.Y - previous.Y;
                diagonal |= dx != 0 && dy != 0; length += Math.Sqrt(dx * dx + dy * dy); previous = point;
            }
            Assert(diagonal && length < 14, $"Still axis-only: {length} tiles");
        });
        Check("default movement cancellation completes after half a second", () =>
        {
            CropAt(31, 20); SelectModern(new WateringCan { WaterLeft = 30 }, new(31, 20, 1, 1));
            for (int i = 0; i < 5; i++) Control.ObserveMovement(Who, true, 100);
            Assert(!Control.Board.HasSelection && Who.controller is null, "Half-second movement did not cancel");
        });
        Check("arrival facing chooses the largest live sweep rather than a weaker planned facing", () =>
        {
            var scythe = new MeleeWeapon("47"); Who.Items[0] = scythe;
            for (int x = 21; x < 24; x++) CropAt(x, 20, true, "483");
            var targets = WorldTargets.Scan(Map, Who, ToolMode.Scythe, scythe, new(21, 20, 3, 1), Config);
            var best = ScythePlanner.AtStand(targets, Who, scythe)!;
            Assert(best.Coverage > 1, "Fixture has no multi-hit facing");
            for (int face = 0; face < 4; face++)
                Assert(ScythePlanner.AtStand(targets, Who, scythe, face)!.Coverage == best.Coverage, "A stale facing reduced actual coverage");
        });
        Check("spring onion forage crop is harvestable without normal crop phases", () =>
        {
            var soil = new HoeDirt(0, Map) { crop = new Crop(true, "1", 23, 20, Map) };
            Map.terrainFeatures[new(23, 20)] = soil;
            Select(new MeleeWeapon("47"), 23, 20);
            Assert(Control.Board.Jobs.Count == 1, "Forage crop discarded by normal growth-phase check");
            FinishCountingSwings(); Assert(soil.crop is null, "Mature forage crop was not harvested");
        });
        Check("diagonal routing cannot squeeze between blocked corners", () =>
        {
            var target = new WorkTarget { Entity = new Cell(3, 3), Origin = new(3, 3), Area = new(3, 3, 1, 1), Kind = ActionKind.Stone };
            var route = new RouteSearch(new(0, 0), new[] { target }, p => p.X >= 0 && p.Y >= 0 && p.X < 5 && p.Y < 5 && p != new Cell(1, 0) && p != new Cell(0, 1));
            while (!route.Finished) route.Step();
            Assert(route.Result is null, "Crossed an impassable diagonal corner");
        });
        Check("diagonal route setting and native execution agree", () =>
        {
            var target = new WorkTarget { Entity = new Cell(29, 29), Origin = new(29, 29), Area = new(29, 29, 1, 1), Kind = ActionKind.Stone };
            var measures = new List<object>();
            foreach (bool diagonal in new[] { false, true })
            {
                Reset(); Config.AllowDiagonalMovement = diagonal;
                var route = new RouteSearch(Cell.Of(Who), new[] { target }, p => WorldTargets.CanStand(Map, Who, p), settings: Config);
                while (!route.Finished) route.Step();
                var points = route.Path(); var walker = new WalkRoute(Who, Map, points); Who.controller = walker;
                double distance = 0; int frames = 0;
                while (!walker.Finished && frames++ < 1000)
                {
                    var previous = Who.Position; WalkFrame(walker, 16); distance += Vector2.Distance(previous, Who.Position);
                }
                Assert(walker.Finished && !walker.Failed, "Diagonal native walk stalled");
                measures.Add(new { diagonal, distance, frames });
                if (diagonal) Assert(distance < 14 * 64, $"Native motion wasted travel: {distance}");
                else Assert(points.Count == 17, "Disabled diagonals still in search");
            }
            File.WriteAllText(Path.Combine(Evidence, "routing-distance.json"), System.Text.Json.JsonSerializer.Serialize(measures));
        });
        Check("movement cancellation duration is configurable and release resets the timer", () =>
        {
            Config.MovementCancelSeconds = 1;
            CropAt(31, 20); SelectModern(new WateringCan { WaterLeft = 30 }, new(31, 20, 1, 1));
            for (int i = 0; i < 7; i++) Control.ObserveMovement(Who, true, 100);
            Assert(Control.Board.HasSelection, "Custom delay ignored");
            Control.ObserveMovement(Who, false, 0);
            for (int i = 0; i < 9; i++) Control.ObserveMovement(Who, true, 100);
            Assert(Control.Board.HasSelection, "Released movement did not reset delay");
            Control.ObserveMovement(Who, true, 100);
            Assert(!Control.Board.HasSelection, "Custom cancellation deadline missed");
        });
        Check("gold scythe never selects young or cooldown-only beds", () =>
        {
            foreach (string seed in new[] { "472", "473", "481", "493", "431", "483", "885" })
            {
                Reset();
                var young = CropAt(23, 20, false, seed);
                var regrow = CropAt(24, 20, true, seed); regrow.crop!.fullyGrown.Value = true; regrow.crop.dayOfCurrentPhase.Value = 3;
                Select(new MeleeWeapon("53"), 23, 20, 2, 1);
                Assert(!Control.HasSession && Control.Active is null, "Immature bed selected: " + seed);
                for (int i = 0; i < 100; i++) Frame();
                Assert(Control.Active is null && !Who.UsingTool && young.crop is not null && regrow.crop.dayOfCurrentPhase.Value == 3, "Swung at immature plants: " + seed);
            }
        });
        Check("gold scythe harvests one round of regrowing crops and stops until ripe again", () =>
        {
            foreach (string seed in new[] { "473", "481", "493" })
            {
                Reset();
                var plants = Enumerable.Range(23, 4).Select(x => CropAt(x, 20, true, seed)).ToArray();
                Select(new MeleeWeapon("53"), 23, 20, 4, 1);
                int swings = FinishCountingSwings();
                Assert(swings <= 2 && plants.All(p => p.crop is { } c && c.fullyGrown.Value && c.dayOfCurrentPhase.Value > 0), "Repeated cooldown swings: " + seed);
                Select(Who.CurrentTool, 23, 20, 4, 1);
                Assert(!Control.HasSession, "Cooldown plants returned on reselect: " + seed);
                foreach (var plant in plants) plant.crop!.dayOfCurrentPhase.Value = 0;
                Select(new MeleeWeapon("53"), 23, 20, 4, 1);
                Assert(Control.Board.Jobs.Count == 4, "Ripe regrowing plants excluded: " + seed);
                FinishCountingSwings();
                Assert(plants.All(p => p.crop!.dayOfCurrentPhase.Value > 0), "Second valid harvest missed: " + seed);
            }
        });
        Check("gold scythe stops a committed approach when crops enter regrowth", () =>
        {
            var crop = CropAt(29, 25, true, "473"); Select(new MeleeWeapon("53"), 29, 25);
            Until(() => Who.controller is not null);
            crop.crop!.fullyGrown.Value = true; crop.crop.dayOfCurrentPhase.Value = 3;
            int swings = FinishCountingSwings();
            Assert(swings == 0 && crop.crop.dayOfCurrentPhase.Value == 3, "Continued toward already-harvested crop");
        });
        Check("gold scythe harvests ripe pots once and keeps regrowing pots unselected", () =>
        {
            var pot = new IndoorPot(new(23, 20));
            pot.hoeDirt.Value.crop = new Crop("473", 23, 20, Map);
            pot.hoeDirt.Value.crop.currentPhase.Value = pot.hoeDirt.Value.crop.phaseDays.Count - 1;
            Map.Objects[new(23, 20)] = pot;
            Select(new MeleeWeapon("53"), 23, 20); FinishCountingSwings();
            Assert(pot.hoeDirt.Value.crop is { } c && c.fullyGrown.Value && c.dayOfCurrentPhase.Value > 0, "Pot regrowth not respected");
        });
        Check("native harvest entry guard blocks unripe crops even when another caller skips the soil hook", () =>
        {
            var ripe = CropAt(23, 20, true, "483"); var young = CropAt(24, 20, false, "483");
            Select(new MeleeWeapon("53"), 23, 20, 2, 1); Until(() => Control.Active is not null);
            Assert(!Control.AllowHarvest(young.crop!, young, 24, 20), "Unripe direct harvest allowed");
            Assert(Control.AllowHarvest(ripe.crop!, ripe, 23, 20), "Ready crop blocked");
            ripe.crop!.fullyGrown.Value = true; ripe.crop.dayOfCurrentPhase.Value = 4;
            Assert(!Control.AllowHarvest(ripe.crop, ripe, 23, 20), "Harvested-in-this-swing crop allowed again");
            Until(() => Control.Active is null); Control.Clear();
            Assert(Control.AllowHarvest(young.crop!, young, 24, 20), "Ordinary manual harvest intercepted");
        });
        Check("disabled automatic refill pauses without searching or watering", () =>
        {
            Config.AutoRefillWateringCan = false; var crop = CropAt(25, 20);
            Select(new WateringCan { WaterLeft = 0 }, 25, 20);
            Until(() => Control.Paused);
            Assert(Control.State == "water" && Who.controller is null && crop.state.Value == 0, "Refill toggle ignored");
        });
        Check("invalid operational parameters are clamped and legacy independent action pools survive", () =>
        {
            var options = new ModConfig { ConfigVersion = 7, MovementCancelSeconds = float.NaN, ScytheSearchTiles = 999,
                ScytheSwingCost = -1, RefreshIntervalSeconds = 0, CompletionDelaySeconds = float.PositiveInfinity };
            options.LeftActions.Clear(); options.Migrate();
            Assert(options.MovementCancelSeconds == .5f && options.ScytheSearchTiles == 24 && options.ScytheSwingCost == 4
                && options.RefreshIntervalSeconds == .1f && options.CompletionDelaySeconds == 1.5f && options.LeftActions.Count == 0, "Migration/clamps changed user choices");
        });
    }
}
