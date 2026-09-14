using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void LeftCareTests()
    {
        Check("left selection pets milks and shears with every held item category", () =>
        {
            Item?[] held = { null, new Pickaxe(), new Axe(), new Hoe(), new WateringCan { WaterLeft = 100 }, new MilkPail(), new Shears(), new MeleeWeapon("47"), new MeleeWeapon("0"), ItemRegistry.Create("(O)194", 3), ItemRegistry.Create("(O)286", 3), ItemRegistry.Create("(O)472", 3), ItemRegistry.Create("(O)628", 3), ItemRegistry.Create("(O)GoldenAnimalCracker", 3) };
            foreach (var item in held)
            {
                Reset();
                Modern();
                Who.Items[0] = item!;
                Who.Items[1] = new MilkPail();
                Who.Items[2] = new Shears();
                // Occupied young crop plots keep seed selection explicit without adding planting work here.
                for (int y = 18; y <= 23; y++)
                    for (int x = 23; x <= 30; x++)
                        CropAt(x, y);
                var cow = CareAnimal("White Cow", 99801, 24, 20, "184");
                var sheep = CareAnimal("Sheep", 99802, 29, 20, "440");
                DragWith(SButton.MouseLeft, 23, 18, 30, 23);
                Assert(Control.Board.Jobs.Count(t => t.Kind is ActionKind.Pet or ActionKind.Milk or ActionKind.Shear) == 4, "Missing care with " + item?.Name);
                Assert(Control.Board.Jobs.GroupBy(t => (t.Entity, t.Mode)).All(g => g.Count() == 1), "Duplicate animal task");
                Until(() => Control.State == "idle");
                Assert(cow.wasPet.Value && sheep.wasPet.Value && cow.currentProduce.Value is null && sheep.currentProduce.Value is null, "Care incomplete with " + item?.Name);
                Assert(Who.Items.Any(i => i?.QualifiedItemId == "(O)184") && Who.Items.Any(i => i?.QualifiedItemId == "(O)440"), "Native animal produce absent");
                Assert(Game1.activeClickableMenu is null && !cow.hasEatenAnimalCracker.Value && !sheep.hasEatenAnimalCracker.Value && !Who.isEating && (item is not StardewValley.Object || item.Stack == 3), "Held item used or management opened");
                Assert(ReferenceEquals(Who.CurrentItem, item), "Held item not restored");
            }
        });
        Check("left daily care borrows real stored milk shears and water tools without watering crops", () =>
        {
            Modern();
            var food = ItemRegistry.Create("(O)194", 3);
            Who.Items[0] = food;
            var pail = new MilkPail();
            var shears = new Shears();
            var can = new WateringCan { WaterLeft = 30 };
            var chest = Store(pail);
            chest.Items.Add(shears);
            chest.Items.Add(can);
            var cow = CareAnimal("White Cow", 99803, 24, 20, "184");
            var sheep = CareAnimal("Sheep", 99804, 29, 20, "440");
            var bowl = new PetBowl(new(24, 24));
            Map.buildings.Add(bowl);
            var crop = CropAt(29, 25);
            DragWith(SButton.MouseLeft, 23, 18, 31, 27);
            Assert(Control.Board.Jobs.Any(t => t.Kind == ActionKind.WaterBowl && ReferenceEquals(t.Tool, can)), "Stored bowl tool missing");
            Until(() => Control.State == "idle");
            Assert(cow.currentProduce.Value is null && sheep.currentProduce.Value is null && bowl.watered.Value && can.WaterLeft == 29 && crop.state.Value == 0, "Care crossed into crop automation");
            Assert(new Tool[] { pail, shears, can }.All(t => chest.Items.Contains(t) && !Who.Items.Contains(t)) && ReferenceEquals(Who.CurrentItem, food), "Loan not returned or held food changed");
        });
        Check("left care stays available when right smart matching is disabled and respects action switches", () =>
        {
            Modern();
            Config.SmartActions.Clear();
            Who.Items[0] = ItemRegistry.Create("(O)194", 3);
            Who.Items[1] = new MilkPail();
            Who.Items[2] = new Shears();
            var cow = CareAnimal("White Cow", 99805, 24, 20, "184");
            var sheep = CareAnimal("Sheep", 99806, 29, 20, "440");
            Config.LeftActions.Remove(ActionKind.Shear);
            DragWith(SButton.MouseLeft, 23, 18, 31, 23);
            Assert(Control.Board.Jobs.Any(t => t.Kind == ActionKind.Milk) && !Control.Board.Jobs.Any(t => t.Kind == ActionKind.Shear), "Left linked to right toggle or ignored care switch");
            Until(() => Control.State == "idle");
            Assert(cow.wasPet.Value && sheep.wasPet.Value && cow.currentProduce.Value is null && sheep.currentProduce.Value == "440", "Care switches not honored");
        });
        Check("left missing disabled or inaccessible tools never create animal produce", () =>
        {
            Modern();
            Who.Items[0] = ItemRegistry.Create("(O)194", 3);
            var cow = CareAnimal("White Cow", 99807, 24, 20, "184");
            cow.wasPet.Value = true;
            DragWith(SButton.MouseLeft, 23, 18, 26, 23);
            Assert(!Control.Board.HasSelection && cow.currentProduce.Value == "184", "Absent pail fabricated");
            var pail = new MilkPail();
            var chest = Store(pail);
            Config.UseStoredTools = false;
            DragWith(SButton.MouseLeft, 23, 18, 26, 23);
            Assert(!Control.Board.HasSelection && chest.Items.Contains(pail), "Disabled storage used");
            Config.UseStoredTools = true;
            Config.LeftActions.Remove(ActionKind.Milk);
            DragWith(SButton.MouseLeft, 23, 18, 26, 23);
            Assert(!Control.Board.HasSelection && cow.currentProduce.Value == "184", "Disabled milk used");
        });
        Check("left care rescans milk appearing after petting even when holding a combat weapon", () =>
        {
            Modern();
            Who.Items[0] = new MeleeWeapon("0");
            Who.Items[1] = new MilkPail();
            var cow = CareAnimal("White Cow", 99808, 24, 20);
            DragWith(SButton.MouseLeft, 23, 18, 26, 23);
            Until(() => cow.wasPet.Value);
            for (int i = 0; i < 25; i++)
                Frame();
            cow.currentProduce.Value = "184";
            Until(() => Control.State == "idle");
            Assert(cow.currentProduce.Value is null && Who.Items.Any(i => i?.QualifiedItemId == "(O)184") && Game1.activeClickableMenu is null, "Second care missed");
        });

        Check("left daily pet interaction and hay feeding work with unrelated held food", () =>
        {
            Modern();
            Who.Items[0] = ItemRegistry.Create("(O)194", 3);
            var pet = new Pet(24, 20, Game1.petData["Dog"].Breeds.First().Id, "Dog") { currentLocation = Map };
            Map.characters.Add(pet);
            DragWith(SButton.MouseLeft, 23, 18, 26, 23);
            Until(() => Control.State == "idle");
            Assert(pet.lastPetDay.GetValueOrDefault(Who.UniqueMultiplayerID) == Game1.Date.TotalDays, "Pet interaction missing");
            InHouse((house, outside) =>
            {
                var food = Who.Items[0];
                Who.CurrentToolIndex = 0;
                Who.Items[1] = ItemRegistry.Create("(O)178", 3);
                DragWith(SButton.MouseLeft, 23, 20, 23, 20);
                Until(() => Control.State == "idle");
                Assert(house.Objects.GetValueOrDefault(new(23, 20))?.QualifiedItemId == "(O)178" && Who.Items[1].Stack == 2 && ReferenceEquals(Who.CurrentItem, food), "Left trough feeding failed");
            });
        });
        Check("left animal care never infers axe pickaxe or hoe work", () =>
        {
            Modern();
            Config.Actions.Add(ActionKind.Till);
            Who.Items[0] = ItemRegistry.Create("(O)194", 3);
            Who.Items[1] = new MilkPail();
            Who.Items[2] = new Axe();
            Who.Items[3] = new Pickaxe();
            Who.Items[4] = new Hoe();
            var cow = CareAnimal("White Cow", 99811, 24, 20, "184");
            var tree = new StardewValley.TerrainFeatures.Tree("1", 5);
            Map.terrainFeatures[new(28, 23)] = tree;
            var stone = ObjectAt("(O)343", 29, 23);
            stone.MinutesUntilReady = 1;
            DragWith(SButton.MouseLeft, 23, 18, 31, 24);
            Assert(Control.Board.Jobs.All(t => t.Kind is ActionKind.Pet or ActionKind.Milk), "Left inferred unrelated tool");
            Until(() => Control.State == "idle");
            Assert(cow.currentProduce.Value is null && tree.health.Value == 10 && Map.Objects.ContainsKey(new(29, 23)), "Care damaged unrelated targets");
        });
    }
}
