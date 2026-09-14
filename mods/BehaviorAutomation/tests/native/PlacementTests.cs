using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private void PlacementTests()
    {
        Check("held axe is allowed while left extras and right pool forbid felling", () =>
        {
            Config.LeftActions.Clear();
            Config.SmartActions.Clear();
            var tree = new Tree("1", 5);
            Map.terrainFeatures[new(24, 20)] = tree;
            var axe = new Axe { UpgradeLevel = 4 };
            Who.Items[1] = axe;
            SelectModern(null, new(24, 20, 1, 1));
            Assert(!Control.Board.HasSelection, "Empty hand inferred held-only axe");
            SmartSelect(axe, new(24, 20, 1, 1));
            Assert(!Control.Board.HasSelection, "Right used held-only permission");
            SelectModern(axe, new(24, 20, 1, 1));
            Until(() => Control.State == "idle");
            Assert(!Map.terrainFeatures.ContainsKey(new(24, 20)), "Held-only tree/stump work failed");
        });
        Check("left extras borrow a milk pail even when held and right actions are disabled", () =>
        {
            Config.Actions.Clear();
            Config.SmartActions.Clear();
            Config.LeftActions = new() { ActionKind.Milk };
            Who.Items[0] = ItemRegistry.Create("(O)194", 2);
            Store(new MilkPail());
            var cow = CareAnimal("White Cow", 99910, 24, 20, "184");
            cow.wasPet.Value = true;
            DragWith(SButton.MouseLeft, 24, 20, 25, 21);
            Until(() => Control.State == "idle");
            Assert(cow.currentProduce.Value is null && Who.Items.Any(i => i?.QualifiedItemId == "(O)184"), "Left global still depends on held settings");
        });
        Check("floors fill the selected rectangle and skip old flooring crops and objects", () =>
        {
            Config.LeftActions.Clear();
            var stack = ItemRegistry.Create<StardewValley.Object>("(O)328", 10);
            Who.Items[0] = stack;
            Map.terrainFeatures[new(24, 20)] = new Flooring("1");
            var crop = CropAt(25, 20);
            var chest = Store(new Axe(), 26, 20);
            DragWith(SButton.MouseLeft, 24, 20, 26, 21);
            Assert(Control.Board.Jobs.Count == 3 && Control.Board.Jobs.All(t => t.Kind == ActionKind.PlaceFloor && t.Scope == WorkScope.Held), "Wrong floor candidates");
            Until(() => Control.State == "idle");
            Assert(Enumerable.Range(24, 3).All(x => Map.terrainFeatures.GetValueOrDefault(new(x, 21)) is Flooring), "Floor coverage incomplete");
            Assert(stack.Stack == 7 && crop.crop is not null && ReferenceEquals(Map.Objects[new(26, 20)], chest), "Placement replaced existing entity or charged wrong stock");
            Assert(!Map.terrainFeatures.ContainsKey(new(27, 21)), "Floor escaped rectangle");
        });
        Check("floor planning is capped by actual matching stacks and consumes only that material", () =>
        {
            Who.Items[0] = ItemRegistry.Create("(O)328", 2);
            Who.Items[1] = ItemRegistry.Create("(O)328", 2);
            var other = ItemRegistry.Create("(O)329", 5);
            Who.Items[2] = other;
            DragWith(SButton.MouseLeft, 24, 20, 28, 21);
            Assert(Control.Board.Jobs.Count == 4, "Planner invented extra stock");
            Until(() => Control.State == "idle");
            Assert(Map.terrainFeatures.Values.OfType<Flooring>().Count() == 4 && !Who.Items.Any(i => i?.QualifiedItemId == "(O)328") && other.Stack == 5, "Material stock debit or choice incorrect");
        });
        Check("native fences torches sprinklers chests and machines place from held stacks", () =>
        {
            foreach (string id in new[] { "(O)322", "(O)93", "(O)599", "(BC)130", "(BC)13" })
            {
                Reset();
                Config.LeftActions.Clear();
                var stock = ItemRegistry.Create<StardewValley.Object>(id, 2);
                Who.Items[0] = stock;
                Assert(Placement.Supports(stock), "Not recognized: " + id);
                DragWith(SButton.MouseLeft, 24, 20, 25, 20);
                Until(() => Control.State == "idle");
                Assert(Map.Objects.ContainsKey(new(24, 20)) && Map.Objects.ContainsKey(new(25, 20)), "Placement incomplete: " + id);
                Assert(!Who.Items.Any(i => i?.QualifiedItemId == id), "Native debit missing: " + id);
            }
        });
        Check("placement requires the held item and its own switch; right never places", () =>
        {
            var floor = ItemRegistry.Create("(O)328", 8);
            Who.Items[1] = floor;
            Config.LeftActions.UnionWith(Enum.GetValues<ActionKind>());
            Config.Normalize();
            SelectModern(null, new(24, 20, 2, 2));
            Assert(!Control.Board.HasSelection, "Placement inferred from backpack");
            SmartSelect(floor, new(24, 20, 2, 2));
            Assert(!Control.Board.HasSelection, "Right placed held flooring");
            Config.Actions.Remove(ActionKind.PlaceFloor);
            Who.CurrentToolIndex = 1;
            DragWith(SButton.MouseLeft, 24, 20, 25, 21);
            Assert(!Control.Board.HasSelection && floor.Stack == 8, "Disabled floor placed");
        });
        Check("canceled placement does not consume items; bombs stairs and room wallpaper are not fill materials", () =>
        {
            foreach (string id in new[] { "(O)286", "(O)287", "(O)288", "(BC)71", "(O)472", "(O)805" })
                Assert(!Placement.Supports(ItemRegistry.Create<StardewValley.Object>(id)), "Unsafe fill material: " + id);
            var floor = ItemRegistry.Create("(O)328", 4);
            Who.Items[0] = floor;
            DragWith(SButton.MouseLeft, 32, 20, 33, 21);
            Until(() => Who.controller is not null);
            Control.Clear();
            for (int i = 0; i < 60; i++)
                Frame();
            Assert(floor.Stack == 4 && !Map.terrainFeatures.Values.OfType<Flooring>().Any(), "Placement survived cancel");
        });
    }
}
