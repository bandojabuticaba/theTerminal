using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace theTerminal.GameObjects;

/// <summary>
/// The whole workstation scene (prototype art): the cubicle seen at an angle,
/// which turns to face the monitor head-on when the player clicks it. The turn
/// is the 8-frame "turn-to-monitor" animation from prototype-scene-definition.xml,
/// played once; turning away plays the same frames backwards.
/// </summary>
public class Cubicle
{
    // The monitor in the first and last frames, in the frame's native pixels
    // (measured from prototype_atlas-0.png, bezel included). The first is the
    // click target; the last is where the close-up monitor art is laid over
    // the scene once the turn ends. Redo these if the art changes.
    private static readonly Rectangle AngledMonitorRegion = new(144, 156, 102, 88);
    private static readonly Rectangle FrontMonitorRegion = new(121, 170, 109, 86);

    // Native size of each frame (see prototype-scene-definition.xml).
    private const float FrameWidth = 358f;
    private const float FrameHeight = 541f;

    private readonly AnimatedSprite _sprite;
    private readonly Animation _turnToMonitor;
    private readonly Animation _turnAway;

    public Vector2 Position;

    /// <summary>The atlas texture the frames are cut from.</summary>
    public Texture2D Texture => _sprite.Region.Texture;

    /// <summary>How much the frames are scaled on screen, per axis.</summary>
    public Vector2 Scale => _sprite.Scale;

    public float Width => _sprite.Width;
    public float Height => _sprite.Height;

    /// <summary>Whether a turn has been started and hasn't reached its last frame yet.</summary>
    public bool IsTurning { get; private set; }

    /// <summary>The monitor in the angled (first) frame, in world space.</summary>
    public Rectangle AngledMonitorBounds => ToWorld(AngledMonitorRegion);

    /// <summary>The monitor in the head-on (last) frame, in world space.</summary>
    public Rectangle FrontMonitorBounds => ToWorld(FrontMonitorRegion);

    /// <summary>The width that keeps the frames' proportions at the given height.</summary>
    public static float WidthForHeight(float height) => height * FrameWidth / FrameHeight;

    /// <param name="size">
    /// The on-screen size. The frames are stretched to it on each axis, so the
    /// art's proportions are only kept if the size has them (see
    /// <see cref="WidthForHeight"/>).
    /// </param>
    public Cubicle(ContentManager content, Vector2 size)
    {
        TextureAtlas atlas = TextureAtlas.FromFile(content, "images/prototype-scene-definition.xml");

        _turnToMonitor = atlas.GetAnimation("turn-to-monitor");
        var reversed = new List<TextureRegion>(_turnToMonitor.Frames);
        reversed.Reverse();
        _turnAway = new Animation(reversed, _turnToMonitor.Delay);

        _sprite = atlas.CreateAnimatedSprite("turn-to-monitor");
        _sprite.IsLooping = false;

        _sprite.Scale = size / new Vector2(_sprite.Region.Width, _sprite.Region.Height);
    }

    /// <summary>
    /// Where a pixel of the atlas inside the first (angled) frame lands in world
    /// space. Used to lay sprites cut from that frame exactly over it.
    /// </summary>
    public Vector2 FirstFrameToWorld(Point atlasPoint)
    {
        Point frameOrigin = _turnToMonitor.Frames[0].SourceRectangle.Location;
        return Position + (atlasPoint - frameOrigin).ToVector2() * _sprite.Scale;
    }

    /// <summary>Plays the turn from the angled view to facing the monitor.</summary>
    public void TurnToMonitor() => StartTurn(_turnToMonitor);

    /// <summary>Plays the turn from facing the monitor back to the angled view.</summary>
    public void TurnAway() => StartTurn(_turnAway);

    public void Update(GameTime gameTime)
    {
        if (!IsTurning)
        {
            return;
        }

        _sprite.Update(gameTime);
        IsTurning = !_sprite.IsFinished;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _sprite.Draw(spriteBatch, Position);
    }

    private void StartTurn(Animation animation)
    {
        _sprite.Animation = animation;
        IsTurning = true;
    }

    private Rectangle ToWorld(Rectangle region) => new(
        (int)(Position.X + region.X * _sprite.Scale.X),
        (int)(Position.Y + region.Y * _sprite.Scale.Y),
        (int)(region.Width * _sprite.Scale.X),
        (int)(region.Height * _sprite.Scale.Y));
}
