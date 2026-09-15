using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Sznine.BehaviorAutomation;

public sealed partial class ActionMenu
{
    private enum SettingKind { Toggle, Number, Choice, Binding }
    private sealed record Setting(string Key, Func<string> Value, Action<int> Change, SettingKind Kind, string? Hint = null);
    private readonly List<(ClickableComponent Button, int Row, int Delta)> settingButtons = new();
    private Setting[] settings = Array.Empty<Setting>();
    private readonly List<(Rectangle Bounds, int Index)> settingRows = new();
    public IReadOnlyList<ClickableComponent> SettingButtons => settingButtons.Select(b => b.Button).ToArray();

    private void LayoutSettings()
    {
        string Enabled(bool v) => text(v ? "menu.on" : "menu.off");
        Setting Key(string key) => new(key, () => BindingValue(key).ToString(), _ => BeginBinding(key), SettingKind.Binding, "menu.bind-tip");
        settings = new[]
        {
            new Setting("config.menu-style", () => text("style.menu." + config.MenuAppearance), _ => config.MenuAppearance = (MenuStyle)(((int)config.MenuAppearance + 1) % 3), SettingKind.Choice),
            new Setting("config.selection-style", () => text("style.selection." + config.SelectionAppearance), _ => config.SelectionAppearance = (SelectionStyle)(((int)config.SelectionAppearance + 1) % 3), SettingKind.Choice),
            new Setting("config.selection-size", () => Enabled(config.ShowSelectionSize), _ => config.ShowSelectionSize = !config.ShowSelectionSize, SettingKind.Toggle),
            Key("config.select"), Key("config.menu"), Key("config.cancel"),
            new Setting("config.pan", () => Enabled(config.PanWhileSelecting), _ => config.PanWhileSelecting = !config.PanWhileSelecting, SettingKind.Toggle),
            new Setting("config.pan-speed", () => config.SelectionPanSpeed.ToString(), d => config.SelectionPanSpeed += d, SettingKind.Number),
            new Setting("config.interiors", () => Enabled(config.WorkInsideBuildings), _ => config.WorkInsideBuildings = !config.WorkInsideBuildings, SettingKind.Toggle, "config.interiors-tip"),
            new Setting("config.stored-tools", () => Enabled(config.UseStoredTools), _ => config.UseStoredTools = !config.UseStoredTools, SettingKind.Toggle),
            new Setting("config.wild-spacing", () => config.WildTreeSpacing.ToString(), d => config.WildTreeSpacing += d, SettingKind.Number, "config.spacing-tip"),
            new Setting("config.fruit-spacing", () => config.FruitTreeSpacing.ToString(), d => config.FruitTreeSpacing += d, SettingKind.Number, "config.spacing-tip")
        };
        const int rowHeight = 58;
        rows = Math.Max(1, content.Height / rowHeight);
        maxOffset = Math.Max(0, settings.Length - rows);
        offset = Math.Clamp(offset, 0, maxOffset);
        for (int i = offset; i < Math.Min(settings.Length, offset + rows); i++)
        {
            var r = new Rectangle(content.X + 4, content.Y + (i - offset) * rowHeight, content.Width - 8, rowHeight - 6);
            settingRows.Add((r, i));
            void Add(int x, int w, int delta, bool reset = false)
            {
                var button = new ClickableComponent(new(x, r.Center.Y - 20, w, 40), settings[i].Key + (reset ? ".reset" : ""))
                {
                    myID = 200 + settingButtons.Count,
                    leftNeighborID = 103,
                    upNeighborID = i == offset ? 103 : 200 + Math.Max(0, settingButtons.Count - 1)
                };
                settingButtons.Add((button, i, delta));
            }
            if (settings[i].Kind == SettingKind.Number) { Add(r.Right - 152, 40, -1); Add(r.Right - 48, 40, 1); }
            else if (settings[i].Kind == SettingKind.Binding) { Add(r.Right - 294, 226, 0); Add(r.Right - 60, 52, 2, true); }
            else Add(r.Right - 212, 204, 0);
        }
        for (int i = 0; i < settingButtons.Count; i++)
        {
            var button = settingButtons[i].Button;
            button.downNeighborID = i + 1 < settingButtons.Count ? 201 + i : maxOffset > 0 ? 107 : -1;
            if (i + 1 < settingButtons.Count && settingButtons[i + 1].Row == settingButtons[i].Row) button.rightNeighborID = 201 + i;
        }
    }
    private bool ClickSettings(int x, int y)
    {
        foreach (var control in settingButtons)
            if (control.Button.containsPoint(x, y))
            {
                var setting = settings[control.Row];
                if (setting.Kind == SettingKind.Binding)
                {
                    if (control.Delta == 2) { SetBinding(setting.Key, DefaultBinding(setting.Key)); Save(); }
                    else setting.Change(0);
                }
                else { setting.Change(control.Delta); Save(); Layout(); }
                return true;
            }
        return false;
    }
    private void HoverSettings(int x, int y)
    {
        foreach (var (bounds, index) in settingRows)
            if (bounds.Contains(x, y) && settings[index].Hint is { } hint) hover = text(hint);
    }
    private void DrawSettings(SpriteBatch b)
    {
        foreach (var (r, index) in settingRows)
        {
            var setting = settings[index];
            int controlsWidth = setting.Kind == SettingKind.Binding ? 310 : setting.Kind == SettingKind.Number ? 170 : 228;
            Center(b, text(setting.Key), new(r.X, r.Y, r.Width - controlsWidth, r.Height), Game1.textColor);
            if (setting.Kind == SettingKind.Number) Center(b, setting.Value(), new(r.Right - 108, r.Y, 60, r.Height), Game1.textColor);
        }
        foreach (var control in settingButtons)
        {
            var setting = settings[control.Row];
            bool listening = bindingKey == setting.Key && control.Delta == 0;
            bool enabled = setting.Kind == SettingKind.Toggle && setting.Value() == text("menu.on");
            string label = listening ? text("menu.bind-listening") : setting.Kind == SettingKind.Binding && control.Delta == 2 ? text("menu.reset")
                : setting.Kind == SettingKind.Number ? control.Delta > 0 ? "+" : "-" : setting.Value();
            Button(b, control.Button, label, listening ? new Color(255, 240, 133) : enabled ? new Color(190, 255, 180) : Color.White);
        }
    }
}
