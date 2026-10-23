using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace theTerminal.GameObjects.Inspectables;

/// <summary>
/// Base class for objects in the scene the player can click for more details.
/// While the cursor is over one, it turns in 3D toward the cursor like a card
/// being pressed - the same 3dEffect DungeonSlime's title used, but around the
/// object's own center and driven by where the cursor is on the object.
/// </summary>
/// <remarks>
/// <para>
/// Each object is a subclass (see <see cref="CoffeeMug"/>): it picks its sprite
/// and says what the details panel shows; the hover, turn, placement and click
/// handling all live here. Override <see cref="OnInspect"/> for behavior on
/// click, and use <see cref="TimesInspected"/> for text that changes once the
/// player has looked before.
/// </para>
/// <para>
/// At rest it draws exactly over its spot in the background, so it's invisible
/// until it turns. The background still has its own copy painted underneath,
/// which shows around the edges while turning; the final art should leave
/// that spot empty in the background.
/// </para>
/// </remarks>
public abstract class Inspectable
{
    // How far it turns and how much it grows are the 3dEffect's TurnIntensity
    // and LiftIntensity, set by the scene and tweakable live in its debug window.

    // How quickly the turn catches up with the cursor; higher is snappier.
    private const float Responsiveness = 12f;

    private readonly Sprite _sprite;

    // The resting center and half size, kept exact (not rounded like Bounds)
    // so at rest it lands precisely over its spot in the background.
    private readonly Vector2 _center;
    private readonly Vector2 _halfSize;

    // Current turn, from -1 to 1: X spins around the vertical axis, Y tilts
    // around the horizontal one.
    private Vector2 _turn;

    // How lifted it is: 0 at rest, 1 fully hovered.
    private float _lift;

    /// <summary>The title of the details panel.</summary>
    public abstract string Name { get; }

    /// <summary>The body of the details panel. Read each time it opens, so it can change with state.</summary>
    public abstract string Description { get; }

    /// <summary>How many times the player has clicked it, including the click opening the panel right now.</summary>
    public int TimesInspected { get; private set; }

    /// <summary>The object's area in world space, used for hover and click.</summary>
    public Rectangle Bounds { get; }

    public bool IsHovered { get; private set; }

    /// <summary>
    /// Creates an object from a region cut out of the cubicle's angled frame
    /// (see prototype-scene-definition.xml), laid exactly over its spot in it.
    /// </summary>
    protected Inspectable(TextureRegion region, Cubicle cubicle)
    {
        Vector2 position = cubicle.FirstFrameToWorld(region.SourceRectangle.Location);
        _sprite = new Sprite(region) { Scale = cubicle.Scale };

        // Drawn from its center, the point the shader turns and lifts around.
        _sprite.CenterOrigin();

        _halfSize = new Vector2(_sprite.Width, _sprite.Height) / 2f;
        _center = position + _halfSize;
        Bounds = new Rectangle(position.ToPoint(), new Point((int)_sprite.Width, (int)_sprite.Height));
    }

    /// <summary>
    /// Called by the scene when the player clicks the object, right before the
    /// details panel opens and reads <see cref="Name"/> and <see cref="Description"/>.
    /// </summary>
    public void Inspect()
    {
        TimesInspected++;
        OnInspect();
    }

    /// <summary>
    /// Override for what clicking this object does beyond opening the panel
    /// (play a sound, reveal a clue, change state...). Runs after
    /// <see cref="TimesInspected"/> counts the click. Does nothing by default.
    /// </summary>
    protected virtual void OnInspect() { }

    /// <param name="cursor">The cursor in world space, or null when objects can't be hovered right now.</param>
    public void Update(GameTime gameTime, Vector2? cursor)
    {
        IsHovered = cursor.HasValue && Bounds.Contains(cursor.Value);

        Vector2 target = Vector2.Zero;
        if (IsHovered)
        {
            // The cursor's offset from the center, from -1 to 1 on each axis,
            // measured on the resting size so the lift doesn't change it.
            // Positive turns push the side under the cursor away from the
            // viewer, so the object dips where it's "pressed".
            Vector2 offset = (cursor.Value - _center) / _halfSize;
            target = offset;
        }

        // Ease toward the targets every frame (exponential smoothing), so it
        // follows the cursor softly and settles back flat when it leaves.
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float blend = 1f - MathF.Exp(-Responsiveness * dt);
        _turn = Vector2.Lerp(_turn, target, blend);
        _lift = MathHelper.Lerp(_lift, IsHovered ? 1f : 0f, blend);
    }

    /// <summary>
    /// Draws the object in its own sprite batch, since the turn parameters
    /// are per object.
    /// </summary>
    /// <param name="material">The 3dEffect material, with MatrixTransform already set.</param>
    public void Draw(SpriteBatch spriteBatch, Material material, SamplerState samplerState)
    {
        material.SetParameter("RotationCenter", _center);
        material.SetParameter("SpinAmount", _turn.X);
        material.SetParameter("TiltAmount", _turn.Y);
        material.SetParameter("Lift", _lift);

        // CullNone: a turn can flip the quad's winding, which would cull it.
        spriteBatch.Begin(samplerState: samplerState, rasterizerState: RasterizerState.CullNone, effect: material.Effect);
        _sprite.Draw(spriteBatch, _center);
        spriteBatch.End();
    }
}
