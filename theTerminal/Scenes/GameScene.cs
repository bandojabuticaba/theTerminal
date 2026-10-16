using System;
using Gum;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using MonoGameLibrary.Scenes;
using theTerminal.GameObjects;

namespace theTerminal.Scenes;

/// <summary>
/// The main gameplay scene: the workstation where the player collects words
/// into the clipboard and drags them into the terminal.
/// </summary>
/// <remarks>
/// The scene has two modes. In <see cref="Mode.Explore"/> the player sees the
/// whole desk; clicking the monitor's screen switches to
/// <see cref="Mode.Terminal"/>, where a camera zooms the world so the screen
/// fills the area above the clipboard. The zoom is animated both ways; Esc, or clicking outside both the screen
/// and the clipboard, goes back. The clipboard is drawn by Gum in screen space,
/// so the camera never moves it - it stays put in both modes.
/// </remarks>
public class GameScene : Scene
{
    private enum Mode
    {
        Explore,
        Terminal,
    }

    // Space left around the zoomed screen in terminal mode.
    private const float TerminalMargin = 20f;

    // How long a full zoom between explore and terminal takes, in seconds.
    private const float ZoomDuration = 0.5f;

    // The timing curve for the zoom (see MonoGameLibrary.Easing). An Out curve
    // because the player triggers the zoom: it reacts instantly to the click and
    // settles softly. Swap for Easing.OutCubic (gentler) or Easing.OutBack
    // (slight overshoot, a physical "lean in") to compare.
    private static readonly Func<float, float> ZoomEasing = Easing.OutQuart;

    private Clipboard _clipboard;
    private Monitor _monitor;

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
        ComputeTerminalView();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        _monitor = new Monitor(Content);

        // Centered horizontally, with a small top margin above the clipboard frame.
        float x = (Core.VirtualWidth - _monitor.Width) / 2f;
        _monitor.Position = new Vector2(x, 40f);
    }

    public override void Update(GameTime gameTime)
    {
        // Gum first, so the clipboard polls this frame's cursor state.
        GumService.Default.Update(gameTime);
        _clipboard.Update(gameTime);

        switch (_mode)
        {
            case Mode.Explore:
                UpdateExplore();
                break;
            case Mode.Terminal:
                UpdateTerminal();
                break;
        }

        UpdateZoom(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        // Drawn inside the scene so Core's transition overlay covers it.
        // PointClamp keeps the pixel art crisp instead of blurring on the scale-up.
        Core.SpriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera);
        _monitor.Draw(Core.SpriteBatch);
        Core.SpriteBatch.End();

        GumService.Default.Draw();
    }

    public override void UnloadContent()
    {
        _clipboard?.Dispose();

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

        if (GumService.Default.Cursor.PrimaryPush && IsCursorOverScreen())
        {
            SetMode(Mode.Terminal);
        }
    }

    private void UpdateTerminal()
    {
        if (Core.Input.Keyboard.WasKeyJustPressed(Keys.Escape))
        {
            SetMode(Mode.Explore);
            return;
        }

        var cursor = GumService.Default.Cursor;
        if (cursor.PrimaryPush
            && IsCursorInsideGame()
            && !IsCursorOverScreen()
            && !_clipboard.Contains(cursor.XRespectingGumZoomAndBounds(), cursor.YRespectingGumZoomAndBounds()))
        {
            SetMode(Mode.Explore);
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
    /// Whether the cursor is over the monitor's screen, in whichever mode is
    /// active - the cursor is mapped back through the camera into world space,
    /// where the screen bounds live.
    /// </summary>
    private bool IsCursorOverScreen()
    {
        var cursor = GumService.Default.Cursor;
        var screenPoint = new Vector2(cursor.XRespectingGumZoomAndBounds(), cursor.YRespectingGumZoomAndBounds());
        var worldPoint = Vector2.Transform(screenPoint, Matrix.Invert(_camera));

        return _monitor.ScreenBounds.Contains(worldPoint);
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
