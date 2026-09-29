using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using MonoGameLibrary.Debug;

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
    }

    protected override void LoadContent()
    {
        base.LoadContent();
    }
    
}
