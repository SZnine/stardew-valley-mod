using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Sznine.BehaviorAutomation;

public enum ActionPage
{
    Held, Extra, Right
}

/// <summary>Three independent pools: held-item actions, left extras, and right smart tools.</summary>
public sealed class ActionMenu : IClickableMenu
{
    private readonly ModConfig config;
    private readonly Action save;
    private readonly Func<string, string> text;
    private readonly List<ClickableComponent> buttons = new();
    private readonly List<ActionDefinition> visible = new();
    private readonly List<ClickableComponent> tabs = new();
    private ClickableComponent selectAll = null!, clearAll = null!;
    private int columns, rows, offset, tileWidth;
    private string hover = "";
    public ActionPage Page
    {
        get; private set;
    }
    public IReadOnlyList<ClickableComponent> ActionButtons => buttons;
    public IReadOnlyList<ActionDefinition> VisibleActions => visible;
    public IReadOnlyList<ClickableComponent> PageButtons => tabs;
    public ClickableComponent SelectAllButton => selectAll;
    public ClickableComponent ClearAllButton => clearAll;
    private HashSet<ActionKind> Pool => Page switch
    {
        ActionPage.Extra => config.LeftActions,
        ActionPage.Right => config.SmartActions,
        _ => config.Actions
    };
    private IEnumerable<ActionDefinition> PageActions => ActionCatalog.All.Where(a => Page == ActionPage.Held || ActionCatalog.CanSelectSmart(a.Kind));

    public ActionMenu(ModConfig config, Action save, Func<string, string> text) : base(0, 0, 0, 0, true)
    {
        this.config = config;
        this.save = save;
        this.text = text;
        Layout();
    }

    private void Layout()
    {
        width = Math.Min(1000, Game1.uiViewport.Width - 32);
        height = Math.Min(700, Game1.uiViewport.Height - 32);
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;
        initializeUpperRightCloseButton();
        int x = xPositionOnScreen + 32, y = yPositionOnScreen + 68;
        tabs.Clear();
        for (int i = 0; i < 3; i++)
            tabs.Add(new(new(x + i * (width - 64) / 3, y, (width - 64) / 3 - 8, 48), ((ActionPage)i).ToString())
            {
                myID = 100 + i,
                leftNeighborID = i > 0 ? 99 + i : -1,
                rightNeighborID = i < 2 ? 101 + i : -1,
                downNeighborID = 0,
                upNeighborID = 103
            });
        selectAll = new(new(xPositionOnScreen + width - 252, yPositionOnScreen + 20, 100, 36), "all")
        {
            myID = 103,
            rightNeighborID = 104,
            downNeighborID = 102
        };
        clearAll = new(new(xPositionOnScreen + width - 144, yPositionOnScreen + 20, 100, 36), "none")
        {
            myID = 104,
            leftNeighborID = 103,
            downNeighborID = 102
        };
        columns = Math.Max(3, Math.Min(7, (width - 64) / 120));
        tileWidth = (width - 64) / columns;
        rows = Math.Max(1, (height - 176) / 112);
        var all = PageActions.ToArray();
        offset = Math.Clamp(offset, 0, Math.Max(0, (all.Length + columns - 1) / columns - rows));
        buttons.Clear();
        visible.Clear();
        foreach (var action in all.Skip(offset * columns).Take(rows * columns))
        {
            int i = visible.Count;
            visible.Add(action);
            buttons.Add(new(new(x + i % columns * tileWidth, yPositionOnScreen + 140 + i / columns * 112, tileWidth - 6, 104), action.Kind.ToString())
            {
                myID = i,
                leftNeighborID = i % columns > 0 ? i - 1 : -1,
                rightNeighborID = i % columns < columns - 1 && i + 1 < Math.Min(rows * columns, all.Length - offset * columns) ? i + 1 : -1,
                upNeighborID = i >= columns ? i - columns : 100 + (int)Page,
                downNeighborID = i + columns < Math.Min(rows * columns, all.Length - offset * columns) ? i + columns : -1
            });
        }
        allClickableComponents = new(tabs) { selectAll, clearAll };
        allClickableComponents.AddRange(buttons);
        if (upperRightCloseButton is not null)
            allClickableComponents.Add(upperRightCloseButton);
        snapToDefaultClickableComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = tabs[(int)Page];
        if (Game1.options.SnappyMenus)
            snapCursorToCurrentSnappedComponent();
    }

    public void SetPage(ActionPage page)
    {
        Page = page;
        offset = 0;
        hover = "";
        Layout();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        for (int i = 0; i < tabs.Count; i++)
            if (tabs[i].containsPoint(x, y))
            {
                SetPage((ActionPage)i);
                Game1.playSound("smallSelect");
                return;
            }
        if (selectAll.containsPoint(x, y) || clearAll.containsPoint(x, y))
        {
            bool select = selectAll.containsPoint(x, y);
            foreach (var action in PageActions)
                if (!select)
                    Pool.Remove(action.Kind);
                else
                    Pool.Add(action.Kind);
            Save();
            return;
        }
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].containsPoint(x, y))
            {
                var kind = visible[i].Kind;
                if (!Pool.Remove(kind))
                    Pool.Add(kind);
                Save();
                return;
            }
        base.receiveLeftClick(x, y, playSound);
    }

    private void Save()
    {
        config.Normalize();
        save();
        Game1.playSound("drumkit6");
    }
    public override void receiveScrollWheelAction(int direction)
    {
        offset += direction < 0 ? 1 : -1;
        Layout();
    }
    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Tab)
        {
            SetPage((ActionPage)(((int)Page + 1) % 3));
            return;
        }
        if (key == Keys.PageDown || key == Keys.PageUp)
        {
            receiveScrollWheelAction(key == Keys.PageDown ? -1 : 1);
            return;
        }
        base.receiveKeyPress(key);
    }
    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => Layout();
    public override void performHoverAction(int x, int y)
    {
        hover = "";
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].containsPoint(x, y))
                hover = text("action." + visible[i].Kind);
        base.performHoverAction(x, y);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
        Center(b, text("menu.title"), new(xPositionOnScreen + 32, yPositionOnScreen + 22, width - 310, 32), Game1.textColor);
        Button(selectAll, "menu.all");
        Button(clearAll, "menu.none");
        for (int i = 0; i < tabs.Count; i++)
        {
            var r = tabs[i].bounds;
            bool active = (int)Page == i;
            drawTextureBox(b, r.X, r.Y, r.Width, r.Height, active ? Color.White : new Color(175, 170, 157));
            if (active)
                b.Draw(Game1.staminaRect, new Rectangle(r.X + 12, r.Bottom - 8, r.Width - 24, 3), new Color(75, 162, 80));
            Center(b, text("menu." + ((ActionPage)i).ToString().ToLowerInvariant()), r, Game1.textColor);
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            var r = buttons[i].bounds;
            var action = visible[i];
            bool enabled = Pool.Contains(action.Kind);
            drawTextureBox(b, r.X, r.Y, r.Width, r.Height, enabled ? new Color(190, 255, 180) : new Color(167, 162, 148));
            if (enabled)
            {
                var glow = new Color(160, 246, 116);
                b.Draw(Game1.staminaRect, new Rectangle(r.X + 10, r.Y + 7, r.Width - 20, 3), glow);
                b.Draw(Game1.staminaRect, new Rectangle(r.X + 10, r.Bottom - 10, r.Width - 20, 3), glow);
            }
            ActionIcons.Draw(b, action, new(r.Center.X - 27, r.Y + 9, 54, 54), enabled ? 1f : .42f);
            Center(b, text("short." + action.Kind), new(r.X + 6, r.Y + 72, r.Width - 12, 22), enabled ? new Color(24, 60, 22) : new Color(85, 80, 72));
        }
        int count = PageActions.Count();
        if ((count + columns - 1) / columns > rows)
            Center(b, $"{offset + 1} / {Math.Max(1, (count + columns - 1) / columns - rows + 1)}", new(xPositionOnScreen, yPositionOnScreen + height - 30, width, 24), Game1.textColor);
        base.draw(b);
        if (hover.Length > 0)
            drawHoverText(b, hover, Game1.smallFont);
        drawMouse(b);

        void Button(ClickableComponent component, string key)
        {
            var r = component.bounds;
            drawTextureBox(b, r.X, r.Y, r.Width, r.Height, Color.White);
            Center(b, text(key), r, Game1.textColor);
        }
    }

    private static void Center(SpriteBatch b, string value, Rectangle bounds, Color color)
    {
        var size = Game1.smallFont.MeasureString(value);
        float scale = Math.Min(1, (bounds.Width - 8) / Math.Max(1, size.X));
        b.DrawString(Game1.smallFont, value, bounds.Center.ToVector2() - size * scale / 2, color, 0, Vector2.Zero, scale, SpriteEffects.None, 1);
    }
}
