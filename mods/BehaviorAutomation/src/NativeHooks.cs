using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
namespace Sznine.BehaviorAutomation;

/// <summary>Native hooks are scoped to the local controller and its exact active tool.</summary>
internal static class NativeHooks
{
    private static Func<WorkController>? current;
    public static void Install(Harmony harmony, Func<WorkController> controller)
    {
        current = controller;
        WalkingInput.Install(harmony, controller);
        harmony.Patch(AccessTools.Method(typeof(Farmer), nameof(Farmer.getMovementSpeed)),
            postfix: new HarmonyMethod(typeof(WalkRoute), nameof(WalkRoute.LimitArrivalStep)));
        harmony.Patch(AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
            prefix: new HarmonyMethod(typeof(NativeHooks), nameof(Harvest)) { priority = Priority.First });
        harmony.Patch(AccessTools.Method(typeof(Character), nameof(Character.GetToolLocation), new[] { typeof(bool) }), prefix: new HarmonyMethod(typeof(NativeHooks), nameof(ToolTarget)));
        foreach (var type in new[] { typeof(Axe), typeof(Pickaxe), typeof(Hoe), typeof(WateringCan) })
            harmony.Patch(AccessTools.Method(type, "DoFunction"), prefix: new HarmonyMethod(typeof(NativeHooks), nameof(Impact)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon), nameof(MeleeWeapon.DoDamage)), prefix: new HarmonyMethod(typeof(NativeHooks), nameof(Impact)));
        foreach (var type in new[] { typeof(MilkPail), typeof(Shears) })
            harmony.Patch(AccessTools.Method(type, "DoFunction"), prefix: new HarmonyMethod(typeof(NativeHooks), nameof(AnimalImpact)));
        foreach (var type in new[] { typeof(StardewValley.Object), typeof(StardewValley.Objects.IndoorPot), typeof(Tree), typeof(FruitTree), typeof(Bush), typeof(Grass), typeof(Flooring), typeof(HoeDirt), typeof(ResourceClump), typeof(GiantCrop) })
        {
            var method = AccessTools.DeclaredMethod(type, "performToolAction");
            if (method is not null && method.ReturnType == typeof(bool))
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(NativeHooks), nameof(Damage)) { priority = Priority.First });
        }
        harmony.Patch(AccessTools.Method(typeof(Utility), "GetBestHarvestableFarmAnimal"), prefix: new HarmonyMethod(typeof(NativeHooks), nameof(ChooseAnimal)));
        harmony.Patch(AccessTools.Method(typeof(PetBowl), nameof(PetBowl.performToolAction)), prefix: new HarmonyMethod(typeof(NativeHooks), nameof(BowlImpact)));
    }
    public static void Compatibility(Harmony harmony, IModRegistry registry, IMonitor monitor)
    {
        PassabilityProbe.Install(harmony, registry, monitor);
        if (registry.IsLoaded("sznine.SmartWateringCan") && AccessTools.TypeByName("Sznine.SmartWateringCan.WaterController") is { } water)
            harmony.Patch(AccessTools.Method(water, "Tick"), prefix: new HarmonyMethod(typeof(NativeHooks), nameof(YieldWater)));
    }
    private static bool ToolTarget(Character __instance, ref Vector2 __result)
    {
        if (current?.Invoke().TryToolTarget(__instance, out var aim) == true)
        {
            __result = aim;
            return false;
        }
        return true;
    }
    private static bool Harvest(Crop __instance, int xTile, int yTile, HoeDirt soil)
        => current?.Invoke().AllowHarvest(__instance, soil, xTile, yTile) ?? true;
    private static bool Impact(Tool __instance, GameLocation location, Farmer who) => current?.Invoke().AllowTool(__instance, who, location) ?? true;
    private static bool BowlImpact(PetBowl __instance, Tool t) => current?.Invoke().AllowEntity(__instance, t) ?? true;
    private static bool AnimalImpact(Tool __instance, GameLocation location, Farmer who)
    {
        if (current?.Invoke().AllowTool(__instance, who, location) != false)
            return true;
        // The native finish event still restores movement and clears the animal reference after cancellation.
        AccessTools.Method(__instance.GetType(), "finish").Invoke(__instance, null);
        return false;
    }
    private static bool Damage(object __instance, object[] __args, ref bool __result)
    {
        var tool = __args.OfType<Tool>().FirstOrDefault();
        var control = current?.Invoke();
        if (control?.AllowEntity(__instance, tool) != false && control?.DeferGather(__instance, tool) != true)
            return true;
        __result = false;
        return false;
    }
    private static bool ChooseAnimal(Tool tool, Rectangle toolRect, ref FarmAnimal? __result)
    {
        if (current?.Invoke().SelectedAnimal(tool) is not { } animal)
            return true;
        __result = toolRect.Intersects(animal.GetBoundingBox()) ? animal : null;
        return false;
    }
    private static bool YieldWater(object __instance)
    {
        AccessTools.Method(__instance.GetType(), "Cancel")?.Invoke(__instance, new object[] { true });
        return false;
    }
}
