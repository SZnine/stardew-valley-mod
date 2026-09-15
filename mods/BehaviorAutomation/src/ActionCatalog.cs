namespace Sznine.BehaviorAutomation;

public sealed record ActionDefinition(ActionKind Kind, string IconId, string Group);

/// <summary>One catalog drives menu order, native icons, and right-click eligibility.</summary>
public static class ActionCatalog
{
    public static readonly ActionDefinition[] All = {
        new(ActionKind.Pet, "(O)104", "care"), new(ActionKind.Milk, "(T)MilkPail", "care"),
        new(ActionKind.Shear, "(T)Shears", "care"), new(ActionKind.Feed, "(O)178", "care"),
        new(ActionKind.WaterBowl, "(T)WateringCan", "care"),
        new(ActionKind.HarvestCrop, "(O)24", "gather"), new(ActionKind.Fruit, "(O)613", "gather"),
        new(ActionKind.Bush, "(O)296", "gather"), new(ActionKind.Forage, "(O)16", "gather"),
        new(ActionKind.Machine, "(BC)13", "gather"),
        new(ActionKind.Water, "(T)WateringCan", "farm"), new(ActionKind.Till, "(T)Hoe", "farm"),
        new(ActionKind.Artifact, "(O)590", "farm"), new(ActionKind.PlantCrop, "(O)472", "farm"),
        new(ActionKind.PlantWildTree, "(O)309", "farm"), new(ActionKind.PlantFruitTree, "(O)628", "farm"),
        new(ActionKind.PlantTea, "(O)251", "farm"),
        new(ActionKind.PlaceFloor, "(O)328", "place"), new(ActionKind.PlaceObject, "(O)599", "place"),
        new(ActionKind.RemoveFloor, "(O)328", "place"),
        new(ActionKind.Weed, "(O)0", "clear"), new(ActionKind.Grass, "(O)297", "clear"),
        new(ActionKind.DeadCrop, "(W)47", "clear"),
        new(ActionKind.Stone, "(O)343", "mine"), new(ActionKind.LargeRock, "(O)386", "mine"),
        new(ActionKind.Twig, "(O)294", "wood"), new(ActionKind.WildTree, "(O)70", "wood"),
        new(ActionKind.TreeStump, "(O)709", "wood"), new(ActionKind.LargeWood, "(O)709", "wood"),
        new(ActionKind.Sapling, "(O)311", "wood"), new(ActionKind.FruitTree, "(O)633", "wood")
    };
    public static bool CanSelectSmart(ActionKind kind) => Enum.IsDefined(typeof(ActionKind), kind)
        && kind is not (ActionKind.BuildingInterior or ActionKind.Till or ActionKind.Artifact or ActionKind.PlantCrop
            or ActionKind.PlantWildTree or ActionKind.PlantFruitTree or ActionKind.PlantTea or ActionKind.PlaceFloor or ActionKind.PlaceObject or ActionKind.RemoveFloor);
    public static IEnumerable<ActionKind> SmartKinds => All.Select(a => a.Kind).Where(CanSelectSmart);
}
