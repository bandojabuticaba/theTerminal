using Gum;
using Gum.Forms;
using MonoGameLibrary;
using MonoGameLibrary.Debug;
using theTerminal.Scenes;

namespace theTerminal;

public class Game1 : Core
{
    public Game1() : base("theTerminal", 1280, 720, false)
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

        GumService.Default.Initialize(this, DefaultVisualsVersion.V3);

        ChangeScene(new GameScene());
    }
}
