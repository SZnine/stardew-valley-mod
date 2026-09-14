using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace Sznine.BehaviorAutomation;

public sealed record StoredTool(Chest Chest, IList<Item> Items, Tool Tool)
{
    public bool Available => !Chest.GetMutex().IsLocked() && Items.Contains(Tool);
}

public static class ToolStorage
{
    public static int EmptySlot(Farmer who) => Enumerable.Range(0, Math.Min(who.Items.Count, who.MaxItems)).FirstOrDefault(i => who.Items[i] is null, -1);
    // Remote multiplayer transfers require a host transaction protocol, not a local chest edit.
    public static List<(Chest Chest, IList<Item> Items)> Sources(Farmer who)
    {
        var result = new List<(Chest, IList<Item>)>();
        if (Context.IsMultiplayer)
            return result;
        var seen = new HashSet<IList<Item>>(ReferenceEqualityComparer.Instance);
        void Add(Chest? chest)
        {
            if (chest is null || chest.SpecialChestType == Chest.SpecialChestTypes.MiniShippingBin)
                return;
            var items = chest.GetItemsForPlayer(who.UniqueMultiplayerID);
            if (seen.Add(items))
                result.Add((chest, items));
        }
        void Location(GameLocation location)
        {
            Add(location.GetFridge());
            foreach (var chest in location.Objects.Values.OfType<Chest>())
                if (chest.playerChest.Value)
                    Add(chest);
            foreach (var building in location.buildings)
                foreach (var chest in building.buildingChests)
                    Add(chest);
        }
        if (who.currentLocation is { } current)
            Location(current);
        Utility.ForEachLocation(location => { Location(location); return true; }, includeInteriors: true, includeGenerated: true);
        return result;
    }
    public static IEnumerable<(Tool Tool, StoredTool? Storage)> Tools(Farmer who, ModConfig config)
    {
        var seen = new HashSet<Tool>(ReferenceEqualityComparer.Instance);
        foreach (var tool in who.Items.Take(who.MaxItems).OfType<Tool>())
            if (seen.Add(tool))
                yield return (tool, null);
        if (!config.UseStoredTools)
            yield break;
        foreach (var (chest, items) in Sources(who))
        {
            if (chest.GetMutex().IsLocked())
                continue;
            foreach (var tool in items.OfType<Tool>())
                if (seen.Add(tool))
                    yield return (tool, new(chest, items, tool));
        }
    }
    public static bool Present(StoredTool source, Farmer who) => Sources(who).Any(s => ReferenceEquals(s.Items, source.Items));
}

public sealed class ToolLoan
{
    private readonly StoredTool source;
    public StoredTool Source => source;
    private readonly Farmer owner;
    private readonly int originalSlot;
    private ToolLoan(StoredTool source, Farmer owner, int slot)
    {
        this.source = source;
        this.owner = owner;
        originalSlot = slot;
    }
    public static ToolLoan? Borrow(StoredTool source, Farmer who)
    {
        if (Context.IsMultiplayer || !source.Available || !ToolStorage.Present(source, who))
            return null;
        int slot = ToolStorage.EmptySlot(who);
        int old = source.Items.IndexOf(source.Tool);
        if (slot < 0 || old < 0)
            return null;
        // Single game-thread transaction. Move the original instance, including enchantments and water.
        source.Items[old] = null!;
        who.Items[slot] = source.Tool;
        return new(source, who, old);
    }
    public bool Return()
    {
        int slot = owner.Items.IndexOf(source.Tool);
        if (slot < 0)
            return true; // Another legitimate inventory action already moved it; never recreate it.
        if (Context.IsMultiplayer || source.Chest.GetMutex().IsLocked() || !ToolStorage.Present(source, owner))
            return false;
        int destination = originalSlot < source.Items.Count && source.Items[originalSlot] is null ? originalSlot :
            Enumerable.Range(0, source.Items.Count).FirstOrDefault(i => source.Items[i] is null, -1);
        if (destination < 0 && source.Items.Count >= source.Chest.GetActualCapacity())
            return false;
        if (destination >= 0)
            source.Items[destination] = source.Tool;
        else
            source.Items.Add(source.Tool);
        owner.Items[slot] = null!;
        return true;
    }
}
