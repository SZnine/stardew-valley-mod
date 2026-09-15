using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace Sznine.BehaviorAutomation;

/// <summary>Temporarily owns the camera while dragging; cursor coordinates stay in world tiles.</summary>
public sealed class SelectionCamera
{
    private bool active, wasFrozen;
    private Vector2 position;
    private Vector2 originalPosition;
    public bool Active => active;

    public void Begin()
    {
        if (active) return;
        active = true;
        wasFrozen = Game1.viewportFreeze;
        position = new(Game1.viewport.X, Game1.viewport.Y);
        originalPosition = position;
    }

    public Cell Update(ICursorPosition cursor, GameLocation map, ModConfig config, double milliseconds)
    {
        if (!active) Begin();
        var grid = map.Map;
        if (grid is null) return Cell.At(cursor.Tile);
        if (config.PanWhileSelecting)
        {
            Game1.viewportFreeze = true;
            Game1.forceSnapOnNextViewportUpdate = false;
            var screen = cursor.ScreenPixels; // SMAPI already adjusts this coordinate for game zoom.
            float edge = 40 / Math.Max(.5f, Game1.options.zoomLevel);
            static float Axis(float p, float size, float band) => p < band ? -Math.Clamp((band - p) / band, 0, 1)
                : p > size - band ? Math.Clamp((p - size + band) / band, 0, 1) : 0;
            var direction = new Vector2(Axis(screen.X, Game1.viewport.Width, edge), Axis(screen.Y, Game1.viewport.Height, edge));
            if (direction.LengthSquared() > 1) direction.Normalize();
            position += direction * config.SelectionPanSpeed * 64 * (float)(Math.Clamp(milliseconds, 0, 100) / 1000d);
            position.X = grid.DisplayWidth > Game1.viewport.Width ? Math.Clamp(position.X, 0, grid.DisplayWidth - Game1.viewport.Width) : originalPosition.X;
            position.Y = grid.DisplayHeight > Game1.viewport.Height ? Math.Clamp(position.Y, 0, grid.DisplayHeight - Game1.viewport.Height) : originalPosition.Y;
            Game1.viewport.X = (int)position.X;
            Game1.viewport.Y = (int)position.Y;
        }
        var world = cursor.ScreenPixels + new Vector2(Game1.viewport.X, Game1.viewport.Y);
        return new(Math.Clamp((int)Math.Floor(world.X / 64), 0, grid.Layers[0].LayerWidth - 1),
            Math.Clamp((int)Math.Floor(world.Y / 64), 0, grid.Layers[0].LayerHeight - 1));
    }

    public void End()
    {
        if (!active) return;
        active = false;
        Game1.viewportFreeze = wasFrozen;
        if (wasFrozen) { Game1.viewport.X = (int)originalPosition.X; Game1.viewport.Y = (int)originalPosition.Y; }
        if (!wasFrozen) Game1.forceSnapOnNextViewportUpdate = true;
    }
}
