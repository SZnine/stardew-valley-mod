using System.Diagnostics;
using System.Text.Json;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
using SObject = StardewValley.Object;

namespace BehaviorProbe;

// Development-only normal-frame tests. Loads a copied synthetic save, never a player's save.
// No calls to Frame(), controller.Tick(), path.update(), or animation advancement here.
public sealed partial class ModEntry
{
    private sealed record LiveCase(string Name, Action Prepare, Func<bool> Verify, bool Fault = false);
    private static ModEntry? live;
    private readonly Queue<LiveCase> liveCases = new();
    private readonly List<object> liveResults = new();
    private readonly List<object> liveTrace = new();
    private readonly Dictionary<Vector2, SObject> protectedProps = new();
    private LiveCase? liveCase;
    private string liveRoot = "";
    private string? liveScreenshot;
    private int liveLoaded, liveFrames, liveOperations, liveRefills, liveOpportunities, liveTouches, liveSettle;
    private double liveStart, liveTravel, liveMaxAction;
    private double liveBoot = Now;
    private Vector2 livePosition;
    private Operation? liveOperation;
    private WorkDecision? liveDecision;
    private bool liveInjected, liveRecovered, liveSpawned;
    private List<HoeDirt> liveCrops = new();
    private readonly List<HoeDirt> liveRipe = new();
    private readonly List<SObject> liveDebris = new();
    private Tool? liveLoan;
    private Chest? liveChest;
    private Tree? liveTree;
    private readonly List<FarmAnimal> liveAnimals = new();
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private void InstallRealtime()
    {
        live = this;
        liveRoot = JsonDocument.Parse(File.ReadAllText(Path.Combine(Helper.DirectoryPath, "realtime.json"))).RootElement.GetProperty("Root").GetString()!;
        Directory.CreateDirectory(Path.Combine(liveRoot, "evidence"));
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.Patch(AccessTools.Method(typeof(Program), nameof(Program.GetSavesFolder)), prefix: new(typeof(ModEntry), nameof(LiveSaves)));
        harmony.Patch(AccessTools.Method(typeof(Game1), nameof(Game1.getPlatformAchievement)), prefix: new(typeof(ModEntry), nameof(LiveNoAchievement)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Game), "IsActive"), prefix: new(typeof(ModEntry), nameof(LiveActive)));
        harmony.Patch(AccessTools.Method(typeof(Game1), "Draw", new[] { typeof(GameTime) }), postfix: new(typeof(ModEntry), nameof(LiveDraw)));
        harmony.Patch(AccessTools.Method(typeof(GameLocation), nameof(GameLocation.checkAction), new[] { typeof(xTile.Dimensions.Location), typeof(xTile.Dimensions.Rectangle), typeof(Farmer) }), prefix: new(typeof(ModEntry), nameof(LiveTouch)));
        Helper.Events.GameLoop.UpdateTicking += LiveBefore;
        Helper.Events.GameLoop.UpdateTicked += LiveAfter;
    }
    private static bool LiveSaves(ref string __result) { __result = Path.Combine(live!.liveRoot, "saves"); return false; }
    private static bool LiveNoAchievement() => false;
    private static bool LiveActive(ref bool __result) { __result = true; return false; }
    private static void LiveTouch(xTile.Dimensions.Location tileLocation)
    {
        if (live is { liveCase: not null } r && r.protectedProps.ContainsKey(new(tileLocation.X, tileLocation.Y))) r.liveTouches++;
    }
    private static void LiveDraw()
    {
        if (live?.liveScreenshot is not { } name) return;
        live.liveScreenshot = null;
        var device = Game1.graphics.GraphicsDevice;
        int w = device.PresentationParameters.BackBufferWidth, h = device.PresentationParameters.BackBufferHeight;
        var pixels = new Color[w * h]; device.GetBackBufferData(pixels);
        using var texture = new Texture2D(device, w, h); texture.SetData(pixels);
        using var output = File.Create(Path.Combine(live.liveRoot, "evidence", name)); texture.SaveAsPng(output, w, h);
    }
    [EventPriority(EventPriority.High)]
    private void LiveBefore(object? sender, UpdateTickingEventArgs e)
    {
        if (Game1.options is not null) Game1.options.pauseWhenOutOfFocus = false;
        if (!Context.IsWorldReady) return;
        SetButtons(CursorAtUi(new(5, 5)));
        Game1.timeOfDay = 900;
        foreach (var menu in Game1.onScreenMenus.Where(m => m.GetType().Name == "ButtonTutorialMenu").ToArray()) Game1.onScreenMenus.Remove(menu);
        if (liveCase?.Fault == true && liveInjected && !liveRecovered && Control.Active is not null)
        {
            // Simulate a lost native completion callback; controller recovery still runs on real game time.
            Who.FarmerSprite.StopAnimation(); Who.FarmerSprite.PauseForSingleAnimation = true;
            Who.canReleaseTool = false; Who.UsingTool = true; Who.CanMove = false;
        }
    }
    private void LiveAfter(object? sender, UpdateTickedEventArgs e)
    {
        try
        {
            if (liveCase is not null && Now - liveStart > 220 || liveLoaded < 90 && Now - liveBoot > 90)
                throw new Exception("Realtime watchdog: world or scene stopped updating");
            if (++ticks == 100)
            {
                Assert(!Context.IsWorldReady, "Realtime fixture must start at title");
                Game1.activeClickableMenu = null;
                SaveGame.Load("BehaviorDemo_260915220");
                return;
            }
            if (!Context.IsWorldReady || Game1.gameMode != 3 || SaveGame.IsProcessing) return;
            if (++liveLoaded == 90)
            {
                Assert(Game1.uniqueIDForThisGame == 260915220, "Unexpected synthetic save identity");
                Game1.game1.SetWindowSize(1280, 720);
                BuildLiveCases();
                Game1.warpFarmer("Farm", 20, 20, 1);
                return;
            }
            if (liveLoaded < 90 || Map is not Farm || Game1.isWarping || Game1.fadeToBlack || Game1.eventUp) return;
            if (liveCase is null)
            {
                if (++liveSettle < 40) return;
                if (liveCases.Count == 0) { FinishLive(null); return; }
                liveCase = liveCases.Dequeue();
                Reset();
                liveTrace.Clear(); protectedProps.Clear();
                liveOperations = liveRefills = liveOpportunities = liveFrames = liveTouches = 0;
                liveTravel = liveMaxAction = 0; liveOperation = null; liveDecision = null;
                liveInjected = liveRecovered = liveSpawned = false;
                liveCrops.Clear(); liveRipe.Clear(); liveDebris.Clear(); liveAnimals.Clear(); liveTree = null; liveChest = null; liveLoan = null;
                Game1.viewportFreeze = false;
                liveCase.Prepare();
                liveStart = Now; livePosition = Who.Position;
                liveScreenshot = liveCase.Name + "-before.png";
                return;
            }
            liveFrames++;
            double elapsed = Now - liveStart;
            liveTravel += Vector2.Distance(livePosition, Who.Position) / 64;
            livePosition = Who.Position;
            if (Control.Active is { } op)
            {
                liveMaxAction = Math.Max(liveMaxAction, op.Elapsed);
                if (!ReferenceEquals(op, liveOperation))
                {
                    liveOperation = op; liveOperations++;
                    if (op.Approach.Target.Entity is WaterSource) liveRefills++;
                    TraceLive("action", op.Approach.Target.Kind + ":" + op.Approach.Target.Origin);
                }
                if (liveCase.Name == "mixed-field" && !liveSpawned && op.Released)
                {
                    liveSpawned = true;
                    var forage = ObjectAt("(O)16", 25, 20); forage.isSpawnedObject.Value = true;
                    liveDebris.Add(forage);
                }
                if (liveCase.Fault && !liveInjected && op.SawUsing && !Who.canReleaseTool)
                { liveInjected = true; TraceLive("fault", "lost completion callback"); }
            }
            else if (liveCase.Fault && liveInjected)
            {
                liveRecovered = true;
                liveMaxAction = Math.Max(liveMaxAction, liveOperation?.Elapsed ?? 0);
            }
            var decision = Control.Board.Coordinator.LastDecision;
            if (decision is not null && !ReferenceEquals(decision, liveDecision))
            {
                liveDecision = decision;
                if (decision.Opportunity)
                {
                    liveOpportunities++;
                    Assert(decision.ChosenCost < decision.MainCost && decision.ExtraCost <= 20, "Invalid realtime opportunity distance");
                }
                TraceLive("decision", new { decision.Opportunity, decision.Main.Stand, Chosen = decision.Chosen.Stand, decision.MainCost, decision.ChosenCost, decision.ExtraCost });
            }
            if (liveFrames % 60 == 0)
            {
                TraceLive("tick", new { Control.State, Jobs = Control.Board.Jobs.Count, X = Who.Position.X / 64, Y = Who.Position.Y / 64, Who.UsingTool, Who.CanMove });
                File.WriteAllText(Path.Combine(liveRoot, "evidence", "progress.json"), JsonSerializer.Serialize(new { Case = liveCase.Name, Seconds = elapsed, Control.State, Jobs = Control.Board.Jobs.Count, Operations = liveOperations, Refills = liveRefills, Opportunities = liveOpportunities, Active = Control.Active?.Elapsed, Who.Position.X, Who.Position.Y }));
            }
            if (liveFrames == 600) liveScreenshot = liveCase.Name + "-during.png";
            if (Control.Paused || elapsed > 210) throw new Exception($"{liveCase.Name}: state={Control.State}, jobs={Control.Board.Jobs.Count}, elapsed={elapsed:F1}, using={Who.UsingTool}, move={Who.CanMove}, eligible={Sznine.BehaviorAutomation.ModEntry.Eligible()}");
            if (liveFrames < 60 || Control.State != "idle" || Control.Active is not null) return;
            Assert(liveCase.Verify(), "Final world check failed: " + liveCase.Name);
            Assert(liveTouches == 0 && protectedProps.All(p => ReferenceEquals(Map.Objects.GetValueOrDefault(p.Key), p.Value)), "Touched or removed a protected facility");
            Assert(Who.CanMove && !Who.UsingTool && !Who.FarmerSprite.PauseForSingleAnimation, "Tool/movement lock survived completion");
            liveResults.Add(new { Case = liveCase.Name, Frames = liveFrames, Seconds = elapsed, WalkTiles = liveTravel,
                Operations = liveOperations, Refills = liveRefills, Opportunities = liveOpportunities, Protected = protectedProps.Count,
                ProtectedTouches = liveTouches, MaxActionMilliseconds = liveMaxAction, FaultInjected = liveInjected, Recovered = liveRecovered, Passed = true });
            File.WriteAllText(Path.Combine(liveRoot, "evidence", liveCase.Name + "-trace.json"), JsonSerializer.Serialize(liveTrace));
            liveScreenshot = liveCase.Name + "-after.png";
            liveCase = null; liveSettle = 0;
        }
        catch (Exception ex) { FinishLive(ex); }
    }
    private void TraceLive(string kind, object data) => liveTrace.Add(new { Seconds = Now - liveStart, Kind = kind, Data = data });
    private void FinishLive(Exception? error)
    {
        if (error is not null)
        {
            File.WriteAllText(Path.Combine(liveRoot, "evidence", "failure.txt"), error.ToString());
            File.WriteAllText(Path.Combine(liveRoot, "evidence", "failed-trace.json"), JsonSerializer.Serialize(liveTrace));
        }
        File.WriteAllText(Path.Combine(liveRoot, "evidence", "result.json"), JsonSerializer.Serialize(new
        {
            Version = Behavior.ModManifest.Version.ToString(), ProductSHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(WorkController).Assembly.Location))),
            LoadedMods = Helper.ModRegistry.GetAll().Select(m => m.Manifest.UniqueID).ToArray(), CompletedUtc = DateTime.UtcNow,
            NativeGameFrames = true, Failed = error is not null, Error = error?.Message, Cases = liveResults
        }, new JsonSerializerOptions { WriteIndented = true }));
        GameRunner.instance.Exit();
    }
    private void LiveTools(bool borrow = false, int water = 100)
    {
        Who.Items[0] = new WateringCan { UpgradeLevel = 4, WaterLeft = water };
        var axe = new Axe { UpgradeLevel = 2 };
        if (borrow) { liveLoan = axe; liveChest = Store(axe, 18, 18); }
        else Who.Items[1] = axe;
        Who.Items[2] = new Pickaxe { UpgradeLevel = 2 };
        Who.Items[3] = new MeleeWeapon("66");
        Who.Items[4] = new MilkPail(); Who.Items[5] = new Shears();
    }
    private void BuildLiveCases()
    {
        BuildFieldLiveCases();
        liveCases.Enqueue(new("mixed-field", () =>
        {
            LiveTools(borrow: true, water: 6);
            for (int x = 23; x < 35; x++) for (int y = 20; y < 26; y++) liveCrops.Add(CropAt(x, y));
            for (int x = 36; x < 40; x++) for (int y = 20; y < 23; y++) liveRipe.Add(CropAt(x, y, true));
            for (int x = 35; x < 40; x++) Map.terrainFeatures[new(x, 27)] = new Grass(1, 4);
            foreach (var (x, y) in new[] { (26, 19), (32, 26), (38, 25) }) liveDebris.Add(ObjectAt("(O)343", x, y));
            liveTree = new Tree("1", 5); Map.terrainFeatures[new(30, 28)] = liveTree;
            string[] ids = { "(O)93", "(O)599", "(O)621", "(O)645", "(O)463", "(O)464", "(O)746", "(O)326", "(O)461", "(O)922", "(O)923", "(O)924", "(O)925", "(O)927", "(O)929" };
            for (int i = 0; i < ids.Length; i++)
            { var prop = ObjectAt(ids[i], 22 + i, 31); protectedProps[prop.TileLocation] = prop; }
            var machine = ObjectAt("(BC)13", 36, 24); machine.heldObject.Value = ItemRegistry.Create<SObject>("(O)334"); machine.readyForHarvest.Value = true;
            Map.Map.GetLayer("Back").Tiles[19, 26].Properties["WaterSource"] = "T";
            DragWith(SButton.MouseRight, 20, 18, 42, 32);
        }, () => liveCrops.All(c => c.state.Value == 1) && liveRipe.All(c => c.crop is null)
            && liveDebris.All(o => !ReferenceEquals(Map.Objects.GetValueOrDefault(o.TileLocation), o))
            && !Map.terrainFeatures.Values.Contains(liveTree!) && liveChest!.Items.Contains(liveLoan!)
            && Map.terrainFeatures.Values.All(t => t is not Grass) && liveRefills > 0));
        liveCases.Enqueue(new("occluded-plots-and-wall", () =>
        {
            LiveTools(); Config.SmartActions.Remove(ActionKind.WildTree); Config.SmartActions.Remove(ActionKind.TreeStump);
            for (int x = 23; x < 31; x++) for (int y = 20; y < 25; y++) liveCrops.Add(CropAt(x, y));
            for (int x = 33; x < 38; x++) for (int y = 21; y < 25; y++) liveCrops.Add(CropAt(x, y));
            liveTree = new Tree("1", 5); Map.terrainFeatures[new(27, 26)] = liveTree;
            for (int y = 18; y <= 26; y++) if (y != 25)
            { var fence = new Fence(new(32, y), "322", false); Map.Objects[fence.TileLocation] = fence; protectedProps[fence.TileLocation] = fence; }
            foreach (var p in new[] { new Point(24, 23), new Point(28, 22), new Point(35, 23) })
            { var sprinkler = ObjectAt("(O)645", p.X, p.Y); protectedProps[sprinkler.TileLocation] = sprinkler; }
            SmartSelect(Who.Items[0], new(20, 18, 20, 11));
        }, () => liveCrops.All(c => c.state.Value == 1) && Map.terrainFeatures.Values.Contains(liveTree!)));
        liveCases.Enqueue(new("moving-animals-and-crops", () =>
        {
            LiveTools();
            for (int x = 23; x < 29; x++) for (int y = 20; y < 23; y++) liveCrops.Add(CropAt(x, y));
            liveAnimals.Add(CareAnimal("White Cow", 99101, 23, 24, "184"));
            liveAnimals.Add(CareAnimal("Sheep", 99102, 27, 24, "440"));
            liveAnimals.Add(CareAnimal("White Chicken", 99103, 25, 25));
            SmartSelect(Who.Items[0], new(18, 16, 24, 20));
        }, () => liveCrops.All(c => c.state.Value == 1) && liveAnimals.All(a => a.wasPet.Value && a.currentProduce.Value is null)));
        foreach (string tool in new[] { "water", "axe", "pickaxe" })
            liveCases.Enqueue(new("lost-animation-" + tool, () =>
            {
                Tool selected = tool == "water" ? new WateringCan { WaterLeft = 20 } : tool == "axe" ? new Axe() : new Pickaxe();
                if (tool == "water") liveCrops.Add(CropAt(23, 20));
                else if (tool == "axe") Map.terrainFeatures[new(23, 20)] = new Tree("1", 5);
                else ObjectAt("(O)343", 23, 20).MinutesUntilReady = 2;
                Who.Items[0] = selected;
                var forage = ObjectAt("(O)16", 26, 20); forage.isSpawnedObject.Value = true; liveDebris.Add(forage);
                SmartSelect(selected, new(23, 20, 4, 1));
            }, () => liveInjected && liveRecovered && liveMaxAction >= 12000
                && liveDebris.All(o => !ReferenceEquals(Map.Objects.GetValueOrDefault(o.TileLocation), o)), true));
    }
}
