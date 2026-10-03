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
    private void BuildFieldLiveCases()
    {
        liveCases.Enqueue(new("left-global-scythe", () =>
        {
            var pick = new Pickaxe(); Who.Items[0] = pick;
            var scythe = new MeleeWeapon("53"); liveLoan = scythe; liveChest = Store(scythe);
            for (int x = 23; x < 27; x++) for (int y = 20; y < 23; y++) liveRipe.Add(CropAt(x, y, true));
            for (int x = 27; x < 31; x++)
            {
                var forage = ObjectAt("(O)16", x, 20); forage.isSpawnedObject.Value = true; liveDebris.Add(forage);
                liveDebris.Add(ObjectAt("(O)0", x, 21));
                Map.terrainFeatures[new(x, 22)] = new Grass(1, 3);
                liveCrops.Add(CropAt(x, 23));
            }
            foreach (string id in new[] { "(O)93", "(O)645", "(O)463" })
            { var prop = ObjectAt(id, 24 + protectedProps.Count * 2, 24); protectedProps[prop.TileLocation] = prop; }
            DragWith(SButton.MouseLeft, 22, 19, 32, 25);
            Assert(Control.Board.Jobs.Count > 0 && Control.Board.Jobs.All(t => t.Mode == ToolMode.Scythe), "Left global targets did not choose the stored scythe");
        }, () => liveRipe.All(s => s.crop is null) && liveCrops.All(s => s.crop is { dead.Value: false })
            && liveDebris.All(o => !Map.Objects.ContainsKey(o.TileLocation)) && Map.terrainFeatures.Values.All(t => t is not Grass)
            && liveChest!.Items.Contains(liveLoan!) && !Who.Items.Contains(liveLoan!) && liveOperations > 0));

        SObject? fertilizer = null, spare = null;
        liveCases.Enqueue(new("held-fertilizer", () =>
        {
            fertilizer = ItemRegistry.Create<SObject>("(O)368", 60); Who.Items[0] = fertilizer;
            spare = ItemRegistry.Create<SObject>("(O)369", 20); Who.Items[1] = spare;
            Who.Items[2] = new MeleeWeapon("66");
            for (int x = 23; x < 29; x++) for (int y = 20; y < 25; y++)
                liveCrops.Add(y % 2 == 0 ? EmptySoil(x, y) : CropAt(x, y));
            for (int x = 29; x < 32; x++)
            {
                var pot = new IndoorPot(new(x, 23)); Map.Objects[pot.TileLocation] = pot; liveCrops.Add(pot.hoeDirt.Value);
                liveDebris.Add(ObjectAt("(O)0", x, 21));
            }
            var torch = ObjectAt("(O)93", 25, 25); protectedProps[torch.TileLocation] = torch;
            DragWith(SButton.MouseLeft, 22, 19, 32, 25);
        }, () => liveCrops.Count == 33 && liveCrops.All(s => ItemRegistry.QualifyItemId(s.fertilizer.Value) == "(O)368")
            && fertilizer!.Stack == 27 && spare!.Stack == 20 && liveDebris.All(o => !Map.Objects.ContainsKey(o.TileLocation))));

        liveCases.Enqueue(new("water-empty-bed", () =>
        {
            Config.WaterEmptySoil = true;
            Who.Items[0] = new WateringCan { UpgradeLevel = 4, WaterLeft = 0 };
            for (int x = 23; x < 35; x++) for (int y = 20; y < 26; y++) liveCrops.Add(EmptySoil(x, y));
            foreach (var at in new[] { new Point(25, 21), new Point(30, 24), new Point(34, 20) })
            { var sprinkler = ObjectAt("(O)645", at.X, at.Y); protectedProps[sprinkler.TileLocation] = sprinkler; }
            Map.Map.GetLayer("Back").Tiles[19, 26].Properties["WaterSource"] = "T";
            DragWith(SButton.MouseLeft, 23, 20, 34, 25);
        }, () => liveCrops.Count == 72 && liveCrops.All(s => s.state.Value == 1 && s.crop is null) && liveRefills > 0));

        var cleared = new List<Vector2>();
        liveCases.Enqueue(new("clear-empty-bed", () =>
        {
            Config.Actions.Add(ActionKind.RemoveSoil); Who.Items[0] = new Pickaxe(); cleared.Clear();
            for (int x = 23; x < 29; x++) for (int y = 20; y < 23; y++)
            { EmptySoil(x, y); cleared.Add(new(x, y)); }
            for (int x = 23; x < 29; x++) liveCrops.Add(CropAt(x, 23));
            var covered = EmptySoil(29, 22); liveCrops.Add(covered);
            var sprinkler = ObjectAt("(O)645", 29, 22); protectedProps[sprinkler.TileLocation] = sprinkler;
            DragWith(SButton.MouseLeft, 23, 20, 30, 23);
        }, () => cleared.All(p => !Map.terrainFeatures.ContainsKey(p))
            && liveCrops.All(s => Map.terrainFeatures.Values.Contains(s)) && liveCrops.Count(s => s.crop is not null) == 6));
    }
}
