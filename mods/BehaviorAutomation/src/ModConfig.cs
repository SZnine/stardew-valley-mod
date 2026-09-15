using StardewModdingAPI.Utilities;

namespace Sznine.BehaviorAutomation;

public enum SelectionStyle { Filled, Outline, Grid }
public enum MenuStyle { Cards, Sidebar, List }

/// <summary>User choices; hard safety and per-frame search limits belong to WorkRules.</summary>
public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public KeybindList SelectKey { get; set; } = KeybindList.Parse("LeftShift, RightShift");
    public KeybindList ActionMenuKey { get; set; } = KeybindList.Parse("F8");
    public KeybindList CancelKey { get; set; } = KeybindList.Parse("LeftShift, RightShift");
    public bool UseStoredTools { get; set; } = true;
    public float ReserveStamina { get; set; } = 10;
    public float MovementCancelSeconds { get; set; } = .5f;
    public bool AllowDiagonalMovement { get; set; } = true;
    public int ScytheSearchTiles { get; set; } = 12;
    public int ScytheSwingCost { get; set; } = 16;
    public bool AutoRefillWateringCan { get; set; } = true;
    public bool ClearObstacles { get; set; } = true;
    public bool PanWhileSelecting { get; set; } = true;
    public bool ShowSelectionSize { get; set; } = true;
    public SelectionStyle SelectionAppearance { get; set; } = SelectionStyle.Grid;
    public MenuStyle MenuAppearance { get; set; } = MenuStyle.Sidebar;
    public int SelectionPanSpeed { get; set; } = 12;
    public bool WorkInsideBuildings { get; set; } = true;
    public int WildTreeSpacing { get; set; } = 2;
    public int FruitTreeSpacing { get; set; } = 3;
    public float RefreshIntervalSeconds { get; set; } = .3f;
    public float CompletionDelaySeconds { get; set; } = 1.5f;
    // Serialized as Actions for backward compatibility; applies only to held-item work.
    public HashSet<ActionKind> Actions
    {
        get; set;
    } = Enum.GetValues<ActionKind>()
        .Except(new[] { ActionKind.Sapling, ActionKind.FruitTree, ActionKind.BuildingInterior }).ToHashSet();
    public HashSet<ActionKind> SmartActions
    {
        get; set;
    } = ActionCatalog.SmartKinds
        .Except(new[] { ActionKind.Sapling, ActionKind.FruitTree }).ToHashSet();
    // Extra tasks selected with any held item, independent of held-item and right-click pools.
    public HashSet<ActionKind> LeftActions
    {
        get; set;
    } = new()
    {
        ActionKind.Pet, ActionKind.Milk, ActionKind.Shear, ActionKind.Feed, ActionKind.WaterBowl,
        ActionKind.HarvestCrop, ActionKind.Fruit, ActionKind.Bush, ActionKind.Forage, ActionKind.Machine
    };
    public int ConfigVersion
    {
        get; set;
    }

    public bool Allows(ActionKind kind, WorkScope scope = WorkScope.Held) => scope switch
    {
        WorkScope.LeftExtra => ActionCatalog.CanSelectSmart(kind) && LeftActions.Contains(kind),
        WorkScope.Smart => ActionCatalog.CanSelectSmart(kind) && SmartActions.Contains(kind),
        _ => Actions.Contains(kind)
    };

    public void Normalize()
    {
        SelectKey ??= KeybindList.Parse("LeftShift, RightShift");
        ActionMenuKey ??= KeybindList.Parse("F8");
        CancelKey ??= KeybindList.Parse("LeftShift, RightShift");
        if (!Enum.IsDefined(typeof(SelectionStyle), SelectionAppearance)) SelectionAppearance = SelectionStyle.Grid;
        if (!Enum.IsDefined(typeof(MenuStyle), MenuAppearance)) MenuAppearance = MenuStyle.Sidebar;
        ReserveStamina = float.IsFinite(ReserveStamina) ? Math.Clamp(ReserveStamina, 0, 100) : 10;
        MovementCancelSeconds = float.IsFinite(MovementCancelSeconds) ? Math.Clamp(MovementCancelSeconds, .1f, 2) : .5f;
        ScytheSearchTiles = Math.Clamp(ScytheSearchTiles, 2, 24);
        ScytheSwingCost = Math.Clamp(ScytheSwingCost, 4, 32);
        SelectionPanSpeed = Math.Clamp(SelectionPanSpeed, 4, 24);
        WildTreeSpacing = Math.Clamp(WildTreeSpacing, 2, 8);
        FruitTreeSpacing = Math.Clamp(FruitTreeSpacing, 3, 8);
        RefreshIntervalSeconds = float.IsFinite(RefreshIntervalSeconds) ? Math.Clamp(RefreshIntervalSeconds, .1f, 1) : .3f;
        CompletionDelaySeconds = float.IsFinite(CompletionDelaySeconds) ? Math.Clamp(CompletionDelaySeconds, .3f, 3) : 1.5f;
        Actions ??= new();
        SmartActions ??= new();
        LeftActions ??= new();
        Actions.RemoveWhere(k => !Enum.IsDefined(typeof(ActionKind), k) || k == ActionKind.BuildingInterior);
        SmartActions.RemoveWhere(k => !ActionCatalog.CanSelectSmart(k));
        LeftActions.RemoveWhere(k => !ActionCatalog.CanSelectSmart(k));
    }

    public void Migrate()
    {
        Normalize();
        if (ConfigVersion < 1)
        {
            Actions.Add(ActionKind.Feed);
            Actions.Add(ActionKind.WaterBowl);
        }
        if (ConfigVersion < 6)
        {
            // Preserve the effective old pools once, then let all three evolve independently.
            SmartActions.IntersectWith(Actions);
            LeftActions.IntersectWith(Actions);
            Actions.Add(ActionKind.PlaceFloor);
            Actions.Add(ActionKind.PlaceObject);
        }
        if (ConfigVersion < 7)
            Actions.Add(ActionKind.RemoveFloor);
        ConfigVersion = 9;
    }
}

internal static class WorkRules
{
    public const int MaxSelectionSize = 64;
    public const int MaxJobs = 4096;
}
