using Gum;
using Gum.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using MonoGameLibrary.Debug;
using theTerminal.UI;

namespace theTerminal;

public class Game1 : Core
{
    private WordBankPanel _wordBankPanel;

    public Game1() : base("theTerminal", 1280, 720, false)
    {
    }

    protected override void Initialize()
    {
        base.Initialize();

        new StatsPanel();

        // Flip this to false to hide the debug overlay.
        DebugOverlay.Visible = true;

        GumService.Default.Initialize(this, DefaultVisualsVersion.V3);
        _wordBankPanel = new WordBankPanel();
    }

    protected override void LoadContent()
    {
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        GumService.Default.Update(gameTime);
        _wordBankPanel.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);

        GumService.Default.Draw();
    }

}
