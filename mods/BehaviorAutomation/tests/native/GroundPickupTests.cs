using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
using SObject = StardewValley.Object;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private void GroundPickupTests()
    {
        Check("native placed props never become ground pickup in any selection scope", () =>
        {
            // Includes normal decorations and legacy data entries which content packs may place.
            foreach (string id in new[] { "93", "94", "463", "464", "746", "326", "461", "922", "923", "924", "925", "927", "929" })
            {
                Reset();
                var stock = ItemRegistry.Create<SObject>("(O)" + id);
                Assert(stock.placementAction(Map, 24 * 64, 20 * 64, Who), "Native placement failed: " + id);
                var placed = Map.Objects[new(24, 20)];
                var area = new Rectangle(24, 20, 1, 1);
                Assert(placed.canBeGrabbed.Value, "Fixture no longer covers misleading default: " + id);
                foreach (var mode in new[] { ToolMode.Hand, ToolMode.Scythe })
                    Assert(WorldTargets.Scan(Map, Who, mode, null, area, Config).Count == 0, "Placed prop selected: " + id + " / " + mode);
                Assert(SmartSelection.Scan(Map, Who, ToolMode.Hand, null, area, Config).Count == 0, "Right smart selected prop: " + id);
                Assert(SmartSelection.ScanLeft(Map, Who, null, area, Config).Count == 0, "Left extras selected prop: " + id);
                var stale = new WorkTarget { Entity = placed, Origin = new(24, 20), Area = area, Mode = ToolMode.Hand, Kind = ActionKind.Forage };
                Assert(!WorldTargets.Pending(Map, stale, Config), "Stale forage job survived recheck: " + id);
            }
        });
        Check("forage category and CanBeGrabbed alone do not prove native pickup", () =>
        {
            var obj = ObjectAt("(O)16", 24, 20);
            Assert(obj.isForage() && obj.canBeGrabbed.Value && !obj.isSpawnedObject.Value, "Misleading flags fixture invalid");
            Assert(!Map.checkAction(new(24, 20), Game1.viewport, Who) && Map.Objects.ContainsKey(new(24, 20)), "Native game unexpectedly picked up unspawned item");
            Assert(WorldTargets.Scan(Map, Who, ToolMode.Hand, null, new(24, 20, 1, 1), Config).Count == 0, "Unspawned forage category selected");
            obj.isSpawnedObject.Value = true;
            obj.canBeGrabbed.Value = false;
            Select(null, 24, 20);
            Finished();
            Assert(!Map.Objects.ContainsKey(new(24, 20)) && Who.Items.Any(i => i?.QualifiedItemId == "(O)16"), "Actual spawned pickup was rejected by CanBeGrabbed");
        });
        Check("native device interaction takes precedence even if Spawned is set", () =>
        {
            foreach (SObject obj in new SObject[] { ItemRegistry.Create<SObject>("(O)463"), new CrabPot() })
            {
                Reset();
                obj.TileLocation = new(24, 20);
                obj.isSpawnedObject.Value = true;
                Map.Objects[new(24, 20)] = obj;
                foreach (var mode in new[] { ToolMode.Hand, ToolMode.Scythe })
                    Assert(WorldTargets.Scan(Map, Who, mode, null, new(24, 20, 1, 1), Config).Count == 0, "Device entered native pickup branch: " + obj.Type);
            }
        });
        Check("native spawned forage and animal produce still complete", () =>
        {
            foreach (string id in new[] { "16", "78", "176", "418", "430" })
            {
                Reset();
                var obj = ObjectAt("(O)" + id, 24, 20);
                obj.isSpawnedObject.Value = true;
                Select(null, 24, 20);
                Assert(Control.Board.Jobs.Single().Kind == ActionKind.Forage, "Missing spawned pickup: " + id);
                Finished();
                Assert(!Map.Objects.ContainsKey(new(24, 20)) && Who.Items.Any(i => i?.QualifiedItemId == "(O)" + id), "Native pickup failed: " + id);
            }
        });
        Check("ground pickup classification does not suppress ready machine output", () =>
        {
            var furnace = ObjectAt("(BC)13", 24, 20);
            furnace.heldObject.Value = ItemRegistry.Create<SObject>("(O)334");
            furnace.readyForHarvest.Value = true;
            Select(null, 24, 20);
            Assert(Control.Board.Jobs.Single().Kind == ActionKind.Machine, "Ready machine lost its action");
            Finished();
            Assert(ReferenceEquals(Map.Objects[new(24, 20)], furnace) && furnace.heldObject.Value is null
                && Who.Items.Any(i => i?.QualifiedItemId == "(O)334"), "Machine output failed or machine was removed");
        });
        Check("mixed watering and gathering completes beside a placed torch", () =>
        {
            var stock = ItemRegistry.Create<SObject>("(O)93");
            Assert(stock.placementAction(Map, 23 * 64, 20 * 64, Who), "Torch placement failed");
            var torch = Map.Objects[new(23, 20)];
            var soil = CropAt(24, 20);
            ObjectAt("(O)16", 25, 20).isSpawnedObject.Value = true;
            var can = new WateringCan { WaterLeft = 40 };
            SmartSelect(can, new(22, 19, 5, 3));
            Assert(Control.Board.Jobs.Count == 2 && Control.Board.Jobs.All(t => !ReferenceEquals(t.Entity, torch)), "Torch entered mixed work selection");
            Until(() => Control.State == "idle" || Control.Paused);
            Assert(Control.State == "idle" && !Control.Paused && soil.state.Value == 1
                && !Map.Objects.ContainsKey(new(25, 20)) && ReferenceEquals(Map.Objects[new(23, 20)], torch), "Mixed work stalled or damaged placed torch");
        });
    }
}
