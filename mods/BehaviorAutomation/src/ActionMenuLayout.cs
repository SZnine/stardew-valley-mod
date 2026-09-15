using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Sznine.BehaviorAutomation;

public sealed partial class ActionMenu
{
    private void Layout()
    {
        width = Math.Min(1000, Game1.uiViewport.Width - 32);
        height = Math.Min(700, Game1.uiViewport.Height - 32);
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;
        initializeUpperRightCloseButton();
        bool sidebar = config.MenuAppearance == MenuStyle.Sidebar;
        int side = Math.Clamp(width / 5, 148, 190);
        tabs.Clear();
        for (int i = 0; i < 4; i++)
        {
            var r = sidebar
                ? new Rectangle(xPositionOnScreen + 24, yPositionOnScreen + 82 + i * 68, side, 54)
                : new Rectangle(xPositionOnScreen + 32 + i * (width - 64) / 4, yPositionOnScreen + 68, (width - 64) / 4 - 8, 48);
            tabs.Add(new(r, ((ActionPage)i).ToString())
            {
                myID = 100 + i,
                leftNeighborID = !sidebar && i > 0 ? 99 + i : -1,
                rightNeighborID = sidebar ? (Page == ActionPage.Settings ? 200 : 0) : i < 3 ? 101 + i : -1,
                upNeighborID = sidebar && i > 0 ? 99 + i : -1,
                downNeighborID = sidebar && i < 3 ? 101 + i : Page == ActionPage.Settings ? 200 : 0
            });
        }
        int top = sidebar && Page == ActionPage.Settings ? 84 : config.MenuAppearance == MenuStyle.List && Page != ActionPage.Settings ? 184 : 140;
        content = new(sidebar ? xPositionOnScreen + side + 40 : xPositionOnScreen + 32,
            yPositionOnScreen + top, sidebar ? width - side - 64 : width - 64, height - top - 40);
        selectAll = new(new(sidebar || config.MenuAppearance == MenuStyle.List ? content.X : xPositionOnScreen + width - 252,
            sidebar || config.MenuAppearance == MenuStyle.List ? content.Y - 52 : yPositionOnScreen + 20, 100, 36), "all") { myID = 104, rightNeighborID = 105, downNeighborID = 0 };
        clearAll = new(new(selectAll.bounds.X + 112, selectAll.bounds.Y, 100, 36), "none") { myID = 105, leftNeighborID = 104, downNeighborID = 0 };
        previous = new(new(content.Right - 80, yPositionOnScreen + height - 34, 32, 26), "previous") { myID = 106, rightNeighborID = 107 };
        next = new(new(content.Right - 38, yPositionOnScreen + height - 34, 32, 26), "next") { myID = 107, leftNeighborID = 106 };
        buttons.Clear(); visible.Clear(); settingButtons.Clear(); settingRows.Clear();
        if (Page == ActionPage.Settings) LayoutSettings(); else LayoutActions();
        allClickableComponents = new(tabs);
        if (Page == ActionPage.Settings) allClickableComponents.AddRange(settingButtons.Select(s => s.Button));
        else { allClickableComponents.AddRange(new[] { selectAll, clearAll }); allClickableComponents.AddRange(buttons); }
        if (maxOffset > 0) allClickableComponents.AddRange(new[] { previous, next });
        if (upperRightCloseButton is not null) allClickableComponents.Add(upperRightCloseButton);
        snapToDefaultClickableComponent();
    }
    private void LayoutActions()
    {
        bool list = config.MenuAppearance == MenuStyle.List;
        columns = list ? Math.Max(1, Math.Min(3, content.Width / 285)) : Math.Max(3, Math.Min(config.MenuAppearance == MenuStyle.Sidebar ? 6 : 7, content.Width / 116));
        int cellWidth = content.Width / columns, cellHeight = list ? 64 : config.MenuAppearance == MenuStyle.Sidebar ? 100 : 112;
        rows = Math.Max(1, content.Height / cellHeight);
        var all = PageActions.ToArray();
        maxOffset = Math.Max(0, (all.Length + columns - 1) / columns - rows);
        offset = Math.Clamp(offset, 0, maxOffset);
        foreach (var action in all.Skip(offset * columns).Take(rows * columns))
        {
            int i = visible.Count, count = Math.Min(rows * columns, all.Length - offset * columns);
            visible.Add(action);
            buttons.Add(new(new(content.X + i % columns * cellWidth, content.Y + i / columns * cellHeight, cellWidth - 8, cellHeight - 8), action.Kind.ToString())
            {
                myID = i, leftNeighborID = i % columns > 0 ? i - 1 : 100 + (int)Page,
                rightNeighborID = i % columns < columns - 1 && i + 1 < count ? i + 1 : -1,
                upNeighborID = i >= columns ? i - columns : 100 + (int)Page,
                downNeighborID = i + columns < count ? i + columns : maxOffset > 0 ? 107 : -1
            });
        }
    }
    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
        Center(b, text("menu.title"), new(xPositionOnScreen + 32, yPositionOnScreen + 22, config.MenuAppearance == MenuStyle.Cards ? width - 310 : width - 64, 32), Game1.textColor);
        if (Page != ActionPage.Settings) { Button(b, selectAll, text("menu.all")); Button(b, clearAll, text("menu.none")); }
        for (int i = 0; i < tabs.Count; i++)
            Button(b, tabs[i], text("menu." + ((ActionPage)i).ToString().ToLowerInvariant()), (int)Page == i ? new Color(190, 255, 180) : new Color(175, 170, 157));
        if (Page == ActionPage.Settings) DrawSettings(b);
        for (int i = 0; i < buttons.Count; i++) DrawAction(b, buttons[i].bounds, visible[i], Pool.Contains(visible[i].Kind));
        if (maxOffset > 0)
        {
            Center(b, $"{offset + 1} / {maxOffset + 1}", new(content.X, yPositionOnScreen + height - 32, content.Width - 95, 24), Game1.textColor);
            Button(b, previous, "^"); Button(b, next, "v");
        }
        base.draw(b);
        if (hover.Length > 0 && !CapturingBinding) drawHoverText(b, hover, Game1.smallFont);
        drawMouse(b);
    }
    private void DrawAction(SpriteBatch b, Rectangle r, ActionDefinition action, bool enabled)
    {
        if (config.MenuAppearance == MenuStyle.Cards)
        {
            drawTextureBox(b, r.X, r.Y, r.Width, r.Height, enabled ? new Color(190, 255, 180) : new Color(167, 162, 148));
            if (enabled)
            {
                b.Draw(Game1.staminaRect, new Rectangle(r.X + 10, r.Y + 7, r.Width - 20, 3), new Color(160, 246, 116));
                b.Draw(Game1.staminaRect, new Rectangle(r.X + 10, r.Bottom - 10, r.Width - 20, 3), new Color(160, 246, 116));
            }
        }
        else
        {
            b.Draw(Game1.staminaRect, r, enabled ? new Color(165, 208, 109) * .55f : new Color(91, 79, 55) * .16f);
            if (enabled) b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - 3, r.Width, 3), new Color(85, 153, 72));
        }
        if (config.MenuAppearance == MenuStyle.List)
        {
            ActionIcons.Draw(b, action, new(r.X + 8, r.Y + 6, 40, 40), enabled ? 1 : .42f);
            Center(b, text("short." + action.Kind), new(r.X + 52, r.Y, r.Width - 90, r.Height), Game1.textColor);
            drawTextureBox(b, r.Right - 36, r.Y + 12, 26, 28, enabled ? new Color(153, 228, 112) : new Color(180, 173, 152));
            if (enabled) b.Draw(Game1.staminaRect, new Rectangle(r.Right - 28, r.Y + 20, 10, 10), new Color(33, 81, 26));
        }
        else
        {
            int size = config.MenuAppearance == MenuStyle.Cards ? 54 : 48;
            ActionIcons.Draw(b, action, new(r.Center.X - size / 2, r.Y + 7, size, size), enabled ? 1 : .42f);
            Center(b, text("short." + action.Kind), new(r.X + 4, r.Bottom - 31, r.Width - 8, 26), enabled ? new Color(24, 60, 22) : new Color(85, 80, 72));
        }
    }
    private static void Button(SpriteBatch b, ClickableComponent button, string label, Color? tint = null)
    {
        var r = button.bounds;
        drawTextureBox(b, r.X, r.Y, r.Width, r.Height, tint ?? Color.White);
        Center(b, label, r, Game1.textColor);
    }
    private static void Center(SpriteBatch b, string value, Rectangle bounds, Color color)
    {
        var size = Game1.smallFont.MeasureString(value);
        float scale = Math.Max(.1f, Math.Min(1, (bounds.Width - 8) / Math.Max(1, size.X)));
        b.DrawString(Game1.smallFont, value, bounds.Center.ToVector2() - size * scale / 2, color, 0, Vector2.Zero, scale, SpriteEffects.None, 1);
    }
}
