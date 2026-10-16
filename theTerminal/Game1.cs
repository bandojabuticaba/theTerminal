using Gum;
using Gum.Forms;
using Microsoft.Xna.Framework;
using MonoGameLibrary;
using MonoGameLibrary.Debug;
using theTerminal.Scenes;

namespace theTerminal;

public class Game1 : Core
{
    public Game1() : base("theTerminal", 1440, 810, false)
    {
    }

    protected override void Initialize()
    {
        base.Initialize();

        new StatsPanel();

        // Flip this to false to hide the debug overlay.
        DebugOverlay.Visible = true;

        // Flip this to true to bring back the fade/wipe scene transition.
        UseSceneTransition = false;

        // Flip this to false to disable the F key fullscreen toggle.
        ToggleFullScreenOnF = true;

        // GameScene owns Esc: it leaves terminal mode, and only quits from explore mode.
        ExitOnEscape = false;

        // The game always lays out at 1440x810 and is scaled to fit the window or
        // the fullscreen display, with black bars where the shape doesn't match.
        UseVirtualResolution = true;

        GumService.Default.Initialize(this, DefaultVisualsVersion.V3);

        // Gum takes its canvas from the back buffer at startup; pin it to the
        // virtual resolution so it never follows the real window size.
        GumService.Default.CanvasWidth = VirtualWidth;
        GumService.Default.CanvasHeight = VirtualHeight;

        ChangeScene(new GameScene());
    }

    protected override void Update(GameTime gameTime)
    {
        // Map the raw mouse position into virtual coordinates before the scene
        // updates Gum. Recomputed every frame since the window size can change
        // (fullscreen toggle).
        GumService.Default.Cursor.TransformMatrix = ScreenToVirtual;

        base.Update(gameTime);
    }
}
