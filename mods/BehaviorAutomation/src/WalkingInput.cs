using HarmonyLib;
using StardewValley;

namespace Sznine.BehaviorAutomation;

/// <summary>Game input normally halts an idle keyboard every frame, restarting a pathing farmer's stride.</summary>
internal static class WalkingInput
{
    [ThreadStatic] private static int inputDepth;
    private static Func<WorkController> current = null!;

    public static void Install(Harmony harmony, Func<WorkController> controller)
    {
        current = controller;
        harmony.Patch(AccessTools.Method(typeof(Game1), "UpdateControlInput"),
            prefix: new HarmonyMethod(typeof(WalkingInput), nameof(BeginInput)),
            finalizer: new HarmonyMethod(typeof(WalkingInput), nameof(EndInput)));
        harmony.Patch(AccessTools.Method(typeof(Farmer), nameof(Farmer.Halt)),
            prefix: new HarmonyMethod(typeof(WalkingInput), nameof(AllowHalt)));
        harmony.Patch(AccessTools.Method(typeof(Farmer), nameof(Farmer.setMoving)),
            prefix: new HarmonyMethod(typeof(WalkingInput), nameof(AllowCommand)));
    }

    private static void BeginInput(out int __state)
    {
        __state = inputDepth;
        inputDepth++;
    }
    private static void EndInput(int __state) => inputDepth = __state;
    private static bool KeepStride(Farmer who) => inputDepth > 0 && current().OwnsWalking(who)
        && Game1.activeClickableMenu is null && !Game1.eventUp && !Game1.IsChatting
        && !Game1.freezeControls && who.CanMove && !who.UsingTool && !who.isEating && who.freezePause <= 0;
    private static bool AllowHalt(Farmer __instance) => !KeepStride(__instance);
    private static bool AllowCommand(Farmer __instance, byte command) => command is not (33 or 34 or 36 or 40 or 64) || !KeepStride(__instance);
}
