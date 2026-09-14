using System.Collections;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void MenuTests()
    {
        Check("configured panel key opens the actual menu and closes it again", () =>
        {
            var cursor = CursorAtTile(24, 20);
            Buttons(cursor, SButton.F8);
            ModTick();
            Assert(Game1.activeClickableMenu is ActionMenu && Helper.Input.IsSuppressed(SButton.F8), "F8 did not open action panel");
            Buttons(cursor);
            Buttons(cursor, SButton.F8);
            Assert(Game1.activeClickableMenu is null, "F8 did not close action panel");
        });
        Check("panel key cancels a native swing and opens after the animation releases control", () =>
        {
            var tree = new StardewValley.TerrainFeatures.Tree("1", 5);
            Map.terrainFeatures[new(24, 20)] = tree;
            Select(new Axe(), 24, 20);
            Until(() => Control.Active is not null && Who.UsingTool);
            Buttons(CursorAtTile(24, 20), SButton.F8);
            Buttons(CursorAtTile(24, 20));
            for (int i = 0; i < 200 && Game1.activeClickableMenu is null; i++)
            {
                Frame();
                ModTick();
            }
            Assert(Game1.activeClickableMenu is ActionMenu && !Control.Board.HasSelection && Who.CanMove && !Who.UsingTool, "Panel request lost during native animation");
            Assert(tree.health.Value == 10, "Canceled swing impacted after the menu request");
            Game1.activeClickableMenu = null;
        });
        Check("action catalog covers held kinds and keeps hoe planting and placement out of automatic pools", () =>
        {
            Assert(ActionCatalog.All.Select(a => a.Kind).Distinct().Count() == Enum.GetValues<ActionKind>().Length, "Missing action icon");
            Assert(ActionCatalog.SmartKinds.Count() == 22 && !ActionCatalog.CanSelectSmart(ActionKind.Till) && !ActionCatalog.CanSelectSmart(ActionKind.PlantCrop), "Unsafe smart pool catalog");
        });
        CheckWithMod("spacechase0.GenericModConfigMenu", "GMCM has only six settings and setters persist", () =>
        {
            var gmcm = Installed<object>("spacechase0.GenericModConfigMenu");
            var manager = AccessTools.Field(gmcm.GetType(), "ConfigManager").GetValue(gmcm)!;
            var setup = AccessTools.Method(manager.GetType(), "Get").Invoke(manager, new object[] { Behavior.ModManifest, false })!;
            var options = ((IEnumerable)AccessTools.Method(setup.GetType(), "GetAllOptions").Invoke(setup, null)!).Cast<object>().ToArray();
            Assert(options.Length == 6, "Legacy configuration remains: " + options.Length);
            foreach (var option in options)
            {
                var value = AccessTools.Property(option.GetType(), "Value");
                value.SetValue(option, value.PropertyType == typeof(bool) ? false : value.PropertyType == typeof(KeybindList) ? KeybindList.Parse("K") : 21f);
                AccessTools.Method(option.GetType(), "BeforeSave").Invoke(option, null);
            }
            Config.LeftActions = new() { ActionKind.Water };
            Behavior.Helper.WriteConfig(Config);
            var read = Behavior.Helper.ReadConfig<ModConfig>();
            Assert(!read.Enabled && !read.UseStoredTools && read.ReserveStamina == 21 && read.ActionMenuKey.ToString() == "K" && read.LeftActions.SetEquals(new[] { ActionKind.Water }), "Config roundtrip failed");
        });
        Check("old global exclusions survive config migration", () =>
        {
            var legacy = new ModConfig { ConfigVersion = 3, ReserveStamina = 27, UseStoredTools = false, SelectKey = KeybindList.Parse("LeftControl") };
            legacy.Actions.Remove(ActionKind.WildTree);
            legacy.Migrate();
            Assert(legacy.ConfigVersion == 7 && !legacy.Allows(ActionKind.WildTree) && !legacy.Allows(ActionKind.WildTree, WorkScope.Smart) && legacy.ReserveStamina == 27 && !legacy.UseStoredTools && legacy.SelectKey.ToString() == "LeftControl", "Migration changed user choices");
        });
        Check("native action menu switches all three pools and saves immediately", () =>
        {
            var old = Game1.uiViewport;
            Game1.uiViewport = new(0, 0, 1280, 720);
            int saves = 0;
            try
            {
                var menu = new ActionMenu(Config, () => saves++, k => Behavior.Helper.Translation.Get(k));
                Assert(menu.Page == ActionPage.Held, "First page is not held actions");
                menu.SetPage(ActionPage.Held);
                int index = menu.VisibleActions.ToList().FindIndex(a => a.Kind == ActionKind.Stone);
                var button = menu.ActionButtons[index].bounds.Center;
                menu.receiveLeftClick(button.X, button.Y);
                Assert(!Config.Actions.Contains(ActionKind.Stone) && saves == 1, "Global click not applied immediately");
                menu.SetPage(ActionPage.Right);
                index = menu.VisibleActions.ToList().FindIndex(a => a.Kind == ActionKind.Stone);
                button = menu.ActionButtons[index].bounds.Center;
                menu.receiveLeftClick(button.X, button.Y);
                Assert(saves == 2 && !Config.Allows(ActionKind.Stone, WorkScope.Smart), "Right setting not saved independently");
                index = menu.VisibleActions.ToList().FindIndex(a => a.Kind == ActionKind.Milk);
                button = menu.ActionButtons[index].bounds.Center;
                menu.receiveLeftClick(button.X, button.Y);
                Assert(!Config.SmartActions.Contains(ActionKind.Milk) && Config.Actions.Contains(ActionKind.Milk) && saves == 3, "Right click changed left pool");
                Assert(menu.VisibleActions.All(a => ActionCatalog.CanSelectSmart(a.Kind)), "Manual-only actions on right page");
                menu.SetPage(ActionPage.Extra);
                index = menu.VisibleActions.ToList().FindIndex(a => a.Kind == ActionKind.Water);
                button = menu.ActionButtons[index].bounds.Center;
                menu.receiveLeftClick(button.X, button.Y);
                Assert(Config.LeftActions.Contains(ActionKind.Water) && saves == 4, "Left whitelist not saved");
                menu.receiveKeyPress(Microsoft.Xna.Framework.Input.Keys.Tab);
                Assert(menu.Page == ActionPage.Right, "Tab did not advance to right");
            }
            finally { Game1.uiViewport = old; }
        });
        Check("native menu renders at standard and compact viewport sizes", () =>
        {
            var old = Game1.uiViewport;
            try
            {
                foreach (var size in new[] { new Point(1280, 720), new Point(800, 600) })
                {
                    Game1.uiViewport = new(0, 0, size.X, size.Y);
                    var menu = new ActionMenu(Config, () => { }, k => Behavior.Helper.Translation.Get(k));
                    Assert(menu.ActionButtons.All(b => b.bounds.Left >= 0 && b.bounds.Right <= size.X && b.bounds.Bottom < size.Y - 15), "Action tile outside viewport");
                    RenderMenu(menu, $"actions-held-{size.X}.png", size.X, size.Y);
                    menu.SetPage(ActionPage.Right);
                    RenderMenu(menu, $"actions-right-{size.X}.png", size.X, size.Y);
                    menu.SetPage(ActionPage.Extra);
                    RenderMenu(menu, $"actions-extra-{size.X}.png", size.X, size.Y);
                    menu.receiveScrollWheelAction(-120);
                    Assert(menu.ActionButtons.Count > 0, "Compact scrolling lost actions");
                }
            }
            finally { Game1.uiViewport = old; }
        });
        Check("modifier selection and next modifier press cancel without consuming movement", () =>
        {
            Who.Items[0] = new WateringCan { WaterLeft = 20 };
            CropAt(28, 20);
            DragWith(SButton.MouseLeft, 28, 20, 28, 20);
            Assert(Control.Board.HasSelection, "Selection lost on release");
            Buttons(CursorAtTile(28, 20), SButton.LeftShift);
            Assert(!Control.Board.HasSelection && Who.controller is null, "Cancel did not clear region");
        });
        Check("held movement yields immediately and cancels only after two continuous seconds", () =>
        {
            CropAt(31, 20);
            SelectModern(new WateringCan { WaterLeft = 30 }, new(31, 20, 1, 1));
            Until(() => Who.controller is not null);
            Control.ObserveMovement(Who, true, 16);
            Assert(Who.controller is null && Control.Board.HasSelection, "Movement could not take over");
            for (int i = 0; i < 60; i++)
                Control.ObserveMovement(Who, true, 16);
            Control.ObserveMovement(Who, false, 16);
            Assert(Control.Board.HasSelection, "Short movement canceled work");
            for (int i = 0; i < 126; i++)
                Control.ObserveMovement(Who, true, 16);
            Assert(!Control.Board.HasSelection, "Two-second movement did not cancel");
        });
        Check("opening inventory cancels work and old F9 shortcut has no action", () =>
        {
            CropAt(31, 20);
            SelectModern(new WateringCan { WaterLeft = 30 }, new(31, 20, 1, 1));
            Buttons(CursorAtTile(31, 20), SButton.F9);
            Assert(!Control.Paused && !Helper.Input.IsSuppressed(SButton.F9), "Removed F9 still handled");
            var cursor = CursorAtTile(31, 20);
            var key = Game1.options.menuButton[0].ToSButton();
            SetButtons(cursor, key);
            RaiseInput("ButtonPressed", InputArgs(typeof(ButtonPressedEventArgs), key, cursor, Game1.input));
            Assert(!Control.Board.HasSelection && !Helper.Input.IsSuppressed(key), "Inventory input captured");
        });
    }
    private void RenderMenu(ActionMenu menu, string name, int width, int height)
    {
        var device = Game1.graphics.GraphicsDevice;
        var previous = device.GetRenderTargets();
        var viewport = device.Viewport;
        using var target = new RenderTarget2D(device, width, height);
        using var batch = new SpriteBatch(device);
        try
        {
            device.SetRenderTarget(target);
            device.Clear(new Color(103, 146, 76));
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            menu.draw(batch);
            batch.End();
            device.SetRenderTargets(previous);
            using var file = File.Create(Path.Combine(Evidence, name));
            target.SaveAsPng(file, width, height);
        }
        finally { device.SetRenderTargets(previous); device.Viewport = viewport; }
    }
}
