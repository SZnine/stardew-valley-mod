using StardewValley;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.Objects;

namespace Sznine.BehaviorAutomation;

public sealed record FeedSpot(AnimalHouse House, Cell Cell);
public static class AnimalCare
{
    public static Chest? Grabber(StardewValley.Object obj) => obj.QualifiedItemId == "(BC)165" ? obj.heldObject.Value as Chest : null;
    public static string? GrabberBlocked(StardewValley.Object obj, Farmer who)
    {
        if (Grabber(obj) is not { } chest)
            return null;
        if (Context.IsMultiplayer || chest.GetMutex().IsLocked())
            return "grabber-manual";
        return chest.Items.Any(i => i is not null && who.couldInventoryAcceptThisItem(i)) ? null : "inventory";
    }
    public static void CollectGrabber(StardewValley.Object obj, Farmer who)
    {
        // Single-player, single-thread transfer of the original stacks; never drain ordinary chests.
        if (Grabber(obj) is not { } chest || GrabberBlocked(obj, who) is not null)
            return;
        for (int i = 0; i < chest.Items.Count; i++)
        {
            var item = chest.Items[i];
            if (item is null || !who.couldInventoryAcceptThisItem(item))
                continue;
            chest.Items[i] = null!;
            chest.Items[i] = who.addItemToInventory(item)!;
        }
        chest.clearNulls();
        if (chest.isEmpty())
            obj.showNextIndex.Value = false;
    }
    private static Item? hayIcon;
    public static Item HayIcon => hayIcon ??= ItemRegistry.Create("(O)178");
    public static bool EmptyTrough(GameLocation map, Cell p) => map is AnimalHouse && map.doesTileHaveProperty(p.X, p.Y, "Trough", "Back") is not null && !map.Objects.ContainsKey(p.Tile);
    public static bool HasHay(Farmer who)
    {
        if (who.Items.Any(i => i?.QualifiedItemId == "(O)178" && i.Stack > 0))
            return true;
        bool found = who.currentLocation.GetRootLocation().piecesOfHay.Value > 0;
        if (!found)
            Utility.ForEachLocation(map => { found = map.piecesOfHay.Value > 0; return !found; }, includeInteriors: false);
        return found;
    }
    public static bool Feed(Farmer who, FeedSpot spot)
    {
        if (!EmptyTrough(spot.House, spot.Cell))
            return false;
        int oldSlot = who.CurrentToolIndex;
        int haySlot = Enumerable.Range(0, Math.Min(who.Items.Count, who.MaxItems)).FirstOrDefault(i => who.Items[i]?.QualifiedItemId == "(O)178" && who.Items[i].Stack > 0, -1);
        if (haySlot >= 0)
        {
            who.CurrentToolIndex = haySlot;
            try
            {
                return spot.House.checkAction(new xTile.Dimensions.Location(spot.Cell.X, spot.Cell.Y), Game1.viewport, who);
            }
            finally { who.CurrentToolIndex = oldSlot; }
        }
        var root = spot.House.GetRootLocation();
        var hay = GameLocation.GetHayFromAnySilo(root);
        if (hay is null)
            return false;
        var held = who.Items[oldSlot];
        bool placed = false;
        who.Items[oldSlot] = hay;
        try
        {
            placed = spot.House.checkAction(new xTile.Dimensions.Location(spot.Cell.X, spot.Cell.Y), Game1.viewport, who);
            return placed;
        }
        finally
        {
            who.Items[oldSlot] = held!;
            if (!placed && GameLocation.StoreHayInAnySilo(1, root) > 0)
                Game1.createItemDebris(ItemRegistry.Create("(O)178"), who.Position, -1, spot.House);
        }
    }
}
