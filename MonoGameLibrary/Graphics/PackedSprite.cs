using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameLibrary.Graphics;

/// <summary>
/// One image packed into a sprite sheet the way TexturePacker packs it: trimmed
/// (its transparent edges cut off) and possibly turned a quarter turn clockwise
/// to fit tighter. Drawing it undoes both, so it lands exactly where it was in
/// the original, untrimmed image.
/// </summary>
public class PackedSprite
{
    /// <summary>The sprite's pixels in the sheet, turned if <see cref="IsRotated"/>.</summary>
    public TextureRegion Region { get; }

    /// <summary>Whether the sprite is stored a quarter turn clockwise in the sheet.</summary>
    public bool IsRotated { get; }

    /// <summary>Where the trimmed sprite's top-left corner was in the original image.</summary>
    public Vector2 Offset { get; }

    /// <summary>The trimmed sprite's size, upright.</summary>
    public Point Size => IsRotated ? new Point(Region.Height, Region.Width) : new Point(Region.Width, Region.Height);

    public PackedSprite(TextureRegion region, bool isRotated, Vector2 offset)
    {
        Region = region;
        IsRotated = isRotated;
        Offset = offset;
    }

    /// <summary>
    /// Draws the sprite as if it were the whole original image, with that
    /// image's top-left corner at <paramref name="position"/>.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Vector2 position, Vector2 scale, Color color)
    {
        Vector2 topLeft = position + Offset * scale;

        if (!IsRotated)
        {
            Region.Draw(spriteBatch, topLeft, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return;
        }

        // Turned back a quarter turn counterclockwise about the sheet region's
        // top-right corner, which holds the sprite's top-left. Scale applies
        // before the turn, so its axes swap.
        Region.Draw(
            spriteBatch,
            topLeft,
            color,
            -MathHelper.PiOver2,
            new Vector2(Region.Width, 0f),
            new Vector2(scale.Y, scale.X),
            SpriteEffects.None,
            0f);
    }
}
