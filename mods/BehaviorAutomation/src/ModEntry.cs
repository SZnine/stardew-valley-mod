using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace Sznine.BehaviorAutomation;

public sealed class DragSelection
{
    public Cell Start, End;
    public ToolMode Mode;
    public Tool? Tool;
    public StardewValley.Object? Material;
    public SButton Button;
    public bool Smart;
}

public sealed class ModEntry : Mod
{
    private ModConfig config = new();
    private readonly PerScreen<WorkController> controllers;
    private readonly PerScreen<DragSelection?> drag = new(() => null);
    private readonly PerScreen<string?> pendingMenu = new(() => null);
    private readonly PerScreen<string?> lastNotice = new(() => null);
    private readonly PerScreen<double> noticeTime = new(() => 0);
    private readonly PerScreen<OverlayRenderer> overlays = new(() => new());
    private Harmony harmony = null!;
    private bool drawErrorLogged;
    public WorkController Controller => controllers.Value;
    public ModEntry()
    {
        controllers = new(() => new(() => config, Notice));
    }

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<ModConfig>();
        int version = config.ConfigVersion;
        config.Migrate();
        if (version < config.ConfigVersion)
            helper.WriteConfig(config);
        harmony = new(ModManifest.UniqueID);
        NativeHooks.Install(harmony, () => Controller);
        helper.Events.GameLoop.GameLaunched += Launched;
        helper.Events.GameLoop.UpdateTicking += Tick;
        helper.Events.Input.ButtonsChanged += Buttons;
        helper.Events.Input.ButtonPressed += Pressed;
        helper.Events.Display.MenuChanged += (_, e) => { if (e.NewMenu is not null) Reset(); };
        helper.Events.Player.Warped += (_, e) => { if (e.IsLocalPlayer) Reset(); };
        helper.Events.GameLoop.Saving += (_, _) => Reset();
        helper.Events.GameLoop.DayEnding += (_, _) => Reset();
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            foreach (var c in controllers.GetActiveValues())
                c.Value.Abandon();
            foreach (var overlay in overlays.GetActiveValues())
                overlay.Value.Dispose();
            drag.ResetAllScreens();
            pendingMenu.ResetAllScreens();
        };
        helper.Events.Display.Rendered += DrawOverlay;
    }

    private void Launched(object? sender, GameLaunchedEventArgs e)
    {
        NativeHooks.Compatibility(harmony, Helper.ModRegistry, Monitor);
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
            return;
        api.Register(ModManifest, () => { Reset(); config = new(); config.Migrate(); }, SaveConfig);
        api.AddBoolOption(ModManifest, () => config.Enabled, v => config.Enabled = v, () => T("config.enabled"));
        api.AddKeybindList(ModManifest, () => config.SelectKey, v => config.SelectKey = v, () => T("config.select"));
        api.AddKeybindList(ModManifest, () => config.ActionMenuKey, v => config.ActionMenuKey = v, () => T("config.menu"));
        api.AddKeybindList(ModManifest, () => config.CancelKey, v => config.CancelKey = v, () => T("config.cancel"));
        api.AddBoolOption(ModManifest, () => config.UseStoredTools, v => config.UseStoredTools = v, () => T("config.stored-tools"), () => T("config.stored-tools-tip"));
        api.AddNumberOption(ModManifest, () => config.ReserveStamina, v => config.ReserveStamina = v, () => T("config.stamina"), min: 0, max: 100, interval: 1);
        api.AddNumberOption(ModManifest, () => config.MovementCancelSeconds, v => config.MovementCancelSeconds = v, () => T("config.move-cancel"), min: .1f, max: 2, interval: .1f);
        api.AddBoolOption(ModManifest, () => config.AllowDiagonalMovement, v => config.AllowDiagonalMovement = v, () => T("config.diagonal"));
        api.AddNumberOption(ModManifest, () => config.ScytheSearchTiles, v => config.ScytheSearchTiles = (int)v, () => T("config.scythe-distance"), () => T("config.scythe-distance-tip"), min: 2, max: 24, interval: 1);
        api.AddNumberOption(ModManifest, () => config.ScytheSwingCost, v => config.ScytheSwingCost = (int)v, () => T("config.scythe-batch"), () => T("config.scythe-batch-tip"), min: 4, max: 32, interval: 1);
        api.AddBoolOption(ModManifest, () => config.AutoRefillWateringCan, v => config.AutoRefillWateringCan = v, () => T("config.refill"));
        api.AddBoolOption(ModManifest, () => config.ClearObstacles, v => config.ClearObstacles = v, () => T("config.clearance"));
        api.AddNumberOption(ModManifest, () => config.RefreshIntervalSeconds, v => config.RefreshIntervalSeconds = v, () => T("config.refresh"), min: .1f, max: 1, interval: .1f);
        api.AddNumberOption(ModManifest, () => config.CompletionDelaySeconds, v => config.CompletionDelaySeconds = v, () => T("config.completion"), () => T("config.completion-tip"), min: .3f, max: 3, interval: .1f);
    }

    private void SaveConfig()
    {
        Reset();
        config.Normalize();
        Helper.WriteConfig(config);
    }
    private string T(string key) => Helper.Translation.Get(key);
    private void Notice(string key)
    {
        double now = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;
        if (lastNotice.Value == key && now - noticeTime.Value < 3000)
            return;
        lastNotice.Value = key;
        noticeTime.Value = now;
        if (Context.IsWorldReady)
            Game1.addHUDMessage(new HUDMessage(T("notice." + key), 2));
    }
    private void Reset()
    {
        pendingMenu.Value = null;
        drag.Value = null;
        Controller.Editing = false;
        Controller.Clear();
    }
    public static bool IsInventory(IClickableMenu? menu) => menu is InventoryPage || menu is GameMenu g && g.currentTab == GameMenu.inventoryTab;
    public static bool Editable() => Context.IsWorldReady && Game1.activeClickableMenu is null && !Game1.eventUp && !Game1.dialogueUp
        && Game1.currentMinigame is null && Game1.locationRequest is null && !Game1.isWarping && !Game1.paused && !Game1.HostPaused
        && !Game1.IsChatting && Game1.textEntry is null && !Game1.freezeControls && !Game1.globalFade && !Game1.fadeToBlack && Game1.farmEvent is null;
    public static bool Eligible() => Editable() && Game1.player.freezePause <= 0 && !Game1.player.isEating && !Game1.player.isEmoteAnimating;
    private bool MovingInput() => Game1.options.moveUpButton.Concat(Game1.options.moveRightButton).Concat(Game1.options.moveDownButton).Concat(Game1.options.moveLeftButton)
        .Any(b => Helper.Input.IsDown(b.ToSButton())) || Game1.input.GetGamePadState().ThumbSticks.Left.LengthSquared() > .04f;

    private void Buttons(object? sender, ButtonsChangedEventArgs e)
    {
        if (Game1.activeClickableMenu is ActionMenu && config.ActionMenuKey.JustPressed())
        {
            Game1.exitActiveMenu();
            Helper.Input.SuppressActiveKeybinds(config.ActionMenuKey);
            return;
        }
        if (!Editable())
            return;
        if (config.ActionMenuKey.JustPressed())
        {
            Reset();
            pendingMenu.Value = "actions";
            Helper.Input.SuppressActiveKeybinds(config.ActionMenuKey);
            return;
        }
        if (!config.Enabled)
            return;
        Controller.Editing = config.SelectKey.IsDown();
        if (config.CancelKey.JustPressed() && Controller.HasSession)
        {
            Reset();
            return;
        }
        Controller.ObserveMovement(Game1.player, MovingInput(), 0);
    }

    [EventPriority(EventPriority.Low)]
    private void Pressed(object? sender, ButtonPressedEventArgs e)
    {
        if (Editable() && (Controller.HasSession || drag.Value is not null)
            && (Game1.options.menuButton.Any(b => b.ToSButton() == e.Button)
                || Game1.options.gamepadControls && e.Button is SButton.ControllerStart or SButton.ControllerB))
        {
            bool defer = Controller.Active is not null || !Game1.CanShowPauseMenu();
            Reset();
            if (defer)
                pendingMenu.Value = "inventory";
            return;
        }
        if (!config.Enabled || !Editable() || e.Button is not (SButton.MouseLeft or SButton.MouseRight)
            || !config.SelectKey.IsDown() || Helper.Input.IsSuppressed(e.Button) || Game1.player.mount is not null)
            return;
        var ui = e.Cursor.GetScaledScreenPixels();
        if (Game1.onScreenMenus.Any(m => m.isWithinBounds((int)ui.X, (int)ui.Y)))
            return;
        bool smart = e.Button == SButton.MouseRight;
        var mode = ModeInfo.From(Game1.player.CurrentItem) ?? ToolMode.Hand;
        Cell point = Cell.At(e.Cursor.Tile);
        drag.Value = new()
        {
            Start = point,
            End = point,
            Mode = mode,
            Tool = Game1.player.CurrentTool,
            Material = smart ? null : Game1.player.CurrentItem?.getOne() as StardewValley.Object,
            Button = e.Button,
            Smart = smart
        };
        Controller.Editing = true;
        Helper.Input.Suppress(e.Button);
    }

    private void Tick(object? sender, UpdateTickingEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            drag.Value = null;
            Controller.Abandon();
            return;
        }
        try
        {
            if (!config.Enabled && Controller.HasSession)
                Reset();
            if (IsInventory(Game1.activeClickableMenu) && Controller.HasSession)
                Reset();
            Controller.Editing = config.Enabled && Editable() && config.SelectKey.IsDown();
            if (drag.Value is { } selection)
            {
                if (!config.Enabled || !Editable())
                    drag.Value = null;
                else
                {
                    selection.End = Cell.At(Helper.Input.GetCursorPosition().Tile);
                    if (!Controller.Editing || !(Helper.Input.IsDown(selection.Button) || Helper.Input.IsSuppressed(selection.Button)))
                    {
                        Controller.Board.Select(Game1.currentLocation, Game1.player, selection.Mode, selection.Tool,
                            SelectionGeometry.Rectangle(selection.Start, selection.End, WorkRules.MaxSelectionSize), config, selection.Material, selection.Smart);
                        drag.Value = null;
                        Controller.Resume();
                    }
                }
            }
            Controller.ObserveMovement(Game1.player, Editable() && MovingInput(), Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds);
            Controller.Tick(Game1.player, Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds, Eligible() && Game1.game1.IsActive && Game1.player.mount is null);
            if (pendingMenu.Value is { } menu)
            {
                if (!Editable())
                    pendingMenu.Value = null;
                else if (Controller.Active is null && Game1.CanShowPauseMenu())
                {
                    pendingMenu.Value = null;
                    Game1.PushUIMode();
                    try
                    {
                        Game1.activeClickableMenu = menu == "actions" ? new ActionMenu(config, SaveConfig, T) : new GameMenu();
                    }
                    finally { Game1.PopUIMode(); }
                }
            }
        }
        catch (Exception ex) { drag.Value = null; Controller.Pause("error", true); Monitor.Log("Behavior automation paused: " + ex, LogLevel.Error); }
    }

    [EventPriority(EventPriority.Low)]
    private void DrawOverlay(object? sender, RenderedEventArgs e)
    {
        if (!Context.IsWorldReady || !config.Enabled || Game1.activeClickableMenu is not null || Game1.game1.takingMapScreenshot)
            return;
        if (!Controller.Editing && !Controller.HasSession)
            return;
        var cursor = Helper.Input.GetCursorPosition();
        var ui = cursor.GetScaledScreenPixels();
        bool overHud = Game1.onScreenMenus.Any(m => m.isWithinBounds((int)ui.X, (int)ui.Y));
        Cell? hover = Controller.Editing && (drag.Value is not null || !overHud) ? Cell.At(cursor.Tile) : null;
        try
        {
            overlays.Value.DrawTop(e.SpriteBatch, Controller, drag.Value, config, hover);
            drawErrorLogged = false;
        }
        catch (Exception ex) { if (!drawErrorLogged) Monitor.Log("Selection overlay failed: " + ex, LogLevel.Error); drawErrorLogged = true; }
    }
}
