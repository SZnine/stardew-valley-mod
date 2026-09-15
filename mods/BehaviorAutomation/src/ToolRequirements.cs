using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace Sznine.BehaviorAutomation;

/// <summary>Non-mutating preflight for vanilla tool gates. Native tool actions still decide damage and drops.</summary>
internal static class ToolRequirements
{
    // Stardew Valley 1.6.15: ResourceClump.performToolAction and Pickaxe.DoFunction.
    // GiantCrop overrides the clump action; its inherited sprite ID is not a resource requirement.
    public static bool Allows(object entity, Tool? tool, bool duringImpact = false)
    {
        int axeLevel = tool?.UpgradeLevel ?? -1;
        // Axe.DoFunction temporarily adds Powerful's additionalPower before entering the entity.
        // Pickaxe's additionalPower only affects ordinary stone damage, not its clump grade gate.
        if (!duringImpact && tool is Axe axe)
            axeLevel += axe.additionalPower.Value;
        return entity switch
        {
            GiantCrop => tool is Axe,
            ResourceClump c => c.parentSheetIndex.Value switch
            {
                600 => tool is Axe && axeLevel >= 1,
                602 => tool is Axe && axeLevel >= 2,
                148 or 622 => tool is Pickaxe && tool.UpgradeLevel >= 3,
                672 => tool is Pickaxe && tool.UpgradeLevel >= 2,
                752 or 754 or 756 or 758 => tool is Pickaxe,
                _ => true // Custom clumps and green rain foliage retain their native/mod action.
            },
            StardewValley.Object o when !o.IsBreakableStone() && o.Name.Contains("Boulder", StringComparison.Ordinal)
                => tool is Pickaxe && tool.UpgradeLevel >= 2,
            _ => true
        };
    }
}
