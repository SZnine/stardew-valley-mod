using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private static ClickableComponent FindSetting(ActionMenu menu, string key, bool last = false)
    {
        for (int i = 0; i < 12; i++) menu.receiveScrollWheelAction(1);
        for (int i = 0; i < 12; i++)
        {
            var matches = menu.SettingButtons.Where(b => b.name == key).ToArray();
            if (matches.Length > 0) return last ? matches[^1] : matches[0];
            menu.receiveScrollWheelAction(-1);
        }
        throw new Exception("Setting unavailable: " + key);
    }
    private static void ClickSetting(ActionMenu menu, string key)
    {
        var button = FindSetting(menu, key);
        menu.receiveLeftClick(button.bounds.Center.X, button.bounds.Center.Y);
    }
    private void UiSettingsTests()
    {
        Check("UI defaults and live style switches preserve the three action pools", () =>
        {
            var fresh = new ModConfig();
            Assert(fresh.MenuAppearance == MenuStyle.Sidebar && fresh.SelectionAppearance == SelectionStyle.Grid, "Wrong defaults");
            int saves = 0;
            var before = Config.Actions.ToHashSet();
            var menu = new ActionMenu(Config, () => { saves++; Behavior.Helper.WriteConfig(Config); }, k => Behavior.Helper.Translation.Get(k));
            menu.SetPage(ActionPage.Settings);
            ClickSetting(menu, "config.menu-style");
            Assert(Config.MenuAppearance == MenuStyle.List && menu.Page == ActionPage.Settings, "Switch did not rebuild the open page");
            ClickSetting(menu, "config.selection-style");
            var reloaded = Behavior.Helper.ReadConfig<ModConfig>();
            Assert(saves == 2 && reloaded.MenuAppearance == MenuStyle.List && reloaded.SelectionAppearance == SelectionStyle.Filled && before.SetEquals(Config.Actions), "Style switch lost settings or actions");
        });
        Check("in-game key recording saves chords on release and does not trigger menu controls", () =>
        {
            var cursor = CursorAtTile(24, 20);
            Buttons(cursor);
            var menu = new ActionMenu(Config, () => Behavior.Helper.WriteConfig(Config), k => Behavior.Helper.Translation.Get(k));
            Game1.activeClickableMenu = menu; menu.SetPage(ActionPage.Settings);
            foreach (string key in new[] { "config.select", "config.cancel", "config.menu" })
            {
                ClickSetting(menu, key);
                Buttons(cursor, SButton.LeftControl);
                Buttons(cursor, SButton.LeftControl, SButton.K);
                Assert(menu.CapturingBinding && Game1.activeClickableMenu == menu && Helper.Input.IsSuppressed(SButton.K), "Recording leaked input");
                Buttons(cursor);
                Assert(!menu.CapturingBinding, "Binding did not finish on release");
            }
            var read = Behavior.Helper.ReadConfig<ModConfig>();
            Assert(new[] { read.SelectKey, read.CancelKey, read.ActionMenuKey }.All(k => k.ToString().Contains("K") && k.ToString().Contains("LeftControl")), "Chord settings not saved");
            Game1.activeClickableMenu = null;
            Buttons(cursor, SButton.LeftControl, SButton.K); ModTick();
            Assert(Game1.activeClickableMenu is ActionMenu, "New menu chord did not open the panel");
            Buttons(cursor);
        });
        Check("recording the existing panel key stays open and Escape cancels without changing it", () =>
        {
            var cursor = CursorAtTile(24, 20);
            Buttons(cursor);
            var menu = new ActionMenu(Config, () => {}, k => Behavior.Helper.Translation.Get(k));
            Game1.activeClickableMenu = menu; menu.SetPage(ActionPage.Settings);
            ClickSetting(menu, "config.menu");
            Buttons(cursor, SButton.F8);
            Assert(Game1.activeClickableMenu == menu && menu.CapturingBinding, "Existing menu key closed the recorder");
            Buttons(cursor);
            ClickSetting(menu, "config.menu");
            Buttons(cursor, SButton.Escape);
            Assert(Game1.activeClickableMenu == menu && !menu.CapturingBinding && Config.ActionMenuKey.ToString() == "F8", "Escape changed binding or closed panel");
            Buttons(cursor);
            ClickSetting(menu, "config.select");
            Buttons(cursor, SButton.G); Buttons(cursor);
            ClickSetting(menu, "config.select.reset");
            Assert(Config.SelectKey.ToString() == "LeftShift, RightShift", "Binding reset did not restore both Shift keys");
        });
        Check("all selection styles produce different native pixels and preserve capped dimensions", () =>
        {
            var old = Game1.viewport;
            var device = Game1.graphics.GraphicsDevice;
            var targets = device.GetRenderTargets(); var viewport = device.Viewport;
            try
            {
                Game1.viewport = new(0, 0, 1280, 720);
                var drag = new DragSelection { Start = new(3, 3), End = new(10, 7), Mode = ToolMode.Scythe };
                using var renderer = new OverlayRenderer();
                using var batch = new Microsoft.Xna.Framework.Graphics.SpriteBatch(device);
                var images = new List<Color[]>();
                foreach (var style in Enum.GetValues<SelectionStyle>())
                {
                    Config.SelectionAppearance = style;
                    var texture = renderer.Render(batch, Control, drag, Config, drag.End);
                    var colors = new Color[texture.Width * texture.Height]; texture.GetData(colors); images.Add(colors);
                    using var output = File.Create(Path.Combine(Evidence, $"selection-{style}.png")); texture.SaveAsPng(output, texture.Width, texture.Height);
                }
                Assert(images[0].Where((c,i)=>c!=images[1][i]).Count()>1000 && images[1].Where((c,i)=>c!=images[2][i]).Count()>1000, "Style selector did not affect native drawing");
                Assert(Overlay.SizeText(drag) is "8 × 5" or "8 x 5", "Style changed dimensions");
            }
            finally { device.SetRenderTargets(targets); device.Viewport = viewport; Game1.viewport = old; }
        });
    }
}
