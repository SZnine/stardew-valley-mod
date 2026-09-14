using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
using BehaviorMod = Sznine.BehaviorAutomation.ModEntry;
using SObject = StardewValley.Object;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void PreservedInteractionTests()
    {
        Check("held pickaxe yields weeds to scythe with zero native stamina debit", () =>
        {
            var pick = new Pickaxe();
            Who.Items[0] = pick;
            Who.Items[1] = new MeleeWeapon("47");
            var weed = ObjectAt("(O)0", 23, 20);
            weed.Name = "Weeds";
            weed.Category = -999;
            SmartSelect(pick, new(23, 20, 1, 1));
            Assert(Control.Board.Jobs.Single().Mode == ToolMode.Scythe, "Pickaxe priority survived");
            Finished();
            Assert(!Map.Objects.ContainsKey(new(23, 20)) && Who.Stamina == 1000, "Weed spent stamina");
        });
        Check("ordinary scythe harvests grab crops without destroying young or outside crops", () =>
        {
            var a = CropAt(23, 20, true);
            var young = CropAt(24, 20);
            var outside = CropAt(23, 21, true);
            Select(new MeleeWeapon("47"), 23, 20, 2, 1);
            Finished();
            Assert(a.crop is null && young.crop is not null && !young.crop.dead.Value && outside.crop is not null, "Harvest scope wrong");
            Assert(Who.Stamina == 1000 && Map.debris.Any(d => d.item?.QualifiedItemId == "(O)24") && Who.experiencePoints[0] > 0, "Native crop result or XP missing");
        });
        Check("ordinary scythe preserves regrowing crop and harvests garden pots", () =>
        {
            var soil = CropAt(23, 20, true, "473");
            var crop = soil.crop!;
            var pot = new IndoorPot(new(24, 20));
            Map.Objects[new(24, 20)] = pot;
            pot.hoeDirt.Value.crop = new Crop("472", 24, 20, Map);
            pot.hoeDirt.Value.crop.currentPhase.Value = pot.hoeDirt.Value.crop.phaseDays.Count - 1;
            Select(new MeleeWeapon("47"), 23, 20, 2, 1);
            Finished();
            Assert(ReferenceEquals(soil.crop, crop) && crop.fullyGrown.Value && crop.dayOfCurrentPhase.Value > 0 && pot.hoeDirt.Value.crop is null, "Regrowth or pot failed");
        });
        Check("scythe collects actual ground forage and fruit without damaging trees", () =>
        {
            var forage = ObjectAt("(O)16", 23, 20);
            forage.isSpawnedObject.Value = true;
            var fruit = new FruitTree("628", 5);
            fruit.fruit.Add(ItemRegistry.Create("(O)613"));
            Map.terrainFeatures[new(25, 20)] = fruit;
            Select(new MeleeWeapon("47"), 23, 20, 3, 1);
            Assert(Control.Board.Jobs.Count == 2, "Missing scythe gathering");
            Finished();
            Assert(!Map.Objects.ContainsKey(new(23, 20)) && Who.Items.Any(i => i?.QualifiedItemId == "(O)16") && fruit.fruit.Count == 0 && fruit.health.Value == 10, "Gather failed or tree damaged");
        });
        Check("scythe removes moss without chopping even a tapped tree", () =>
        {
            var tree = new Tree("1", 5);
            tree.hasMoss.Value = true;
            tree.tapped.Value = true;
            Map.terrainFeatures[new(23, 20)] = tree;
            Select(new MeleeWeapon("47"), 23, 20);
            Assert(Control.Board.Jobs.Single().Kind == ActionKind.Forage, "Moss missing");
            var trace = new List<string>();
            Operation? previous = null;
            Until(() => { if (Control.Active is { } op && op != previous) { previous = op; var at = Cell.Of(Who); bool reachable = ScythePlanner.Footprint((MeleeWeapon)op.Approach.Target.Tool!, Who)[op.Approach.Facing].Contains(new Cell(23 - at.X, 20 - at.Y)); trace.Add($"stand={op.Approach.Stand.X}/{op.Approach.Stand.Y},tile={at.X}/{at.Y},aim={op.Approach.Aim},face={op.Approach.Facing},body={Who.GetBoundingBox()},predicted={reachable}"); } return Control.Board.Jobs.Count == 0 && Control.Active is null; });
            Assert(!tree.hasMoss.Value && tree.health.Value == 10 && tree.tapped.Value && Who.Stamina == 1000 && Map.debris.Any(d => d.item?.QualifiedItemId == "(O)Moss"), $"Moss native outcome wrong: moss={tree.hasMoss.Value}, health={tree.health.Value}, tapped={tree.tapped.Value}, stamina={Who.Stamina}, debris={string.Join(",", Map.debris.Select(d => d.item?.QualifiedItemId ?? "null"))}, notices={string.Join(",", notices)}, pos={Who.Position}, trace={string.Join(";", trace)}");
        });
        Check("green rain leaf clump uses scythe and native drops", () =>
        {
            var clump = new ResourceClump(44, 2, 2, new(23, 20));
            Assert(clump.IsGreenRainBush(), "Fixture clump id");
            Map.resourceClumps.Add(clump);
            var pick = new Pickaxe();
            Who.Items[0] = pick;
            Who.Items[1] = new MeleeWeapon("53");
            SmartSelect(pick, new(23, 20, 2, 2));
            Assert(Control.Board.Jobs.Single().Mode == ToolMode.Scythe, "Leaf tool wrong");
            Finished();
            Assert(!Map.resourceClumps.Contains(clump) && Map.debris.Count > 0 && Who.Stamina == 1000, "Leaf native outcome wrong");
        });
        Check("stored tool is borrowed as its original instance and returned with consumed water", () =>
        {
            var can = new WateringCan { UpgradeLevel = 2, WaterLeft = 30 };
            can.modData["fixture"] = "retained";
            var chest = Store(can);
            var crop = CropAt(25, 20);
            SmartSelect(null, new(25, 20, 1, 1));
            Assert(ReferenceEquals(Control.Board.Jobs.Single().Tool, can), "Stored tool not selected");
            Until(() => Control.Active is not null);
            Assert(Who.Items.Contains(can) && !chest.Items.Contains(can), "Loan did not move actual tool");
            Finished();
            Assert(crop.state.Value == 1 && can.WaterLeft == 29 && can.modData["fixture"] == "retained" && chest.Items.Contains(can) && !Who.Items.Contains(can), $"Loan return/state failed: crop={crop.state.Value},water={can.WaterLeft},chest={chest.Items.Contains(can)},pack={Who.Items.Contains(can)}");
        });
    }
}
