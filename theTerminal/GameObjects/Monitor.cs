using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace theTerminal.GameObjects;

/// <summary>
/// The pixel-art monitor prop, drawn above the clipboard. Loaded through the
/// same TextureAtlas pipeline DungeonSlime uses (atlas-definition.xml +
/// atlas.png) - the difference is atlas.png here comes from TexturePacker
/// (trimmed/packed) rather than hand-measured region coordinates.
/// </summary>
public class Monitor
{
    // Prototype scale: the monitor's native art is 153x158 (TexturePacker's
    // trimmed size). At 2x it's 306x316 on screen, comfortably fitting the
    // ~440px tall area above the clipboard frame. Adjust once the real art
    // and final layout are in.
    private static readonly Vector2 SpriteScale = new(2f, 2f);

    private readonly Sprite _sprite;

    public Vector2 Position;

    public float Width => _sprite.Width;
    public float Height => _sprite.Height;

    public Monitor(ContentManager content)
    {
        TextureAtlas atlas = TextureAtlas.FromFile(content, "images/atlas-definition.xml");
        _sprite = atlas.CreateSprite("monitor");
        _sprite.Scale = SpriteScale;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _sprite.Draw(spriteBatch, Position);
    }
}
