using StardewModdingAPI.Utilities;

namespace Sznine.BehaviorAutomation;

/// <summary>User choices only. Scheduling and geometry limits belong to WorkRules.</summary>
public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public KeybindList SelectKey { get; set; } = KeybindList.Parse("LeftShift, RightShift");
    public KeybindList ActionMenuKey { get; set; } = KeybindList.Parse("F8");
    public KeybindList CancelKey { get; set; } = KeybindList.Parse("LeftShift, RightShift");
    public bool UseStoredTools { get; set; } = true;
    public float ReserveStamina { get; set; } = 10;
    // Serialized as Actions for backward compatibility; applies only to held-item work.
    public HashSet<ActionKind> Actions
    {
        get; set;
    } = Enum.GetValues<ActionKind>()
        .Except(new[] { ActionKind.Sapling, ActionKind.FruitTree }).ToHashSet();
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
        ReserveStamina = float.IsFinite(ReserveStamina) ? Math.Clamp(ReserveStamina, 0, 100) : 10;
        Actions ??= new();
        SmartActions ??= new();
        LeftActions ??= new();
        Actions.RemoveWhere(k => !Enum.IsDefined(typeof(ActionKind), k));
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
        ConfigVersion = 7;
    }
}

internal static class WorkRules
{
    public const int MaxSelectionSize = 64;
    public const int MaxJobs = 4096;
    public const int MovementCancelDelayMs = 2000;
    public const int CompletionDelayMs = 1500;
    public const int RefreshMs = 300;
    public const int WildTreeSpacing = 2;
    public const int FruitTreeSpacing = 3;
}
