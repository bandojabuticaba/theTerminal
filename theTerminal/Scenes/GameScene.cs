using System;
using System.Collections.Generic;
using Gum;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using MonoGameLibrary.Content;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Scenes;
using theTerminal.GameObjects;
using theTerminal.GameObjects.Inspectables;

namespace theTerminal.Scenes;

/// <summary>
/// The main gameplay scene: the workstation where the player collects words
/// into the clipboard and drags them into the terminal.
/// </summary>
/// <remarks>
/// In <see cref="Mode.Explore"/> the player sees the cubicle at an angle.
/// Clicking its monitor plays the turn animation (<see cref="Mode.TurningIn"/>);
/// once the cubicle faces the monitor, the close-up monitor art is laid over
/// the scene's monitor and the camera zooms in until its screen fills the area
/// above the clipboard (<see cref="Mode.Terminal"/>). Esc, or clicking outside
/// both the screen and the clipboard, zooms back out (<see cref="Mode.Leaving"/>)
/// and then plays the turn backwards (<see cref="Mode.TurningOut"/>). Also in
/// explore, hovering an <see cref="Inspectable"/> object turns it toward the
/// cursor, and clicking it opens a details panel (<see cref="Mode.Inspecting"/>)
/// until Esc or a click outside the panel. The
/// clipboard is drawn by Gum in screen space, so the camera never moves it -
/// it stays put in every mode.
/// </remarks>
public class GameScene : Scene
{
    private enum Mode
    {
        Explore,
        Inspecting,
        TurningIn,
        Terminal,
        Leaving,
        TurningOut,
    }

    // Space left around the zoomed screen in terminal mode.
    private const float TerminalMargin = 20f;

    // Flip this to false to draw the scene at its real proportions (full height,
    // centered, black bars on the sides) instead of stretched over the canvas.
    private static readonly bool StretchScene = false;

    // How far into the zoom (0 to 1, see _zoomT) the close-up monitor finishes
    // fading in over the scene's own monitor. The two are different art, so a
    // fade hides the swap better than a hard cut. Lower is a quicker fade.
    private const float MonitorFadeEnd = 0.6f;

    // How big the scene's pixelation blocks get, in texture pixels, once fully
    // zoomed in. The scene behind the monitor is blown up several times by the
    // zoom; pixelating it makes that read as deliberate, chunky background
    // instead of blurry, over-scaled art. 1 turns the effect off.
    private const int MaxScenePixelSize = 4;

    // Starting strength of the inspectables' hover effect (see 3dEffect.fxh):
    // how far they turn toward the cursor, in turns, and how much they grow.
    // Set once at load, so the 3dEffect debug window can change them live.
    private const float InspectTurnIntensity = 0.08f;
    private const float InspectLiftIntensity = 0.15f;

    // Flip this to false to hide the 3dEffect debug window (sliders for the
    // hover effect). Leave its "Override Values" box unticked: it would also
    // freeze the per-object values the scene sets every frame.
    private static readonly bool ShowInspectEffectDebug = true;

    // How long a full zoom between explore and terminal takes, in seconds.
    private const float ZoomDuration = 0.5f;

    // The timing curve for the zoom (see MonoGameLibrary.Easing). An Out curve
    // because the player triggers the zoom: it reacts instantly to the click and
    // settles softly. Swap for Easing.OutCubic (gentler) or Easing.OutBack
    // (slight overshoot, a physical "lean in") to compare.
    private static readonly Func<float, float> ZoomEasing = Easing.OutQuart;

    // Linear rather than point sampling: the prototype art is drawn at
    // non-integer scales (the scene stretched ~4x by ~1.5x, the monitor at ~0.2x and growing
    // with the zoom), where point sampling gives uneven, shimmering pixels.
    private static readonly SamplerState ArtSampler = SamplerState.LinearClamp;

    private Clipboard _clipboard;
    private Cubicle _cubicle;
    private Monitor _monitor;
    private Material _pixelate;
    private Material _turn3d;
    private readonly SpriteCamera3d _camera3d = new();
    private readonly List<Inspectable> _inspectables = new();
    private InspectPanel _inspectPanel;

    private Mode _mode = Mode.Explore;
    private Matrix _camera = Matrix.Identity;

    // Where the camera is between the two views: 0 = explore, 1 = terminal
    // (briefly past 1 with an overshooting easing). Each mode change starts a
    // new tween from wherever it is now, so reversing mid-zoom never jumps.
    private float _zoomT;
    private float _zoomFrom;
    private float _zoomTo;
    private float _zoomElapsed;
    private float _zoomDuration;

    // The terminal view as a zoom about a fixed point: the one world point that
    // stays in the same place on screen in both views. Every other point moves
    // straight toward or away from it, like a camera pushing in.
    private Vector2 _zoomFocus;
    private float _terminalZoom;

    public override void Initialize()
    {
        // base.Initialize() runs LoadContent, so the monitor exists by the time
        // the terminal view is computed below.
        base.Initialize();

        _clipboard = new Clipboard();
        _inspectPanel = new InspectPanel(areaHeight: _clipboard.Top);
        ComputeTerminalView();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        if (StretchScene)
        {
            _cubicle = new Cubicle(Content, new Vector2(Core.VirtualWidth, Core.VirtualHeight));
        }
        else
        {
            _cubicle = new Cubicle(Content, new Vector2(Cubicle.WidthForHeight(Core.VirtualHeight), Core.VirtualHeight));
            _cubicle.Position = new Vector2((Core.VirtualWidth - _cubicle.Width) / 2f, 0f);
        }

        _monitor = new Monitor(Content);
        _monitor.FitTo(_cubicle.FrontMonitorBounds);

        _pixelate = Core.SharedContent.WatchMaterial("effects/pixelateEffect");
        _pixelate.SetParameter("TextureSize", new Vector2(_cubicle.Texture.Width, _cubicle.Texture.Height));

        _turn3d = Core.SharedContent.WatchMaterial("effects/3dEffect");
        _turn3d.SetParameter("TurnIntensity", InspectTurnIntensity);
        _turn3d.SetParameter("LiftIntensity", InspectLiftIntensity);
        _turn3d.IsDebugVisible = ShowInspectEffectDebug;

        // The objects the player can click for details (see Inspectable).
        TextureAtlas sceneAtlas = TextureAtlas.FromFile(Content, "images/prototype-scene-definition.xml");
        _inspectables.Add(new CoffeeMug(sceneAtlas, _cubicle));
    }

    public override void Update(GameTime gameTime)
    {
        // Gum first, so the clipboard polls this frame's cursor state.
        GumService.Default.Update(gameTime);
        _clipboard.Update(gameTime);

        _cubicle.Update(gameTime);
        UpdateZoom(gameTime);
        _pixelate.Update();
        _turn3d.Update();

        // Objects only react to the cursor in explore; otherwise they settle flat.
        Vector2? hoverCursor = _mode == Mode.Explore && IsCursorInsideGame() ? CursorWorldPosition() : null;
        foreach (var inspectable in _inspectables)
        {
            inspectable.Update(gameTime, hoverCursor);
        }

        switch (_mode)
        {
            case Mode.Explore:
                UpdateExplore();
                break;
            case Mode.Inspecting:
                UpdateInspecting();
                break;
            case Mode.TurningIn:
                if (!_cubicle.IsTurning)
                {
                    SetMode(Mode.Terminal);
                }
                break;
            case Mode.Terminal:
                UpdateTerminal();
                break;
            case Mode.Leaving:
                UpdateLeaving();
                break;
            case Mode.TurningOut:
                if (!_cubicle.IsTurning)
                {
                    _mode = Mode.Explore;
                }
                break;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        // Drawn inside the scene so Core's transition overlay covers it.
        // The scene gets blockier as the camera zooms in, in whole-texel steps so
        // the blocks snap rather than swim. Outside the zoom it's drawn plain.
        int pixelSize = 1 + (int)MathF.Round((MaxScenePixelSize - 1) * MathHelper.Clamp(_zoomT, 0f, 1f));
        _pixelate.SetParameter("PixelSize", pixelSize);

        Core.SpriteBatch.Begin(
            samplerState: ArtSampler,
            transformMatrix: _camera,
            effect: pixelSize > 1 ? _pixelate.Effect : null);
        _cubicle.Draw(Core.SpriteBatch);
        Core.SpriteBatch.End();

        // Inspectables only line up with the angled frame, which is on screen
        // (unzoomed) only in these two modes. The 3D camera is rebuilt here
        // rather than at load: it reads the viewport, which is only the virtual
        // canvas while the scene draws into it.
        if (_mode == Mode.Explore || _mode == Mode.Inspecting)
        {
            _turn3d.SetParameter("MatrixTransform", _camera3d.CalculateMatrixTransform());
            foreach (var inspectable in _inspectables)
            {
                inspectable.Draw(Core.SpriteBatch, _turn3d, ArtSampler);
            }
        }

        Core.SpriteBatch.Begin(samplerState: ArtSampler, transformMatrix: _camera);

        // The close-up monitor only exists while the cubicle faces it, and fades
        // in with the zoom (and out again when zooming back out).
        if (_mode == Mode.Terminal || _mode == Mode.Leaving)
        {
            _monitor.Opacity = MathHelper.Clamp(_zoomT / MonitorFadeEnd, 0f, 1f);
            _monitor.Draw(Core.SpriteBatch);
        }

        Core.SpriteBatch.End();

        GumService.Default.Draw();
    }

    public override void UnloadContent()
    {
        _clipboard?.Dispose();
        _inspectPanel?.Dispose();

        base.UnloadContent();
    }

    private void UpdateExplore()
    {
        // Core's own exit-on-Esc is off (see Game1), since Esc leaves terminal
        // mode; in explore mode it still quits.
        if (Core.Input.Keyboard.WasKeyJustPressed(Keys.Escape))
        {
            Core.Instance.Exit();
            return;
        }

        if (!GumService.Default.Cursor.PrimaryPush)
        {
            return;
        }

        if (IsCursorOver(_cubicle.AngledMonitorBounds))
        {
            _mode = Mode.TurningIn;
            _cubicle.TurnToMonitor();
            return;
        }

        foreach (var inspectable in _inspectables)
        {
            if (inspectable.IsHovered)
            {
                inspectable.Inspect();
                _inspectPanel.Show(inspectable);
                _mode = Mode.Inspecting;
                return;
            }
        }
    }

    private void UpdateInspecting()
    {
        var cursor = GumService.Default.Cursor;
        bool clickedOutside = cursor.PrimaryPush
            && IsCursorInsideGame()
            && !_inspectPanel.Contains(cursor.XRespectingGumZoomAndBounds(), cursor.YRespectingGumZoomAndBounds());

        if (Core.Input.Keyboard.WasKeyJustPressed(Keys.Escape) || clickedOutside)
        {
            _inspectPanel.Hide();
            _mode = Mode.Explore;
        }
    }

    private void UpdateTerminal()
    {
        if (Core.Input.Keyboard.WasKeyJustPressed(Keys.Escape))
        {
            SetMode(Mode.Leaving);
            return;
        }

        var cursor = GumService.Default.Cursor;
        if (cursor.PrimaryPush
            && IsCursorInsideGame()
            && !IsCursorOver(_monitor.ScreenBounds)
            && !_clipboard.Contains(cursor.XRespectingGumZoomAndBounds(), cursor.YRespectingGumZoomAndBounds()))
        {
            SetMode(Mode.Leaving);
        }
    }

    private void UpdateLeaving()
    {
        // Clicking the screen while zooming out reverses back into the terminal.
        if (GumService.Default.Cursor.PrimaryPush && IsCursorOver(_monitor.ScreenBounds))
        {
            SetMode(Mode.Terminal);
            return;
        }

        // Fully zoomed out: turn the cubicle back to the angled view.
        if (_zoomElapsed >= _zoomDuration)
        {
            _mode = Mode.TurningOut;
            _cubicle.TurnAway();
        }
    }

    /// <summary>
    /// Whether the cursor is over the game itself: the window has focus and the
    /// cursor is inside the virtual canvas (not on the letterbox bars or the
    /// title bar). Needed because on macOS the mouse buttons are read globally,
    /// so a click on another app or the desktop still shows up as a push.
    /// </summary>
    private static bool IsCursorInsideGame()
    {
        var cursor = GumService.Default.Cursor;
        float x = cursor.XRespectingGumZoomAndBounds();
        float y = cursor.YRespectingGumZoomAndBounds();

        return Core.Instance.IsActive
            && x >= 0f && x < Core.VirtualWidth
            && y >= 0f && y < Core.VirtualHeight;
    }

    /// <summary>Switches between the zoomed-in and zoomed-out ends of the terminal zoom.</summary>
    private void SetMode(Mode mode)
    {
        _mode = mode;

        // Start a fresh tween from the current position. A reversal halfway
        // through only has half the distance to cover, so it gets half the time.
        _zoomFrom = _zoomT;
        _zoomTo = mode == Mode.Terminal ? 1f : 0f;
        _zoomElapsed = 0f;
        _zoomDuration = ZoomDuration * Math.Abs(_zoomTo - _zoomFrom);
    }

    /// <summary>Advances the zoom tween and rebuilds the camera from it.</summary>
    private void UpdateZoom(GameTime gameTime)
    {
        _zoomElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        float x = _zoomDuration > 0f ? Math.Min(_zoomElapsed / _zoomDuration, 1f) : 1f;
        _zoomT = MathHelper.Lerp(_zoomFrom, _zoomTo, ZoomEasing(x));

        // Scale is z^t, not a lerp from 1 to z: we perceive zoom as ratios, so
        // equal steps of t should multiply the size by equal factors. A linear
        // lerp from 1x to 3.3x would rush at the start and crawl at the end.
        float zoom = MathF.Pow(_terminalZoom, _zoomT);

        // Scale about the fixed point: x' = focus + zoom * (x - focus).
        // Identity at t = 0, exactly the terminal view at t = 1.
        _camera = Matrix.CreateTranslation(-_zoomFocus.X, -_zoomFocus.Y, 0f)
                * Matrix.CreateScale(zoom, zoom, 1f)
                * Matrix.CreateTranslation(_zoomFocus.X, _zoomFocus.Y, 0f);
    }

    /// <summary>
    /// Where the cursor is in world space, at whatever zoom is active - it's
    /// mapped back through the camera.
    /// </summary>
    private Vector2 CursorWorldPosition()
    {
        var cursor = GumService.Default.Cursor;
        var screenPoint = new Vector2(cursor.XRespectingGumZoomAndBounds(), cursor.YRespectingGumZoomAndBounds());
        return Vector2.Transform(screenPoint, Matrix.Invert(_camera));
    }

    private bool IsCursorOver(Rectangle worldBounds)
    {
        return worldBounds.Contains(CursorWorldPosition());
    }

    /// <summary>
    /// Works out the terminal view: the zoom and position that scale and center
    /// the monitor's screen into the area above the clipboard, keeping its
    /// aspect ratio.
    /// </summary>
    private void ComputeTerminalView()
    {
        Rectangle screen = _monitor.ScreenBounds;

        float areaWidth = Core.VirtualWidth - TerminalMargin * 2;
        float areaHeight = _clipboard.Top - TerminalMargin * 2;

        Vector2 screenCenter = screen.Center.ToVector2();
        var areaCenter = new Vector2(Core.VirtualWidth / 2f, _clipboard.Top / 2f);
        _terminalZoom = Math.Min(areaWidth / screen.Width, areaHeight / screen.Height);

        // The terminal view maps x to z * (x - screenCenter) + areaCenter. Its
        // fixed point (where that equals x) is the focus to zoom about. Only
        // undefined at z = 1, i.e. if the screen already filled the area.
        _zoomFocus = (_terminalZoom * screenCenter - areaCenter) / (_terminalZoom - 1f);
    }
}
