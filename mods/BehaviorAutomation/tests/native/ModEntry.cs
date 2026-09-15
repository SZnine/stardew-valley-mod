using System.Reflection;
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
using StardewModdingAPI.Utilities;
using xTile.Tiles;
using BehaviorMod = Sznine.BehaviorAutomation.ModEntry;
using SObject = StardewValley.Object;

namespace BehaviorProbe;
public sealed partial class ModEntry : Mod
{
    private int ticks; private readonly List<string> results = new(); private readonly List<string> notices = new();
    private BehaviorMod Behavior => Installed<BehaviorMod>("sznine.BehaviorAutomation");
    private WorkController Control => Behavior.Controller;
    private ModConfig Config => (ModConfig)AccessTools.Field(typeof(BehaviorMod), "config").GetValue(Behavior)!;
    private string Evidence => Path.GetFullPath(Path.Combine(Helper.DirectoryPath, "../../evidence"));
    private static Farmer Who => Game1.player; private static GameLocation Map => Game1.currentLocation;
    private static readonly MethodInfo Animate = AccessTools.Method(typeof(FarmerSprite), "animateOnce", new[] { typeof(GameTime) });
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            if (++ticks != 100)
                return;
            Directory.CreateDirectory(Evidence);
            try
            {
                Fixture(Test);
            }
            catch (Exception ex) { results.Add("FAIL fixture " + ex); }
            File.WriteAllLines(Path.Combine(Evidence, "native-tests.txt"), results);
            File.WriteAllText(Path.Combine(Evidence, "native-result.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Version = Behavior.ModManifest.Version.ToString(),
                ProductSHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(BehaviorMod).Assembly.Location))),
                Passed = results.Count(r => r.StartsWith("PASS ")),
                Failed = results.Count(r => r.StartsWith("FAIL ")),
                Skipped = results.Count(r => r.StartsWith("SKIP ")),
                CompletedUtc = DateTime.UtcNow,
                ResultsSHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Evidence, "native-tests.txt"))))
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            foreach (var line in results)
                Monitor.Log(line, line.StartsWith("FAIL") ? LogLevel.Error : LogLevel.Info);
            GameRunner.instance.Exit();
        };
    }
    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
    private void Check(string title, Action action)
    {
        try
        {
            Reset();
            action();
            results.Add("PASS " + title);
        }
        catch (Exception ex) { results.Add("FAIL " + title + ": " + ex); }
    }
    private void Fixture(Action tests)
    {
        Assert(!Context.IsWorldReady, "Must not load any player save");
        var oldMap = Map;
        var oldWho = Who;
        var oldMenu = Game1.activeClickableMenu;
        var oldView = Game1.viewport;
        int oldTime = Game1.timeOfDay;
        int oldDay = Game1.dayOfMonth;
        var farm = new Farm("Maps/Farm", "Farm");
        var farmer = new Farmer(new FarmerSprite("Characters/Farmer/farmer_base"), new(20 * 64, 20 * 64), 2, "Behavior fixture", new List<Item>(), true);
        try
        {
            typeof(Context).GetProperty(nameof(Context.IsWorldReady))!.SetValue(null, true);
            AccessTools.Field(typeof(Game1), "_player").SetValue(null, farmer);
            Game1.currentLocation = farm;
            farmer.currentLocation = farm;
            Game1.locations.Add(farm);
            Game1.activeClickableMenu = null;
            Game1.timeOfDay = 900;
            Game1.dayOfMonth = 1;
            Game1.viewport = new(15 * 64, 15 * 64, 1280, 720);
            farmer.MaxItems = 36;
            farmer.Items.Clear();
            for (int i = 0; i < 36; i++)
                farmer.Items.Add(null!);
            farmer.maxStamina.Value = 1000;
            farmer.Stamina = 1000;
            var sheet = new TileSheet("behavior-test", farm.Map, "Maps/spring_outdoorsTileSheet", new(16, 32), new(16, 16));
            farm.Map.AddTileSheet(sheet);
            foreach (var layer in farm.Map.Layers)
                for (int x = 0; x < layer.LayerWidth; x++)
                    for (int y = 0; y < layer.LayerHeight; y++)
                        layer.Tiles[x, y] = layer.Id == "Back" ? new StaticTile(layer, sheet, BlendMode.Alpha, 0) : null;
            farm.waterTiles = null;
            farm.resourceClumps.Clear();
            farm.buildings.Clear();
            farm.largeTerrainFeatures.Clear();
            farm.warps.Clear();
            tests();
        }
        finally
        {
            Control.Clear();
            Game1.locations.Remove(farm);
            AccessTools.Field(typeof(Game1), "_player").SetValue(null, oldWho);
            Game1.currentLocation = oldMap;
            Game1.activeClickableMenu = oldMenu;
            Game1.viewport = oldView;
            Game1.timeOfDay = oldTime;
            Game1.dayOfMonth = oldDay;
            typeof(Context).GetProperty(nameof(Context.IsWorldReady))!.SetValue(null, false);
        }
    }
    private void Reset()
    {
        // Only this temporary farmer is reset; no player save is loaded or written.
        Control.Clear();
        AccessTools.Property(typeof(WorkController), "Active").SetValue(Control, null);
        Control.Editing = false;
        Who.forceCanMove();
        Who.FarmerSprite.StopAnimation();
        Who.controller = null;
        Who.Position = new(20 * 64, 20 * 64);
        Who.Stamina = 1000;
        Who.farmingLevel.Value = 0;
        Who.miningLevel.Value = 0;
        Who.foragingLevel.Value = 0;
        Who.toolPower.Value = 0;
        Who.toolHold.Value = 0;
        Who.CurrentToolIndex = 0;
        Who.isEating = false;
        Who.freezePause = 0;
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.timeOfDay = 900;
        for (int i = 0; i < 36; i++)
            Who.Items[i] = null!;
        Map.terrainFeatures.Clear();
        Map.Objects.Clear();
        Map.animals.Clear();
        Map.characters.Clear();
        Map.buildings.Clear();
        Map.resourceClumps.Clear();
        Map.largeTerrainFeatures.Clear();
        Map.debris.Clear();
        Config.SelectKey = KeybindList.Parse("LeftShift, RightShift");
        Config.CancelKey = KeybindList.Parse("LeftShift, RightShift");
        Config.ActionMenuKey = KeybindList.Parse("F8");
        Config.Actions = new ModConfig().Actions;
        Config.SmartActions = new ModConfig().SmartActions;
        Config.LeftActions = new ModConfig().LeftActions;
        Config.ReserveStamina = 10;
        Config.Enabled = true;
        Config.UseStoredTools = true;
        Config.MovementCancelSeconds = .5f;
        Config.AllowDiagonalMovement = true;
        Config.ScytheSearchTiles = 12;
        Config.ScytheSwingCost = 16;
        Config.AutoRefillWateringCan = true;
        Config.ClearObstacles = true;
        Config.RefreshIntervalSeconds = .3f;
        Config.CompletionDelaySeconds = 1.5f;
        notices.Clear();
        Who.addedSpeed = 0;
        Who.temporarySpeedBuff = 0;
        Who.stopJittering();

        foreach (string n in new[] { "beginUsingToolEvent", "endUsingToolEvent" })
        {
            var ev = AccessTools.Field(typeof(Farmer), n).GetValue(Who)!;
            AccessTools.Method(ev.GetType(), "Clear").Invoke(ev, null);
        }
        SetButtons(CursorAtUi(Vector2.Zero));
    }
    private HoeDirt CropAt(int x, int y, bool ripe = false, string seed = "472")
    {
        var soil = new HoeDirt(0, Map) { crop = new Crop(seed, x, y, Map) };
        if (ripe)
            soil.crop!.currentPhase.Value = soil.crop.phaseDays.Count - 1;
        Map.terrainFeatures[new(x, y)] = soil;
        return soil;
    }
    private SObject ObjectAt(string id, int x, int y)
    {
        var obj = ItemRegistry.Create<SObject>(id);
        obj.TileLocation = new(x, y);
        Map.Objects[new(x, y)] = obj;
        return obj;
    }
    private void Select(Tool? tool, int x, int y, int width = 1, int height = 1)
    {
        if (tool is not null && !Who.Items.Contains(tool))
        {
            int slot = Enumerable.Range(0, 36).First(i => Who.Items[i] is null);
            Who.Items[slot] = tool;
        }
        Who.CurrentToolIndex = tool is null ? Enumerable.Range(0, 36).First(i => Who.Items[i] is null) : Who.Items.IndexOf(tool);
        var mode = ModeInfo.From(Who.CurrentItem);
        Assert(mode is not null, "Unsupported selection");
        Control.Board.Select(Map, Who, mode!.Value, tool, new(x, y, width, height), Config);
        Control.Resume();
    }
    private void Frame(bool eligible = true, int milliseconds = 16)
    {
        var time = new GameTime(TimeSpan.FromMilliseconds(++ticks * milliseconds), TimeSpan.FromMilliseconds(milliseconds));
        Game1.currentGameTime = time;
        Control.Tick(Who, milliseconds, eligible);
        foreach (string n in new[] { "beginUsingToolEvent", "endUsingToolEvent" })
        {
            var ev = AccessTools.Field(typeof(Farmer), n).GetValue(Who)!;
            AccessTools.Method(ev.GetType(), "Poll").Invoke(ev, null);
        }
        if (Who.controller is { } path && path.update(time))
            Who.controller = null;
        if (Who.UsingTool && !Who.canReleaseTool)
            Animate.Invoke(Who.FarmerSprite, new object[] { time });
        else
            Who.FarmerSprite.checkForSingleAnimation(time);
        Who.CurrentTool?.tickUpdate(time, Who);
        Who.updateMovementAnimation(time);
        foreach (var pair in Map.terrainFeatures.Pairs.ToArray())
            if (pair.Value.tickUpdate(time))
                Map.terrainFeatures.Remove(pair.Key);
    }
    private void Until(Func<bool> done, int limit = 10000)
    {
        for (int i = 0; i < limit; i++)
        {
            Frame();
            if (done())
                return;
        }
        throw new Exception($"Timeout state={Control.State} jobs={Control.Board.Jobs.Count} pos={Who.Position} tile={Cell.Of(Who)} tool={Who.CurrentTool?.Name} using={Who.UsingTool} release={Who.canReleaseTool} active={Control.Active?.Elapsed} sprite={Who.FarmerSprite.CurrentFrame}/{Who.FarmerSprite.PauseForSingleAnimation}");
    }
    private void Finished() => Until(() => Control.Board.Jobs.Count == 0 && Control.Active is null);
    private void Test()
    {
        CoreTests();
        ToolRestrictionTests();
        RoutingRegression();
        PlacementTests();
        RemoveFloorTests();
        WalkingInputTests();
        LeftWhitelistTests();
        WaterTests();
        MenuTests();
        MovementTests();
        PreservedInteractionTests();
        SweepTests();
        LeftCareTests();
        PassabilityTests();
    }
}
