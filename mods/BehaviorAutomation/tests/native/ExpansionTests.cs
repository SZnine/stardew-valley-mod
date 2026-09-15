using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private void ExpansionTests()
    {
        Check("selection dimensions follow capped tiles, stay visible at edges, and can be disabled", () =>
        {
            var drag = new DragSelection { Start = new(20, 20), End = new(25, 23), Mode = ToolMode.Scythe };
            Assert(Overlay.SizeText(drag) is "6 × 4" or "6 x 4", "Wrong tile dimensions");
            var oldView = Game1.viewport;
            try
            {
                Game1.viewport = new(0, 0, 1280, 720);
                foreach (var end in new[] { new Cell(0, 0), new(19, 0), new(0, 11), new(79, 63) })
                {
                    drag.End = end;
                    var bounds = Overlay.SizeBounds(drag, new(0, 0, 1280, 720));
                    Assert(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= 1280 && bounds.Bottom <= 720, "Dimension badge left viewport");
                }
                drag.Start = new(0, 0); drag.End = new(79, 79);
                Assert(Overlay.SizeText(drag) is "64 × 64" or "64 x 64", "Dimensions ignored selection cap");
                using var renderer = new OverlayRenderer();
                using var batch = new Microsoft.Xna.Framework.Graphics.SpriteBatch(Game1.graphics.GraphicsDevice);
                var device = batch.GraphicsDevice;
                var saved = device.GetRenderTargets(); var viewport = device.Viewport;
                try
                {
                    Config.ShowSelectionSize = false;
                    var hidden = renderer.Render(batch, Control, drag, Config);
                    var before = new Color[hidden.Width * hidden.Height]; hidden.GetData(before);
                    Config.ShowSelectionSize = true;
                    var shown = renderer.Render(batch, Control, drag, Config);
                    var after = new Color[shown.Width * shown.Height]; shown.GetData(after);
                    Assert(before.Where((c, i) => c != after[i]).Count() > 100, "Dimension switch did not change rendered pixels");
                }
                finally { device.SetRenderTargets(saved); device.Viewport = viewport; }
                int saves = 0;
                var menu = new ActionMenu(Config, () => saves++, k => k); menu.SetPage(ActionPage.Settings);
                var toggle = menu.SettingButtons.Single(b => b.name == "config.selection-size");
                menu.receiveLeftClick(toggle.bounds.Center.X, toggle.bounds.Center.Y);
                Assert(!Config.ShowSelectionSize && saves == 1, "Dimension switch did not save");
            }
            finally { Game1.viewport = oldView; }
        });
        Check("drag camera extends world selection without moving the farmer and restores follow", () =>
        {
            var oldView = Game1.viewport;
            bool frozen = Game1.viewportFreeze;
            var position = Who.Position;
            var camera = new SelectionCamera();
            try
            {
                Game1.viewportFreeze = false;
                Game1.viewport = new(0, 0, 1280, 720);
                var cursor = (ICursorPosition)Activator.CreateInstance(typeof(Mod).Assembly.GetType("StardewModdingAPI.Framework.CursorPosition")!,
                    new Vector2(1279, 360), new Vector2(1279, 360), new Vector2(19, 5), new Vector2(19, 5))!;
                camera.Begin();
                Cell end = default;
                for (int i = 0; i < 200; i++) end = camera.Update(cursor, Map, Config, 16);
                Assert(Game1.viewport.X > 1200 && end.X >= 38, "Edge drag did not reach beyond the initial screen");
                Assert(Who.Position == position, "Camera pan moved the farmer");
                camera.End();
                Assert(!Game1.viewportFreeze && Game1.forceSnapOnNextViewportUpdate, "Camera follow was not restored");
            }
            finally { camera.End(); Game1.viewport = oldView; Game1.viewportFreeze = frozen; }
        });
        Check("drag camera clamps map edges and respects its disabled switch", () =>
        {
            var oldView = Game1.viewport;
            bool frozen = Game1.viewportFreeze;
            var camera = new SelectionCamera();
            try
            {
                Game1.viewport = new(0, 0, 1280, 720);
                camera.Begin();
                var left = CursorAtTile(0, 0);
                for (int i = 0; i < 20; i++) camera.Update(left, Map, Config, 100);
                Assert(Game1.viewport.X == 0 && Game1.viewport.Y == 0, "Camera crossed the map boundary");
                Config.PanWhileSelecting = false;
                var right = CursorAtTile(19, 5);
                for (int i = 0; i < 50; i++) camera.Update(right, Map, Config, 100);
                Assert(Game1.viewport.X == 0, "Disabled edge pan still moved the camera");
            }
            finally { camera.End(); Game1.viewport = oldView; Game1.viewportFreeze = frozen; }
        });
        Check("F8 settings change new options without a configuration mod", () =>
        {
            int saved = 0;
            var menu = new ActionMenu(Config, () => saved++, k => k);
            menu.SetPage(ActionPage.Settings);
            Assert(menu.PageButtons.Count == 4, "Missing built-in settings controls");
            var button = FindSetting(menu, "config.interiors");
            menu.receiveLeftClick(button.bounds.Center.X, button.bounds.Center.Y);
            var increase = FindSetting(menu, "config.wild-spacing", last: true);
            menu.receiveLeftClick(increase.bounds.Center.X, increase.bounds.Center.Y);
            Assert(!Config.WorkInsideBuildings && Config.WildTreeSpacing == 3 && saved == 2, "Built-in settings failed to save");
        });
        Check("tree spacing uses configured trunk distance and preserves native minimums", () =>
        {
            var config = new ModConfig { WildTreeSpacing = 4, FruitTreeSpacing = 5 };
            var area = new Rectangle(22, 20, 13, 11);
            var cells = Enumerable.Range(area.Left, area.Width).SelectMany(x => Enumerable.Range(area.Top, area.Height).Select(y => new Cell(x, y))).ToHashSet();
            foreach (var cell in cells) Map.Map.GetLayer("Back").Tiles[cell.X, cell.Y].Properties["Type"] = "Dirt";
            foreach (var (id, spacing) in new[] { ("(O)309", 4), ("(O)628", 5) })
            {
                var seed = ItemRegistry.Create<StardewValley.Object>(id);
                var targets = Planting.Scan(Map, seed, ToolMode.TreeSeeds, area, cells, config, Array.Empty<WorkTarget>());
                Assert(targets.Count >= 4, "No tree planting pattern generated");
                Assert(targets.All(a => targets.All(b => ReferenceEquals(a, b) || Math.Max(Math.Abs(a.Origin.X - b.Origin.X), Math.Abs(a.Origin.Y - b.Origin.Y)) >= spacing)), "Configured spacing was ignored");
                var first = targets[0];
                Map.terrainFeatures[first.Origin.Tile] = id == "(O)309" ? new Tree("1", 0) : new FruitTree("628", 0);
                Assert(!Planting.CanPlant(Map, seed, first.Origin.Add(new(spacing - 1, 0)), config), "Execution ignored a newly occupied nearby tree");
                Map.terrainFeatures.Clear();
            }
            config.WildTreeSpacing = 0; config.FruitTreeSpacing = 0; config.Normalize();
            Assert(config.WildTreeSpacing == 2 && config.FruitTreeSpacing == 3, "Native growth minimums lost");
        });
        Check("large floor selection starts nearby instead of its distant first reserved tile", () =>
        {
            Who.Position = new(43 * 64, 21 * 64);
            Config.LeftActions.Clear();
            Who.Items[0] = ItemRegistry.Create("(O)328", 60);
            Control.Board.Select(Map, Who, ToolMode.Place, null, new(20, 20, 25, 2), Config);
            Control.Resume();
            Until(() => Map.terrainFeatures.Values.OfType<Flooring>().Any());
            var first = Map.terrainFeatures.Pairs.First(p => p.Value is Flooring).Key;
            Assert(first.X >= 40, "Walked across the whole rectangle before placing the first floor");
        });
        Check("selected building discovers interior tasks and obeys the toggle and action pools", () =>
        {
            var building = Building.CreateInstanceFromId("Big Coop", new(25, 18));
            building.parentLocationName.Value = "Farm";
            Map.buildings.Add(building);
            building.FinishConstruction(onGameStart: true);
            building.LoadFromBuildingData(building.GetData(), false, true);
            building.load(); building.updateInteriorWarps();
            var room = building.GetIndoors();
            var egg = ItemRegistry.Create<StardewValley.Object>("(O)176");
            egg.TileLocation = new(5, 5); egg.isSpawnedObject.Value = true;
            room.Objects[egg.TileLocation] = egg;
            var axe = new Axe(); Who.Items[0] = axe;
            Control.Board.Select(Map, Who, ToolMode.Axe, axe, new(25, 18, 6, 3), Config);
            Assert(Control.Board.Jobs.Any(t => t.Entity is BuildingDoor), "Selected coop did not expose its interior work");
            var child = Control.Board.ForInterior(room, Who, Config);
            Assert(child.Jobs.Any(t => ReferenceEquals(t.Entity, egg)) && child.Jobs.All(t => t.Entity is not BuildingDoor), "Interior did not inherit pools or started recursive building work");
            Config.WorkInsideBuildings = false;
            Control.Board.Select(Map, Who, ToolMode.Axe, axe, new(25, 18, 6, 3), Config);
            Assert(!Control.Board.HasSelection, "Disabled interiors still entered the queue");
            Config.WorkInsideBuildings = true; Config.LeftActions.Clear();
            Control.Board.Select(Map, Who, ToolMode.Axe, axe, new(25, 18, 6, 3), Config);
            Assert(!Control.Board.HasSelection, "Interior bypassed left action pool");
        });
    }
}
