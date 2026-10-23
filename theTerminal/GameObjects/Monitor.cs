using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace theTerminal.GameObjects;

/// <summary>
/// The close-up monitor (prototype art), laid over the cubicle once it has
/// turned to face the monitor, and the thing the terminal zoom pushes into.
/// The art is high resolution (748x804) so it still looks sharp after the
/// zoom; it starts scaled down to cover the monitor in the scene.
/// </summary>
public class Monitor
{
    // The dark glass area inside the bezel, in the sprite's native pixels
    // (measured from prototype_atlas-1.png). Redo this if the monitor art changes.
    private static readonly Rectangle ScreenRegion = new(95, 124, 556, 451);

    // Height of the bezel alone, without the stand below it, in native pixels.
    private const float BezelHeight = 679f;

    private readonly Sprite _sprite;

    public Vector2 Position;

    /// <summary>How opaque the monitor is drawn, from 0 (invisible) to 1.</summary>
    public float Opacity = 1f;

    public float Width => _sprite.Width;
    public float Height => _sprite.Height;

    /// <summary>The screen (glass) area in world space, at the current position and scale.</summary>
    public Rectangle ScreenBounds => new(
        (int)(Position.X + ScreenRegion.X * _sprite.Scale.X),
        (int)(Position.Y + ScreenRegion.Y * _sprite.Scale.Y),
        (int)(ScreenRegion.Width * _sprite.Scale.X),
        (int)(ScreenRegion.Height * _sprite.Scale.Y));

    public Monitor(ContentManager content)
    {
        TextureAtlas atlas = TextureAtlas.FromFile(content, "images/prototype-monitor-definition.xml");
        _sprite = atlas.CreateSprite("monitor");
    }

    /// <summary>
    /// Scales the monitor uniformly so its bezel is as tall as
    /// <paramref name="bounds"/>, top-aligned and centered horizontally on them.
    /// Uniform even when the scene is stretched, so the screen keeps its real
    /// shape when the terminal zoom fills the view with it. The stand hangs
    /// below, over the scene's own stand.
    /// </summary>
    public void FitTo(Rectangle bounds)
    {
        float scale = bounds.Height / BezelHeight;
        _sprite.Scale = new Vector2(scale, scale);
        Position = new Vector2(bounds.Center.X - Width / 2f, bounds.Top);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        // Content textures are premultiplied, so fading scales every channel.
        _sprite.Color = Color.White * Opacity;
        _sprite.Draw(spriteBatch, Position);
    }
}
