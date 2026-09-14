using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;

namespace Sznine.BehaviorAutomation;

/// <summary>Draw only game-owned sprites; no bundled copies of game textures.</summary>
public static class ActionIcons
{
    private static readonly ConditionalWeakTable<Texture2D, Dictionary<Rectangle, Rectangle>> trimmed = new();
    private static PetBowl? bowlIcon;
    public static void Draw(SpriteBatch batch, ActionDefinition action, Rectangle bounds, float opacity = 1)
    {
        Texture2D texture;
        Rectangle source;
        switch (action.Kind)
        {
            case ActionKind.Pet:
                texture = Game1.content.Load<Texture2D>("Animals/White Cow");
                source = new(0, 0, 32, 32);
                break;
            case ActionKind.WaterBowl:
                var bowl = bowlIcon ??= new PetBowl(Vector2.Zero);
                texture = bowl.texture.Value;
                source = Trim(texture, bowl.getSourceRect());
                break;
            case ActionKind.WildTree:
            case ActionKind.TreeStump:
            case ActionKind.Sapling:
                texture = Game1.content.Load<Texture2D>("TerrainFeatures/tree1_spring");
                source = Trim(texture, action.Kind == ActionKind.WildTree ? Tree.treeTopSourceRect : action.Kind == ActionKind.TreeStump ? Tree.stumpSourceRect : new(0, 128, 16, 16));
                break;
            case ActionKind.FruitTree:
                texture = Game1.content.Load<Texture2D>("TileSheets/fruitTrees");
                source = Trim(texture, new(192, 0, 48, 64));
                break;
            case ActionKind.Bush:
                texture = Bush.texture.Value;
                source = Trim(texture, new(32, 0, 32, 48));
                break;
            case ActionKind.Grass:
                texture = Game1.content.Load<Texture2D>("TerrainFeatures/grass");
                source = Trim(texture, new(0, 0, 15, 20));
                break;
            case ActionKind.DeadCrop:
                texture = Game1.cropSpriteSheet;
                source = Trim(texture, new(192, 384, 16, 32));
                break;
            case ActionKind.LargeRock:
            case ActionKind.LargeWood:
                texture = Game1.objectSpriteSheet;
                source = Game1.getSourceRectForStandardTileSheet(texture, action.Kind == ActionKind.LargeRock ? 672 : 602, 16, 16);
                source.Width = source.Height = 32;
                source = Trim(texture, source);
                break;
            default:
                var data = ItemRegistry.GetDataOrErrorItem(action.IconId);
                texture = data.GetTexture();
                source = data.GetSourceRect();
                break;
        }
        float scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
        batch.Draw(texture, bounds.Center.ToVector2(), source, Color.White * opacity, 0, source.Size.ToVector2() / 2, scale, SpriteEffects.None, 1);
        if (action.Kind == ActionKind.WaterBowl)
        {
            var water = source;
            water.X += bowlIcon!.getSourceRect().Width;
            batch.Draw(texture, bounds.Center.ToVector2(), water, Color.White * opacity, 0, water.Size.ToVector2() / 2, scale, SpriteEffects.None, 1);
        }
        // Tool/result pairs distinguish interacting with a target from removing it.
        string? badge = action.Kind switch
        {
            ActionKind.Milk => "(O)184",
            ActionKind.Shear => "(O)440",
            ActionKind.WildTree or ActionKind.FruitTree or ActionKind.Sapling or ActionKind.TreeStump or ActionKind.LargeWood => "(T)Axe",
            ActionKind.LargeRock or ActionKind.RemoveFloor => "(T)Pickaxe",
            ActionKind.Machine => "(O)334",
            _ => null
        };
        if (badge is not null)
        {
            var data = ItemRegistry.GetDataOrErrorItem(badge);
            var r = data.GetSourceRect();
            int size = bounds.Width / 2;
            batch.Draw(data.GetTexture(), new Rectangle(bounds.Right - size + 4, bounds.Bottom - size, size, size), r, Color.White * opacity);
        }
    }

    private static Rectangle Trim(Texture2D texture, Rectangle source)
    {
        var cache = trimmed.GetOrCreateValue(texture);
        if (cache.TryGetValue(source, out var result))
            return result;
        var pixels = new Color[source.Width * source.Height];
        texture.GetData(0, source, pixels, 0, pixels.Length);
        int left = source.Width, top = source.Height, right = -1, bottom = -1;
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                if (pixels[y * source.Width + x].A > 0)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
        return cache[source] = right < left ? source : new(source.X + left, source.Y + top, right - left + 1, bottom - top + 1);
    }
}
