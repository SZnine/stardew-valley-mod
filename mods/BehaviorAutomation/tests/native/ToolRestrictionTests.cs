using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
using xTile.Tiles;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private void ToolRestrictionTests()
    {
        var observations = new List<object>();
        foreach (var (id, minimum) in new[] { (600, 1), (602, 2), (672, 2), (148, 3), (622, 3), (752, 0), (754, 0), (756, 0), (758, 0) })
            Check($"native manual versus automated clump {id} at all five tool grades", () =>
            {
                for (int level = 0; level <= 4; level++)
                {
                    Reset();
                    Tool tool = id is 600 or 602 ? new Axe { UpgradeLevel = level } : new Pickaxe { UpgradeLevel = level };
                    Who.Items[0] = tool;
                    var manual = new ResourceClump(id, 2, 2, new(23, 20));
                    Map.resourceClumps.Add(manual);
                    float health = manual.health.Value;
                    tool.swingTicker++;
                    tool.DoFunction(Map, 23 * 64 + 32, 20 * 64 + 32, 1, Who);
                    float manualDamage = health - manual.health.Value;
                    Assert((manualDamage > 0) == (level >= minimum), $"Native restriction differs: id={id}, grade={level}, damage={manualDamage}");
                    Map.resourceClumps.Clear();
                    Game1.activeClickableMenu = null;
                    Game1.dialogueUp = false;
                    Who.forceCanMove();
                    Who.Stamina = 1000;
                    var automated = new ResourceClump(id, 2, 2, new(23, 20));
                    Map.resourceClumps.Add(automated);
                    Select(tool, 23, 20, 2, 2);
                    Until(() => automated.health.Value < health || Control.State == "idle");
                    float autoDamage = health - automated.health.Value;
                    Assert(autoDamage == manualDamage, $"Automatic grade bypass or different damage: id={id}, grade={level}, manual={manualDamage}, auto={autoDamage}");
                    Assert(tool.UpgradeLevel == level, "Automation changed tool grade");
                    if (level < minimum)
                        Assert(Who.Stamina == 1000 && Map.resourceClumps.Contains(automated), "Insufficient tool spent stamina or removed target");
                    observations.Add(new { id, level, minimum, manualDamage, autoDamage });
                }
            });

        foreach (bool smart in new[] { false, true })
            Check($"insufficient copper axe and pickaxe cannot use unavailable stored upgrades ({(smart ? "right" : "left")})", () =>
            {
                foreach (bool useAxe in new[] { true, false })
                {
                    Reset();
                    Tool held = useAxe ? new Axe { UpgradeLevel = 1 } : new Pickaxe { UpgradeLevel = 1 };
                    Tool stored = useAxe ? new Axe { UpgradeLevel = 4 } : new Pickaxe { UpgradeLevel = 4 };
                    var chest = Store(stored);
                    Config.UseStoredTools = false;
                    var clump = new ResourceClump(useAxe ? 602 : 672, 2, 2, new(23, 20));
                    Map.resourceClumps.Add(clump);
                    float health = clump.health.Value;
                    Who.Items[0] = held;
                    if (smart) SmartSelect(held, new(23, 20, 2, 2));
                    else Select(held, 23, 20, 2, 2);
                    Until(() => Control.State == "idle");
                    Assert(clump.health.Value == health && Who.Stamina == 1000 && chest.Items.Contains(stored), "Unavailable stored upgrade was used");
                }
            });

        Check("left basic clearance cannot borrow a higher grade axe for a blocking log", () =>
        {
            var copper = new Axe { UpgradeLevel = 1 };
            var steel = new Axe { UpgradeLevel = 2 };
            var chest = Store(steel);
            Config.LeftActions.Clear();
            var log = new ResourceClump(602, 2, 2, new(22, 20));
            Map.resourceClumps.Add(log);
            float health = log.health.Value;
            var twig = ObjectAt("(O)294", 26, 20);
            var wall = Map.Map.GetLayer("Buildings");
            var sheet = Map.Map.GetLayer("Back").Tiles[22, 0].TileSheet;
            for (int y = 0; y < wall.LayerHeight; y++)
                if (y is not (20 or 21)) wall.Tiles[22, y] = new StaticTile(wall, sheet, BlendMode.Alpha, 0);
            try
            {
                Select(copper, 26, 20);
                Until(() => Control.State == "idle" || Control.Paused);
                Assert(Map.resourceClumps.Contains(log) && log.health.Value == health && Map.Objects.ContainsKey(twig.TileLocation), "Left clearance silently upgraded the selected axe");
                Assert(Who.Stamina == 1000 && chest.Items.Contains(steel) && copper.UpgradeLevel == 1, "Unavailable held action borrowed or spent resources");
            }
            finally
            {
                for (int y = 0; y < wall.LayerHeight; y++) wall.Tiles[22, y] = null;
            }
        });

        Check("native axe additional power works without changing its permanent grade", () =>
        {
            var axe = new Axe();
            axe.additionalPower.Value = 2; // Native Powerful enchantment's axe effect.
            Who.Items[0] = axe;
            var log = new ResourceClump(602, 2, 2, new(23, 20));
            Map.resourceClumps.Add(log);
            float health = log.health.Value;
            axe.swingTicker++;
            axe.DoFunction(Map, 23 * 64 + 32, 20 * 64 + 32, 1, Who);
            float damage = health - log.health.Value;
            Assert(damage > 0 && axe.UpgradeLevel == 0, "Native additional power fixture failed");
            Map.resourceClumps.Clear();
            var automated = new ResourceClump(602, 2, 2, new(23, 20));
            Map.resourceClumps.Add(automated);
            Select(axe, 23, 20, 2, 2);
            Until(() => automated.health.Value < health || Control.State == "idle");
            Assert(health - automated.health.Value == damage && axe.UpgradeLevel == 0 && axe.additionalPower.Value == 2, "Native axe enchantment was blocked or applied twice");
        });
        Check("pickaxe additional power does not unlock farm boulders or meteorites", () =>
        {
            foreach (int id in new[] { 672, 622 })
            {
                Reset();
                var pick = new Pickaxe { UpgradeLevel = 1 };
                pick.additionalPower.Value = 2;
                Who.Items[0] = pick;
                var clump = new ResourceClump(id, 2, 2, new(23, 20));
                Map.resourceClumps.Add(clump);
                float health = clump.health.Value;
                pick.swingTicker++;
                pick.DoFunction(Map, 23 * 64 + 32, 20 * 64 + 32, 1, Who);
                Assert(clump.health.Value == health, "Native pickaxe gate changed");
                Game1.activeClickableMenu = null;
                Game1.dialogueUp = false;
                Who.forceCanMove();
                Who.Stamina = 1000;
                SmartSelect(pick, new(23, 20, 2, 2));
                Until(() => Control.State == "idle");
                Assert(clump.health.Value == health && Who.Stamina == 1000 && pick.UpgradeLevel == 1, "Pickaxe damage bonus was treated as an upgrade");
            }
        });
        Check("left selected copper axe never substitutes a stored steel axe for its selected log", () =>
        {
            var copper = new Axe { UpgradeLevel = 1 };
            var steel = new Axe { UpgradeLevel = 2 };
            var chest = Store(steel);
            Config.LeftActions.Add(ActionKind.LargeWood);
            var log = new ResourceClump(602, 2, 2, new(23, 20));
            Map.resourceClumps.Add(log);
            float health = log.health.Value;
            Select(copper, 23, 20, 2, 2);
            Until(() => Control.State == "idle");
            Assert(log.health.Value == health && Who.Stamina == 1000 && chest.Items.Contains(steel), "Held task was upgraded through extras");
        });
        foreach (bool smart in new[] { true, false })
            Check($"{(smart ? "right smart" : "explicit left extra")} work borrows a real steel axe and returns it unchanged", () =>
            {
                var steel = new Axe { UpgradeLevel = 2 };
                steel.modData["fixture"] = "preserve";
                var chest = Store(steel);
                var log = new ResourceClump(602, 2, 2, new(23, 20));
                Map.resourceClumps.Add(log);
                Who.Items[0] = new Axe { UpgradeLevel = 1 };
                if (smart) SmartSelect(Who.Items[0], new(23, 20, 2, 2));
                else
                {
                    Config.Actions.Remove(ActionKind.LargeWood);
                    Config.LeftActions.Add(ActionKind.LargeWood);
                    Select((Tool)Who.Items[0], 23, 20, 2, 2);
                }
                Assert(ReferenceEquals(Control.Board.Jobs.Single().Tool, steel), "Authorized smart work failed to choose the available steel axe");
                Until(() => Control.Active is not null);
                Assert(ReferenceEquals(Who.CurrentTool, steel) && !chest.Items.Contains(steel), "Action did not use the real borrowed tool");
                Until(() => Control.State == "idle");
                Assert(!Map.resourceClumps.Contains(log) && chest.Items.Contains(steel) && steel.UpgradeLevel == 2
                    && steel.modData["fixture"] == "preserve" && Who.CurrentTool?.UpgradeLevel == 1 && Map.debris.Count > 0, "Native drops, original tools or loan return changed");
            });
        foreach (bool smart in new[] { false, true })
            Check($"{(smart ? "right smart" : "left held")} clearance retains valid native tools", () =>
            {
                var copper = new Axe { UpgradeLevel = 1 };
                var steel = new Axe { UpgradeLevel = 2 };
                var chest = Store(steel);
                Who.Items[0] = copper;
                Config.LeftActions.Clear();
                var blocker = new ResourceClump(smart ? 602 : 600, 2, 2, new(22, 20));
                Map.resourceClumps.Add(blocker);
                var twig = ObjectAt("(O)294", 26, 20);
                var wall = Map.Map.GetLayer("Buildings");
                var sheet = Map.Map.GetLayer("Back").Tiles[22, 0].TileSheet;
                for (int y = 0; y < wall.LayerHeight; y++)
                    if (y is not (20 or 21)) wall.Tiles[22, y] = new StaticTile(wall, sheet, BlendMode.Alpha, 0);
                try
                {
                    if (smart) SmartSelect(copper, new(26, 20, 1, 1));
                    else Select(copper, 26, 20);
                    Until(() => Control.Active is not null || Control.State == "idle");
                    Assert(ReferenceEquals(Control.Active?.Approach.Target.Tool, smart ? steel : copper), "Clearance selected the wrong actual tool");
                    Until(() => Control.State == "idle");
                    Assert(!Map.resourceClumps.Contains(blocker) && !Map.Objects.ContainsKey(twig.TileLocation) && chest.Items.Contains(steel), "Allowed clearance or follow-up work stopped functioning");
                }
                finally
                {
                    for (int y = 0; y < wall.LayerHeight; y++) wall.Tiles[22, y] = null;
                }
            });
        Check("tool downgraded after planning is rechecked before the native swing", () =>
        {
            var axe = new Axe { UpgradeLevel = 2 };
            var log = new ResourceClump(602, 2, 2, new(23, 20));
            Map.resourceClumps.Add(log);
            Select(axe, 23, 20, 2, 2);
            Until(() => Control.Active is not null);
            float health = log.health.Value;
            float stamina = Who.Stamina;
            axe.UpgradeLevel = 1;
            Until(() => Control.State == "idle" || Control.Paused);
            Assert(log.health.Value == health && Who.Stamina == stamina && axe.UpgradeLevel == 1, "Stale plan bypassed the downgraded tool gate");
        });
        File.WriteAllText(Path.Combine(Evidence, "tool-grade-matrix.json"), System.Text.Json.JsonSerializer.Serialize(observations, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
