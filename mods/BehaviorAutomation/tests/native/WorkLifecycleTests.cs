using StardewValley;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private static int blockedGrabberAttempts;
    private static bool BlockGrabber() { blockedGrabberAttempts++; return false; }
    private sealed class IneffectiveRefillCan : WateringCan
    {
        public int Attempts;
        public override void DoFunction(GameLocation location, int x, int y, int power, Farmer who) => Attempts++;
    }

    private void WorkLifecycleTests()
    {
        Check("ineffective auto-grabber interaction stops after three attempts", () =>
        {
            var grabber = ObjectAt("(BC)165", 23, 20);
            var chest = new StardewValley.Objects.Chest();
            chest.Items.Add(ItemRegistry.Create<StardewValley.Object>("(O)176", 4));
            grabber.heldObject.Value = chest;
            var harmony = new HarmonyLib.Harmony("sznine.BehaviorProbe.NoGrabberProgress");
            var method = HarmonyLib.AccessTools.Method(typeof(AnimalCare), nameof(AnimalCare.CollectGrabber));
            blockedGrabberAttempts = 0;
            harmony.Patch(method, prefix: new(typeof(ModEntry), nameof(BlockGrabber)));
            try
            {
                Select(null, 23, 20);
                Until(() => Control.Paused);
                for (int i = 0; i < 600; i++) Frame();
                Assert(Control.State == "no-effect" && blockedGrabberAttempts == 3 && chest.Items[0].Stack == 4
                    && Who.CanMove && !Who.UsingTool, "Collector continued retrying without progress");
            }
            finally { harmony.Unpatch(method, HarmonyLib.HarmonyPatchType.All, harmony.Id); }
        });
        Check("failed refill cannot recreate fresh targets and retry forever", () =>
        {
            var source = Map.Map.GetLayer("Back").Tiles[17, 25];
            source.Properties["WaterSource"] = "T";
            try
            {
                var can = new IneffectiveRefillCan { WaterLeft = 0 };
                var crop = CropAt(23, 20);
                Select(can, 23, 20);
                Until(() => Control.Paused);
                int attempts = can.Attempts;
                for (int i = 0; i < 900; i++)
                    Frame();
                Assert(Control.State == "no-water-source" && attempts is > 0 and <= 3 && can.Attempts == attempts
                    && can.WaterLeft == 0 && crop.state.Value == 0 && Who.CanMove && !Who.UsingTool,
                    $"Unbounded refill: state={Control.State} attempts={can.Attempts} move={Who.CanMove}");
            }
            finally { source.Properties.Remove("WaterSource"); }
        });
        foreach (bool axe in new[] { true, false })
            Check("stalled native tool animation releases locks " + (axe ? "axe" : "pickaxe"), () =>
            {
                Tool tool = axe ? new Axe() : new Pickaxe();
                if (axe)
                    Map.terrainFeatures[new(23, 20)] = new StardewValley.TerrainFeatures.Tree("1", 5);
                else
                    ObjectAt("(O)343", 23, 20).MinutesUntilReady = 2;
                Select(tool, 23, 20);
                Until(() => Control.Active is not null && Who.UsingTool);
                Who.FarmerSprite.StopAnimation();
                Who.canReleaseTool = false;
                for (int i = 0; i < 150; i++)
                    Control.Tick(Who, 100, true);
                Assert(Control.Active is null && Who.CanMove && !Who.UsingTool, "Stalled tool retained control");
            });
        Check("cancelled released operation still recovers its own lock and returns the exact borrowed tool", () =>
        {
            var can = new WateringCan { WaterLeft = 20 };
            var chest = Store(can);
            CropAt(23, 20);
            SmartSelect(null, new(23, 20, 1, 1));
            Until(() => Control.Active is not null && Who.UsingTool);
            Control.Active!.Released = true;
            Who.canReleaseTool = false;
            Who.FarmerSprite.StopAnimation();
            Control.Clear();
            for (int i = 0; i < 150; i++)
                Control.Tick(Who, 100, true);
            Assert(Control.Active is null && Who.CanMove && !Who.UsingTool && chest.Items.Contains(can) && !Who.Items.Contains(can),
                "Cancelled generation lost action ownership or its borrowed tool");
        });
        Check("stalled automation does not release a different currently held tool", () =>
        {
            var can = new WateringCan { WaterLeft = 20 };
            CropAt(23, 20);
            Select(can, 23, 20);
            Until(() => Control.Active is not null && Who.UsingTool);
            Who.Items[1] = new Axe();
            Who.CurrentToolIndex = 1;
            Who.UsingTool = true;
            Who.CanMove = false;
            Who.canReleaseTool = false;
            Who.FarmerSprite.StopAnimation();
            Who.FarmerSprite.PauseForSingleAnimation = true;
            for (int i = 0; i < 150; i++)
                Control.Tick(Who, 100, true);
            Assert(Who.UsingTool && !Who.CanMove && Who.FarmerSprite.PauseForSingleAnimation,
                "Timeout reset a different tool's animation");
        });
        foreach (bool usingFlag in new[] { true, false })
            Check("owned stalled watering releases animation and movement locks using=" + usingFlag, () =>
            {
                var can = new WateringCan { WaterLeft = 20 };
                CropAt(23, 20);
                Select(can, 23, 20);
                Until(() => Control.Active is not null && Who.UsingTool);
                Control.Active!.Released = true;
                Who.canReleaseTool = false;
                // Simulate an interrupted native completion callback, without running another impact.
                Who.FarmerSprite.StopAnimation();
                Who.UsingTool = usingFlag;
                Who.CanMove = false;
                Who.FarmerSprite.PauseForSingleAnimation = true;
                for (int i = 0; i < 150; i++)
                    Control.Tick(Who, 100, true);
                Assert(Control.Active is null && !Who.UsingTool && Who.CanMove && !Who.FarmerSprite.PauseForSingleAnimation,
                    $"Owned locks survived: active={Control.Active is not null} using={Who.UsingTool} move={Who.CanMove}");
                CropAt(24, 20);
                Select(can, 24, 20);
                Until(() => Control.State == "idle");
                Assert(((StardewValley.TerrainFeatures.HoeDirt)Map.terrainFeatures[new(24, 20)]).state.Value == 1,
                    "Recovered controller could not execute the next operation");
            });
        Check("paused game time does not consume an owned animation watchdog", () =>
        {
            var can = new WateringCan { WaterLeft = 20 };
            CropAt(23, 20);
            Select(can, 23, 20);
            Until(() => Control.Active is not null && Who.UsingTool);
            Control.Active!.Released = true;
            Who.canReleaseTool = false;
            var operation = Control.Active;
            double elapsed = operation!.Elapsed;
            for (int i = 0; i < 150; i++)
                Control.Tick(Who, 100, false);
            Assert(ReferenceEquals(Control.Active, operation) && !Control.Paused && operation.Elapsed == elapsed,
                "A menu/background pause was mistaken for a stalled animation");
            Who.UsingTool = false;
            Who.CanMove = true;
            Who.FarmerSprite.PauseForSingleAnimation = false;
            Who.FarmerSprite.StopAnimation();
            Until(() => Control.State == "idle");
        });
        Check("unowned native tool and movement locks are never reset", () =>
        {
            Who.Items[0] = new Axe();
            Who.UsingTool = true;
            Who.CanMove = false;
            Who.FarmerSprite.PauseForSingleAnimation = true;
            for (int i = 0; i < 150; i++)
                Control.Tick(Who, 100, true);
            Assert(Who.UsingTool && !Who.CanMove && Who.FarmerSprite.PauseForSingleAnimation,
                "Manual operation was reset by automation");
        });
    }
}
