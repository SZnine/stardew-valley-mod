using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

public enum ToolMode
{
    Hand, Pickaxe, Axe, Scythe, Hoe, WateringCan, MilkPail, Shears, Seeds, TreeSeeds, Auto, Place
}
public enum ActionKind
{
    Pet, Milk, Shear, Stone, LargeRock, Twig, WildTree, TreeStump, LargeWood, Sapling, FruitTree, Weed, Grass, DeadCrop, HarvestCrop, Fruit, Bush, Forage, Machine, Water, Artifact, Till, PlantCrop, PlantWildTree, PlantFruitTree, PlantTea, Feed, WaterBowl, PlaceFloor, PlaceObject, RemoveFloor
}
public enum WorkScope
{
    Held, LeftExtra, Smart
}

public readonly record struct Cell(int X, int Y)
{
    public static readonly Cell[] Directions = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };
    public Vector2 Tile => new(X, Y);
    public Vector2 Center => new(X * 64 + 32, Y * 64 + 32);
    public Cell Add(Cell other) => new(X + other.X, Y + other.Y);
    public static Cell At(Vector2 tile) => new((int)Math.Floor(tile.X), (int)Math.Floor(tile.Y));
    public static Cell Of(Farmer who) => new(who.TilePoint.X, who.TilePoint.Y);
}
public static class SelectionGeometry
{
    public static Rectangle Rectangle(Cell a, Cell b, int maxSize = 64)
    {
        maxSize = Math.Max(1, maxSize);
        var end = new Cell(Math.Clamp(b.X, a.X - maxSize + 1, a.X + maxSize - 1), Math.Clamp(b.Y, a.Y - maxSize + 1, a.Y + maxSize - 1));
        return new(Math.Min(a.X, end.X), Math.Min(a.Y, end.Y), Math.Abs(a.X - end.X) + 1, Math.Abs(a.Y - end.Y) + 1);
    }
}
public sealed class WorkTarget
{
    public ToolMode Mode
    {
        get; init;
    }
    public ActionKind Kind
    {
        get; init;
    }
    public object Entity { get; init; } = null!;
    public Cell Origin
    {
        get; init;
    }
    public Rectangle Area
    {
        get; init;
    }
    public Tool? Tool
    {
        get; init;
    }
    public StoredTool? Storage
    {
        get; set;
    }
    public bool IsObstacle
    {
        get; set;
    }
    public WorkScope Scope
    {
        get; set;
    }
    public StardewValley.Object? Material
    {
        get; init;
    }
    public Item? Icon => (Item?)Tool ?? Material ?? (Kind == ActionKind.Feed ? AnimalCare.HayIcon : null);
    public long Group
    {
        get; set;
    }
    public int FailedRoutes, FailedActions;
    public Rectangle Bounds => Entity is Character animal ? animal.GetBoundingBox() : new(Area.X * 64, Area.Y * 64, Area.Width * 64, Area.Height * 64);
    public Vector2 Aim => Entity is Character animal ? animal.GetBoundingBox().Center.ToVector2() : Origin.Center;
}
public static class ModeInfo
{
    public static ToolMode? From(Item? item) => item switch
    {
        null => ToolMode.Hand,
        Pickaxe => ToolMode.Pickaxe,
        Axe => ToolMode.Axe,
        MeleeWeapon w when w.isScythe() => ToolMode.Scythe,
        Hoe => ToolMode.Hoe,
        WateringCan => ToolMode.WateringCan,
        MilkPail => ToolMode.MilkPail,
        Shears => ToolMode.Shears,
        StardewValley.Object seed when seed.IsWildTreeSapling() || seed.IsFruitTreeSapling() => ToolMode.TreeSeeds,
        StardewValley.Object seed when seed.Category == -74 || seed.IsTeaSapling() => ToolMode.Seeds,
        StardewValley.Object placeable when Placement.Supports(placeable) => ToolMode.Place,
        _ => null
    };
    public static Color Color(ToolMode mode) => mode switch
    {
        ToolMode.Hand or ToolMode.Auto => new(40, 255, 100),
        ToolMode.Pickaxe => new(75, 125, 255),
        ToolMode.Axe => new(255, 145, 35),
        ToolMode.Scythe => new(195, 255, 35),
        ToolMode.Hoe => new(255, 95, 100),
        ToolMode.WateringCan => new(25, 240, 255),
        ToolMode.MilkPail => new(255, 235, 100),
        ToolMode.Seeds => new(205, 120, 255),
        ToolMode.TreeSeeds => new(20, 210, 165),
        _ => new(255, 75, 210)
    };
}
