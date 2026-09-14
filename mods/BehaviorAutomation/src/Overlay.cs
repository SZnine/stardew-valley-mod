using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Sznine.BehaviorAutomation;
public static class Overlay
{
    private static Rectangle Screen(Cell p) => new(p.X * 64 - Game1.viewport.X, p.Y * 64 - Game1.viewport.Y, 64, 64);
    private static void Fill(SpriteBatch b, Rectangle r, Color c) => b.Draw(Game1.staminaRect, r, c);
    private static void Border(SpriteBatch b, Rectangle r, Color c, int thickness = 2)
    {
        Fill(b, new(r.X, r.Y, r.Width, thickness), c);
        Fill(b, new(r.X, r.Bottom - thickness, r.Width, thickness), c);
        Fill(b, new(r.X, r.Y, thickness, r.Height), c);
        Fill(b, new(r.Right - thickness, r.Y, thickness, r.Height), c);
    }
    public static void DrawWorld(SpriteBatch b, WorkController control, DragSelection? drag, ModConfig config, Cell? hover = null)
    {
        if (control.Board.Location is not null && control.Board.Location != Game1.currentLocation)
            return;
        var view = new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height);
        DrawOverhead(b, control);
        if (drag is not null)
        {
            var area = SelectionGeometry.Rectangle(drag.Start, drag.End, WorkRules.MaxSelectionSize);
            var r = new Rectangle(area.X * 64 - Game1.viewport.X, area.Y * 64 - Game1.viewport.Y, area.Width * 64, area.Height * 64);
            Fill(b, r, ModeInfo.Color(drag.Mode) * .18f);
            Border(b, r, Color.White * .9f, 3);
        }
        if (control.Editing && hover is { } hoveredCell)
        {
            var r = Screen(hoveredCell);
            Fill(b, r, new Color(230, 255, 235) * .27f);
            Border(b, r, Color.White, 2);
            // Corners and boundaries use the full world tile, independent of UI/zoom scaling.
            Corners(b, r, Color.White);
            if (drag is not null && drag.Start != hoveredCell)
                Corners(b, Screen(drag.Start), new Color(255, 240, 145));
        }
    }
    public static Rectangle OverheadBounds(Farmer who, double milliseconds, bool animated)
    {
        float bob = animated ? (float)Math.Sin(milliseconds / 180d) * 4 : 0;
        var standing = who.getStandingPosition();
        return new((int)standing.X - Game1.viewport.X - 24, (int)(standing.Y - who.Sprite.SpriteHeight * 4 - 40 - bob) - Game1.viewport.Y, 48, 48);
    }
    private static void DrawOverhead(SpriteBatch b, WorkController control)
    {
        if (!control.HasSession)
            return;
        double time = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
        bool working = !control.Paused && !control.Editing && !control.ManualMovement;
        var bounds = OverheadBounds(Game1.player, time, working);
        float angle = working ? (float)Math.Sin(time / 140d) * .12f : 0;
        float opacity = working ? .86f + (float)Math.Sin(time / 160d) * .14f : .6f;
        DrawIcon(b, control.DisplayedTask is { } task ? task.Icon : control.Board.SelectionIcon, bounds, Color.White * opacity, angle);
    }
    private static void DrawIcon(SpriteBatch b, Item? item, Rectangle bounds, Color? tint = null, float angle = 0)
    {
        var color = tint ?? Color.White;
        if (item is not null)
        {
            var data = ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId);
            var source = data.GetSourceRect();
            b.Draw(data.GetTexture(), bounds.Center.ToVector2(), source, color, angle, source.Size.ToVector2() / 2, new Vector2(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height), SpriteEffects.None, 0);
        }
        else
            DrawHand(b, bounds.Location.ToVector2() + new Vector2(3, 3), color, bounds.Width / 24f);
    }
    private static void Corners(SpriteBatch b, Rectangle r, Color c)
    {
        foreach (int x in new[] { r.Left, r.Right - 10 })
            foreach (int y in new[] { r.Top, r.Bottom - 10 })
            {
                Fill(b, new(x, y == r.Top ? y : y + 7, 10, 3), c);
                Fill(b, new(x == r.Left ? x : x + 7, y, 3, 10), c);
            }
    }
    private static void DrawHand(SpriteBatch b, Vector2 p, Color c, float scale = 1)
    {
        int x = (int)p.X, y = (int)p.Y;
        void Part(int px, int py, int w, int h) => Fill(b, new(x + (int)(px * scale), y + (int)(py * scale), (int)(w * scale), (int)(h * scale)), c);
        Part(4, 8, 12, 9);
        Part(6, 17, 8, 3);
        for (int i = 0; i < 4; i++)
            Part(4 + i * 3, i == 0 || i == 3 ? 3 : 0, 2, 10);
        Part(0, 8, 3, 7);
        Part(2, 12, 4, 5);
    }
}
