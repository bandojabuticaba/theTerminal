using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace theTerminal.GameObjects;

/// <summary>
/// The whole workstation scene: the room seen at an angle, which turns to face
/// the monitor head-on when the player clicks it. The turn is 8 frames played
/// once; turning away plays the same frames backwards.
/// </summary>
/// <remarks>
/// The scene is a stack of layers drawn back to front, each with its own
/// frames. Neo's room (neoroom.png, rendered by terminalScenes/neo_room.py and
/// packed with TexturePacker) has one layer per object: walls, table, both
/// monitors, keyboard, mouse and chair. Its neoroom.json is that script's
/// scene.json, copied as is: the layer order, frame delay, and where the main
/// monitor and its screen are. The prototype art is a single layer.
/// </remarks>
public class Cubicle
{
    // Flip this to false to go back to the AI-drawn prototype frames instead of
    // Neo's room.
    public static readonly bool UseNeoRoom = true;

    // The monitor in the first and last frames of the prototype art, in the
    // frame's native pixels (measured from prototype_atlas-0.png, bezel
    // included). The first is the click target; the last is where the
    // close-up monitor art is laid over the scene once the turn ends. Redo
    // these if the art changes. Neo's room carries its own in neoroom.json.
    private static readonly Rectangle PrototypeAngledMonitorRegion = new(144, 156, 102, 88);
    private static readonly Rectangle PrototypeFrontMonitorRegion = new(121, 170, 109, 86);

    // Native size of each frame (see prototype-scene-definition.xml). Neo's
    // room frames are a third of the 1440x810 canvas, so they fill it at an
    // even 3x; keep these in sync with WIDTH/HEIGHT in neo_room.py.
    private const float PrototypeFrameWidth = 358f;
    private const float PrototypeFrameHeight = 541f;
    private const float NeoRoomFrameWidth = 480f;
    private const float NeoRoomFrameHeight = 270f;

    private static float FrameWidth => UseNeoRoom ? NeoRoomFrameWidth : PrototypeFrameWidth;
    private static float FrameHeight => UseNeoRoom ? NeoRoomFrameHeight : PrototypeFrameHeight;

    // Each layer's frames, back to front. A null frame has nothing to draw
    // (the second monitor is out of view by the last one).
    private readonly List<PackedSprite[]> _layers = new();
    private readonly List<string> _layerNames = new();
    private readonly int _frameCount;
    private readonly TimeSpan _frameDelay;
    private readonly Rectangle _angledMonitorRegion;
    private readonly Rectangle _frontMonitorRegion;
    private readonly Rectangle? _frontScreenRegion;

    // The frame on screen, and the one the current turn is heading to.
    private int _frame;
    private int _targetFrame;
    private TimeSpan _frameElapsed;

    public Vector2 Position;

    /// <summary>The texture the frames are cut from.</summary>
    public Texture2D Texture { get; }

    /// <summary>How much the frames are scaled on screen, per axis.</summary>
    public Vector2 Scale { get; }

    public float Width => FrameWidth * Scale.X;
    public float Height => FrameHeight * Scale.Y;

    /// <summary>Whether the first (angled) frame is the one on screen.</summary>
    public bool IsOnFirstFrame => _frame == 0;

    /// <summary>Whether a turn has been started and hasn't reached its last frame yet.</summary>
    public bool IsTurning => _frame != _targetFrame;

    /// <summary>
    /// How far the current (or last) turn has got, from 0 on its first frame to
    /// 1 on its last, either way round.
    /// </summary>
    public float TurnProgress
    {
        get
        {
            float towardMonitor = (float)_frame / (_frameCount - 1);
            return _targetFrame == 0 ? 1f - towardMonitor : towardMonitor;
        }
    }

    /// <summary>The monitor in the angled (first) frame, in world space.</summary>
    public Rectangle AngledMonitorBounds => ToWorld(_angledMonitorRegion);

    /// <summary>The monitor in the head-on (last) frame, in world space.</summary>
    public Rectangle FrontMonitorBounds => ToWorld(_frontMonitorRegion);

    /// <summary>
    /// The monitor's screen in the head-on (last) frame, in world space, or null
    /// for the prototype art, whose screen is only known on the close-up
    /// <see cref="Monitor"/>.
    /// </summary>
    public Rectangle? FrontScreenBounds => _frontScreenRegion.HasValue ? ToWorld(_frontScreenRegion.Value) : null;

    /// <summary>The width that keeps the frames' proportions at the given height.</summary>
    public static float WidthForHeight(float height) => height * FrameWidth / FrameHeight;

    /// <param name="size">
    /// The on-screen size. The frames are stretched to it on each axis, so the
    /// art's proportions are only kept if the size has them (see
    /// <see cref="WidthForHeight"/>).
    /// </param>
    public Cubicle(ContentManager content, Vector2 size)
    {
        if (UseNeoRoom)
        {
            var sheet = TexturePackerSheet.FromFile(content, "images/neoroom.txt");
            Texture = sheet.Texture;

            using Stream stream = TitleContainer.OpenStream(Path.Combine(content.RootDirectory, "images/neoroom.json"));
            using JsonDocument scene = JsonDocument.Parse(stream);
            JsonElement root = scene.RootElement;

            JsonElement frame = root.GetProperty("frame");
            _frameCount = frame.GetProperty("count").GetInt32();
            _frameDelay = TimeSpan.FromMilliseconds(frame.GetProperty("delay").GetDouble());

            // The sprites are named after the layer folders TexturePacker was
            // given: "<layer>/turn-1" to "<layer>/turn-N".
            foreach (JsonElement layer in root.GetProperty("layers").EnumerateArray())
            {
                string name = layer.GetProperty("name").GetString();
                JsonElement[] bounds = [.. layer.GetProperty("frames").EnumerateArray()];

                var frames = new PackedSprite[_frameCount];
                for (int i = 0; i < _frameCount; i++)
                {
                    frames[i] = bounds[i].ValueKind == JsonValueKind.Null ? null : sheet.GetSprite($"{name}/turn-{i + 1}");
                }

                _layers.Add(frames);
                _layerNames.Add(name);
            }

            JsonElement regions = root.GetProperty("regions");
            _angledMonitorRegion = ReadRectangle(regions.GetProperty("monitor-first"));
            _frontMonitorRegion = ReadRectangle(regions.GetProperty("monitor-last"));
            _frontScreenRegion = ReadRectangle(regions.GetProperty("screen-last"));
        }
        else
        {
            TextureAtlas atlas = TextureAtlas.FromFile(content, "images/prototype-scene-definition.xml");
            Texture = atlas.Texture;

            Animation turn = atlas.GetAnimation("turn-to-monitor");
            _frameCount = turn.Frames.Count;
            _frameDelay = turn.Delay;
            _layers.Add(turn.Frames.ConvertAll(region => new PackedSprite(region, false, Vector2.Zero)).ToArray());

            _angledMonitorRegion = PrototypeAngledMonitorRegion;
            _frontMonitorRegion = PrototypeFrontMonitorRegion;
        }

        Scale = size / new Vector2(FrameWidth, FrameHeight);
    }

    /// <summary>
    /// Hands over a layer's sprite in the first (angled) frame, e.g. "mouse",
    /// and stops drawing it there, so whoever takes it draws it instead (see
    /// <see cref="Inspectables.Inspectable"/>). The other frames still have it.
    /// Neo's room only: the prototype art has no layers.
    /// </summary>
    public PackedSprite TakeFirstFrameSprite(string layer)
    {
        PackedSprite[] frames = _layers[_layerNames.IndexOf(layer)];
        PackedSprite sprite = frames[0];
        frames[0] = null;
        return sprite;
    }

    /// <summary>Plays the turn from the angled view to facing the monitor.</summary>
    public void TurnToMonitor() => StartTurn(_frameCount - 1);

    /// <summary>Plays the turn from facing the monitor back to the angled view.</summary>
    public void TurnAway() => StartTurn(0);

    public void Update(GameTime gameTime)
    {
        if (!IsTurning)
        {
            return;
        }

        _frameElapsed += gameTime.ElapsedGameTime;
        while (_frameElapsed >= _frameDelay && IsTurning)
        {
            _frameElapsed -= _frameDelay;
            _frame += Math.Sign(_targetFrame - _frame);
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (PackedSprite[] layer in _layers)
        {
            layer[_frame]?.Draw(spriteBatch, Position, Scale, Color.White);
        }
    }

    private void StartTurn(int targetFrame)
    {
        _targetFrame = targetFrame;
        _frameElapsed = TimeSpan.Zero;
    }

    private static Rectangle ReadRectangle(JsonElement element) => new(
        element.GetProperty("x").GetInt32(),
        element.GetProperty("y").GetInt32(),
        element.GetProperty("width").GetInt32(),
        element.GetProperty("height").GetInt32());

    private Rectangle ToWorld(Rectangle region) => new(
        (int)(Position.X + region.X * Scale.X),
        (int)(Position.Y + region.Y * Scale.Y),
        (int)(region.Width * Scale.X),
        (int)(region.Height * Scale.Y));
}
