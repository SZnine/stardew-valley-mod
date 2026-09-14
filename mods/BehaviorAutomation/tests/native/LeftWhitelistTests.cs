using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private void LeftWhitelistTests()
    {
        Check("left whitelist performs extra mining and watering with held food and empty right pool", () =>
        {
            var food = ItemRegistry.Create("(O)194", 3);
            Who.Items[0] = food;
            var can = new WateringCan { WaterLeft = 30 };
            var chest = Store(can);
            chest.Items.Add(new Pickaxe());
            Config.LeftActions = new() { ActionKind.Stone, ActionKind.Water };
            Config.SmartActions.Clear();
            var crop = CropAt(25, 20);
            var stone = ObjectAt("(O)343", 24, 20);
            stone.MinutesUntilReady = 1;
            DragWith(SButton.MouseLeft, 24, 20, 25, 20);
            Assert(Control.Board.Jobs.Count == 2, "Left whitelist did not add both tools");
            Until(() => Control.State == "idle");
            Assert(crop.state.Value == 1 && !Map.Objects.ContainsKey(new(24, 20)), "Extra work incomplete");
            Assert(food.Stack == 3 && ReferenceEquals(Who.CurrentItem, food) && can.WaterLeft == 29 && chest.Items.Contains(can), "Held food or original tool stock changed incorrectly");
        });
        Check("left whitelist can disable all implicit hand and animal work without disabling held tools", () =>
        {
            Config.LeftActions.Clear();
            Who.Items[1] = new MilkPail();
            var cow = CareAnimal("White Cow", 99901, 26, 20, "184");
            var forage = ObjectAt("(O)16", 25, 20);
            forage.isSpawnedObject.Value = true;
            var stone = ObjectAt("(O)343", 24, 20);
            stone.MinutesUntilReady = 1;
            SelectModern(new Pickaxe(), new(23, 19, 6, 4));
            Assert(Control.Board.Jobs.Count == 1 && Control.Board.Jobs[0].Kind == ActionKind.Stone, "Unchecked extra actions selected");
            Until(() => Control.State == "idle");
            Assert(!cow.wasPet.Value && cow.currentProduce.Value == "184" && Map.Objects.ContainsKey(new(25, 20)), "Unchecked extra actions ran");
            Config.Actions.Remove(ActionKind.Forage);
            SelectModern(null, new(25, 20, 1, 1));
            Assert(!Control.Board.HasSelection, "Empty hand bypassed whitelist");
        });
        Check("held exclusion leaves left extras independent; extras never infer hoe or planting", () =>
        {
            Config.LeftActions.UnionWith(Enum.GetValues<ActionKind>());
            Config.Normalize();
            Assert(!Config.LeftActions.Contains(ActionKind.Till) && !Config.LeftActions.Contains(ActionKind.PlantCrop), "Manual actions admitted to whitelist");
            var stone = ObjectAt("(O)343", 24, 20);
            stone.MinutesUntilReady = 1;
            Config.Actions.Remove(ActionKind.Stone);
            SelectModern(new Pickaxe(), new(24, 20, 1, 1));
            Assert(Control.Board.Jobs.Count == 1 && Control.Board.Jobs[0].Scope == WorkScope.LeftExtra, "Held page disabled left extras");
            Config.LeftActions.Remove(ActionKind.Stone);
            SelectModern(new Pickaxe(), new(24, 20, 1, 1));
            Assert(!Control.Board.HasSelection, "Both disabled yet work selected");
        });
        Check("left whitelist refresh respects excluded stump after felling and obstacle tool boundaries", () =>
        {
            var axe = new Axe { UpgradeLevel = 4 };
            Who.Items[1] = axe;
            Config.LeftActions = new() { ActionKind.WildTree };
            var tree = new StardewValley.TerrainFeatures.Tree("1", 5);
            Map.terrainFeatures[new(24, 20)] = tree;
            SelectModern(null, new(24, 20, 1, 1));
            Until(() => Control.State == "idle");
            Assert(tree.stump.Value && tree.health.Value > -99 && Map.terrainFeatures.ContainsKey(new(24, 20)), "Follow-up stump bypassed whitelist");
            SelectModern(new Pickaxe(), new(24, 20, 1, 1));
            Config.LeftActions.Clear();
            Assert(!Control.Board.AllowsObstacle(new WorkTarget { Mode = ToolMode.Axe, Kind = ActionKind.Twig }, Config), "Other obstacle tool bypassed left scope");
        });
        Check("all and clear apply to the whole page including hidden cards without changing other pools", () =>
        {
            var old = Game1.uiViewport;
            Game1.uiViewport = new(0, 0, 800, 600);
            try
            {
                int saves = 0;
                var menu = new ActionMenu(Config, () => saves++, k => Behavior.Helper.Translation.Get(k));
                menu.SetPage(ActionPage.Extra);
                var global = Config.Actions.ToHashSet();
                var right = Config.SmartActions.ToHashSet();
                void Click(StardewValley.Menus.ClickableComponent b) => menu.receiveLeftClick(b.bounds.Center.X, b.bounds.Center.Y);
                Click(menu.ClearAllButton);
                Assert(Config.LeftActions.Count == 0, "Clear left failed");
                Click(menu.SelectAllButton);
                Assert(Config.LeftActions.SetEquals(ActionCatalog.SmartKinds), "All left missed hidden cards or enabled global exclusions");
                Assert(Config.Actions.SetEquals(global) && Config.SmartActions.SetEquals(right), "Bulk changed another pool");
                menu.SetPage(ActionPage.Right);
                Click(menu.ClearAllButton);
                Assert(Config.SmartActions.Count == 0 && Config.LeftActions.Count > 0, "Right clear touched left");
                Click(menu.SelectAllButton);
                Assert(Config.SmartActions.SetEquals(Config.LeftActions), "Right all scope mismatch");
                menu.SetPage(ActionPage.Held);
                Click(menu.ClearAllButton);
                Assert(Config.Actions.Count == 0 && Config.LeftActions.Count > 0, "Global clear erased saved whitelist");
                Click(menu.SelectAllButton);
                Assert(Config.Actions.Count == ActionCatalog.All.Length && saves == 6, "All global omitted manual actions");
            }
            finally { Game1.uiViewport = old; }
        });
        Check("top overlay restores render targets and draws tile preview above the scene", () =>
        {
            var device = Game1.graphics.GraphicsDevice;
            var previous = device.GetRenderTargets();
            var viewport = device.Viewport;
            using var canvas = new RenderTarget2D(device, Game1.viewport.Width, Game1.viewport.Height);
            using var batch = new SpriteBatch(device);
            using var overlay = new OverlayRenderer();
            try
            {
                Control.Editing = true;
                device.SetRenderTarget(canvas);
                device.Clear(Color.Black);
                batch.Begin();
                overlay.DrawTop(batch, Control, new DragSelection { Start = new(24, 20), End = new(26, 21), Mode = ToolMode.Axe }, Config, new(26, 21));
                batch.End();
                Assert(ReferenceEquals(device.GetRenderTargets()[0].RenderTarget, canvas), "Overlay lost caller render target");
                device.SetRenderTargets(previous);
                var pixels = new Color[canvas.Width * canvas.Height];
                canvas.GetData(pixels);
                Assert(pixels.Any(p => p.R > 200 && p.G > 200 && p.B > 200), "Preview was not composited above scene");
            }
            finally { device.SetRenderTargets(previous); device.Viewport = viewport; }
        });
    }
}
