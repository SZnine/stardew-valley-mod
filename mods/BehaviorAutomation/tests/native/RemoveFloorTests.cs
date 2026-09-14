using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private void RemoveFloorTests()
    {
        Check("held axe and pickaxe remove selected floors and return native materials", () =>
        {
            foreach (Tool tool in new Tool[] { new Axe(), new Pickaxe() })
            {
                Reset();
                Config.LeftActions.Clear();
                var floor = new Flooring("0");
                string expected = ItemRegistry.QualifyItemId(floor.GetData().ItemId);
                Map.terrainFeatures[new(24, 20)] = floor;
                Map.terrainFeatures[new(25, 20)] = new Flooring("1");
                Map.terrainFeatures[new(26, 20)] = new Flooring("0");
                SelectModern(tool, new(24, 20, 2, 1));
                Assert(Control.Board.Jobs.Count == 2 && Control.Board.Jobs.All(t => t.Kind == ActionKind.RemoveFloor && t.Scope == WorkScope.Held), "Wrong floor scope");
                Until(() => Control.State == "idle");
                Assert(!Map.terrainFeatures.ContainsKey(new(24, 20)) && !Map.terrainFeatures.ContainsKey(new(25, 20)) && Map.terrainFeatures.ContainsKey(new(26, 20)), "Floor removal escaped selected region");
                Assert(Map.debris.Any(d => d.item?.QualifiedItemId == expected) || Who.Items.Any(i => i?.QualifiedItemId == expected), "Native floor material not returned");
            }
        });
        Check("floor removal stays held-only and honors its basic switch", () =>
        {
            Config.LeftActions.UnionWith(Enum.GetValues<ActionKind>());
            Config.SmartActions.UnionWith(Enum.GetValues<ActionKind>());
            Config.Normalize();
            Assert(!Config.LeftActions.Contains(ActionKind.RemoveFloor) && !Config.SmartActions.Contains(ActionKind.RemoveFloor), "Removal entered automatic pools");
            Map.terrainFeatures[new(24, 20)] = new Flooring("0");
            Who.Items[1] = new Pickaxe();
            SmartSelect(Who.Items[1], new(24, 20, 1, 1));
            Assert(!Control.Board.HasSelection, "Right selected flooring");
            SelectModern(null, new(24, 20, 1, 1));
            Assert(!Control.Board.HasSelection, "Empty hand inferred floor removal");
            Config.Actions.Remove(ActionKind.RemoveFloor);
            SelectModern((Tool)Who.Items[1], new(24, 20, 1, 1));
            Assert(!Control.Board.HasSelection, "Disabled removal selected floor");
        });
        Check("ordinary axe actions preserve unselected flooring beneath objects", () =>
        {
            Config.Actions.Remove(ActionKind.RemoveFloor);
            var floor = new Flooring("0");
            Map.terrainFeatures[new(24, 20)] = floor;
            ObjectAt("(O)294", 24, 20);
            Select(new Axe(), 24, 20);
            Until(() => Control.State == "idle");
            Assert(!Map.Objects.ContainsKey(new(24, 20)) && ReferenceEquals(Map.terrainFeatures.GetValueOrDefault(new(24, 20)), floor), "Axe action incidentally removed flooring");
        });
    }
}
