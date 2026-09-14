using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace Sznine.BehaviorAutomation;

/// <summary>Place the selected stack on native-valid tiles; never infer a construction material.</summary>
public static class Placement
{
    public static bool Supports(SObject item) => item is not (Furniture or Wallpaper)
        && item.QualifiedItemId != "(BC)71"
        && item.isPlaceable() && !item.isSapling() && item.Category is not (-74 or -19)
        && (item.IsFloorPathItem() || item.IsFenceItem() || item.IsSprinkler() || item.IsTapper()
            || item.bigCraftable.Value || item.HasContextTag("torch_item") || item.QualifiedItemId == "(O)710");

    public static ActionKind Kind(SObject item) => item.IsFloorPathItem() ? ActionKind.PlaceFloor : ActionKind.PlaceObject;
    public static SObject? FindStock(Farmer who, SObject template) => who.Items.OfType<SObject>()
        .FirstOrDefault(i => i.Stack > 0 && i.QualifiedItemId == template.QualifiedItemId && i.canStackWith(template));

    public static bool CanPlace(GameLocation map, SObject template, Cell at)
    {
        if (!Supports(template) || map.Map is null || at.X < 0 || at.Y < 0
            || at.X >= map.Map.Layers[0].LayerWidth || at.Y >= map.Map.Layers[0].LayerHeight
            || Utility.isPlacementForbiddenHere(map) || map.Objects.ContainsKey(at.Tile))
            return false;
        var terrain = map.terrainFeatures.GetValueOrDefault(at.Tile);
        if (template.IsFloorPathItem() && terrain is not null)
            return false;
        if (terrain is not null && terrain is not Flooring && !(template.IsTapper() && terrain is Tree))
            return false;
        // The player can step away before placement. All other native collision rules remain in force.
        return template.canBePlacedHere(map, at.Tile, CollisionMask.All & ~CollisionMask.Farmers, showError: false);
    }

    public static List<WorkTarget> Scan(GameLocation map, Farmer who, SObject selected, Rectangle area, ModConfig config)
    {
        var jobs = new List<WorkTarget>();
        if (!Supports(selected) || !config.Allows(Kind(selected)))
            return jobs;
        int stock = who.Items.OfType<SObject>().Where(i => i.Stack > 0 && i.QualifiedItemId == selected.QualifiedItemId && i.canStackWith(selected)).Sum(i => i.Stack);
        for (int y = area.Top; y < area.Bottom && jobs.Count < stock; y++)
            for (int x = area.Left; x < area.Right && jobs.Count < stock; x++)
            {
                var at = new Cell(x, y);
                if (!CanPlace(map, selected, at))
                    continue;
                jobs.Add(new()
                {
                    Mode = ToolMode.Place,
                    Kind = Kind(selected),
                    Scope = WorkScope.Held,
                    Origin = at,
                    Entity = at,
                    Area = new(x, y, 1, 1),
                    Material = (SObject)selected.getOne()
                });
            }
        return jobs;
    }
}
