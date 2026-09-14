using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;

namespace Sznine.BehaviorAutomation;

public sealed class OverlayRenderer : IDisposable
{
    private RenderTarget2D? surface;
    private void Resize(GraphicsDevice device, int width, int height)
    {
        if (surface is not null && !surface.IsDisposed && surface.Width == width && surface.Height == height)
            return;
        Dispose();
        surface = new(device, width, height);
    }
    // Caller supplies a closed batch. Only selection previews and the working icon are rendered.
    public Texture2D Render(SpriteBatch batch, WorkController control, DragSelection? drag, ModConfig config, Cell? hover = null)
    {
        var device = batch.GraphicsDevice;
        Resize(device, Game1.viewport.Width, Game1.viewport.Height);
        device.SetRenderTarget(surface);
        device.Clear(Color.Transparent);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        try
        {
            Overlay.DrawWorld(batch, control, drag, config, hover);
        }
        finally { batch.End(); }
        return surface!;
    }
    public static Rectangle UiBounds(int width, int height, float zoom, float uiScale) => new(0, 0, (int)Math.Round(width * zoom / uiScale), (int)Math.Round(height * zoom / uiScale));
    public void DrawTop(SpriteBatch batch, WorkController control, DragSelection? drag, ModConfig config, Cell? hover = null)
    {
        var device = batch.GraphicsDevice;
        var saved = device.GetRenderTargets();
        var viewport = device.Viewport;
        var scissor = device.ScissorRectangle;
        var uiViewport = Game1.uiViewport;
        var nonUi = Game1.nonUIRenderTarget;
        bool pushed = false, opened = false;
        batch.End();
        try
        {
            var texture = Render(batch, control, drag, config, hover);
            var bounds = UiBounds(texture.Width, texture.Height, Game1.options.zoomLevel, Game1.options.uiScale);
            device.SetRenderTargets(saved);
            device.Viewport = viewport;
            device.ScissorRectangle = scissor;
            // The UI buffer is composited after the world. Sorted tree sprites cannot cover this pass.
            Game1.PushUIMode();
            pushed = true;
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            opened = true;
            batch.Draw(texture, bounds, Color.White);
        }
        finally
        {
            if (opened)
                batch.End();
            if (pushed)
                Game1.PopUIMode();
            device.SetRenderTargets(saved);
            device.Viewport = viewport;
            device.ScissorRectangle = scissor;
            Game1.uiViewport = uiViewport;
            Game1.nonUIRenderTarget = nonUi;
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }
    }
    public void Dispose()
    {
        surface?.Dispose();
        surface = null;
    }
}
