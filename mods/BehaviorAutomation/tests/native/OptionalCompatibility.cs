namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private bool RequireTestMod(string uniqueId, string description)
    {
        if (Helper.ModRegistry.IsLoaded(uniqueId))
            return true;

        results.Add($"SKIP {description}: optional mod {uniqueId} is not installed");
        return false;
    }

    private void CheckWithMod(string uniqueId, string description, Action action)
    {
        if (RequireTestMod(uniqueId, description))
            Check(description, action);
    }
}
