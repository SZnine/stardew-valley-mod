using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;

namespace Sznine.BehaviorAutomation;

public static class PassabilityProbe
{
    [ThreadStatic] private static int depth;
    public static bool Active => depth > 0;
    public static Scope Enter()
    {
        depth++;
        return new();
    }
    public readonly struct Scope : IDisposable
    {
        public void Dispose() => depth--;
    }

    public static void Install(Harmony harmony, IModRegistry registry, IMonitor monitor)
    {
        if (!registry.IsLoaded("NCarigon.PassableCrops"))
            return;
        var patched = new List<MethodInfo>();
        try
        {
            foreach (var (type, method) in new[] { ("Trees", "Tree"), ("FruitTrees", "FruitTree"), ("Bushes", "Bush"), ("Objects", "Object") })
            {
                var target = AccessTools.Method(AccessTools.TypeByName("PassableCrops.Patches." + type), "Postfix_" + method + "_isPassable")
                    ?? throw new MissingMethodException("Passable Crops: " + type);
                patched.Add(target);
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(PassabilityProbe), nameof(SkipProbeEffects)));
            }
        }
        catch (Exception ex)
        {
            foreach (var target in patched)
                harmony.Unpatch(target, HarmonyPatchType.Transpiler, harmony.Id);
            monitor.Log("Passable Crops query compatibility could not be applied: " + ex, LogLevel.Warn);
        }
    }

    private static IEnumerable<CodeInstruction> SkipProbeEffects(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var code = instructions.ToList();
        // Keep the installed mod's own eligibility tests and __result=true assignment.
        // Its following code applies passage effects even when native pathfinding=true.
        // Exit before animation, sound, speed, RNG and object modData writes, only inside our query.
        var matches = Enumerable.Range(0, Math.Max(0, code.Count - 2)).Where(i => code[i].opcode == OpCodes.Ldarg_1
            && code[i + 1].opcode == OpCodes.Ldc_I4_1 && code[i + 2].opcode == OpCodes.Stind_I1).ToArray();
        if (matches.Length != 1 || code[^1].opcode != OpCodes.Ret)
            throw new InvalidOperationException("Unrecognized Passable Crops passability method; no rewrite applied.");
        int at = matches[0] + 3;
        var resume = generator.DefineLabel();
        var done = generator.DefineLabel();
        code[at].labels.Add(resume);
        code[^1].labels.Add(done);
        bool protectedBlock = code.Take(at).SelectMany(c => c.blocks).Any(b => b.blockType == ExceptionBlockType.BeginExceptionBlock);
        code.InsertRange(at, new[]{
            new CodeInstruction(OpCodes.Call,AccessTools.PropertyGetter(typeof(PassabilityProbe),nameof(Active))),
            new CodeInstruction(OpCodes.Brfalse,resume),
            new CodeInstruction(protectedBlock?OpCodes.Leave:OpCodes.Br,done)
        });
        return code;
    }
}
