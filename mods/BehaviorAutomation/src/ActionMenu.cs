using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Sznine.BehaviorAutomation;

public enum ActionPage { Held, Extra, Right, Settings }

/// <summary>Independent action pools and shared settings; layouts keep the same controls.</summary>
public sealed partial class ActionMenu : IClickableMenu
{
    private readonly ModConfig config;
    private readonly Action save;
    private readonly Func<string, string> text;
    private readonly List<ClickableComponent> buttons = new();
    private readonly List<ActionDefinition> visible = new();
    private readonly List<ClickableComponent> tabs = new();
    private ClickableComponent selectAll = null!, clearAll = null!, previous = null!, next = null!;
    private Rectangle content;
    private int columns, rows, offset, maxOffset;
    private string hover = "";
    public ActionPage Page { get; private set; }
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
        this.config = config; this.save = save; this.text = text;
        Layout();
    }
    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = tabs[(int)Page];
        if (Game1.options.SnappyMenus) snapCursorToCurrentSnappedComponent();
    }
    public void SetPage(ActionPage page)
    {
        Page = page; offset = 0; hover = "";
        StopBinding(); Layout();
    }
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (CapturingBinding) return;
        for (int i = 0; i < tabs.Count; i++)
            if (tabs[i].containsPoint(x, y))
            {
                SetPage((ActionPage)i); Game1.playSound("smallSelect"); return;
            }
        if (maxOffset > 0 && (previous.containsPoint(x, y) || next.containsPoint(x, y)))
        {
            receiveScrollWheelAction(previous.containsPoint(x, y) ? 1 : -1); return;
        }
        if (Page == ActionPage.Settings)
        {
            if (!ClickSettings(x, y)) base.receiveLeftClick(x, y, playSound);
            return;
        }
        if (selectAll.containsPoint(x, y) || clearAll.containsPoint(x, y))
        {
            bool select = selectAll.containsPoint(x, y);
            foreach (var action in PageActions)
                if (select) Pool.Add(action.Kind); else Pool.Remove(action.Kind);
            Save(); return;
        }
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].containsPoint(x, y))
            {
                if (!Pool.Remove(visible[i].Kind)) Pool.Add(visible[i].Kind);
                Save(); return;
            }
        base.receiveLeftClick(x, y, playSound);
    }
    private void Save()
    {
        config.Normalize(); save(); Game1.playSound("drumkit6");
    }
    public override void receiveScrollWheelAction(int direction)
    {
        if (CapturingBinding) return;
        offset += direction < 0 ? 1 : -1; Layout();
    }
    public override void receiveKeyPress(Keys key)
    {
        if (CapturingBinding) return;
        if (key == Keys.Tab) { SetPage((ActionPage)(((int)Page + 1) % 4)); return; }
        if (key is Keys.PageDown or Keys.PageUp)
        {
            receiveScrollWheelAction(key == Keys.PageDown ? -1 : 1); return;
        }
        base.receiveKeyPress(key);
    }
    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => Layout();
    public override void performHoverAction(int x, int y)
    {
        hover = "";
        if (Page == ActionPage.Settings) HoverSettings(x, y);
        else for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].containsPoint(x, y)) hover = text("action." + visible[i].Kind);
        base.performHoverAction(x, y);
    }
}
