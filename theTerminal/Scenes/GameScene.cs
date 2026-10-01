using Gum;
using Microsoft.Xna.Framework;
using MonoGameLibrary.Scenes;
using theTerminal.GameObjects;

namespace theTerminal.Scenes;

/// <summary>
/// The main gameplay scene: the workstation where the player collects words
/// into the clipboard and drags them into the terminal.
/// </summary>
public class GameScene : Scene
{
    private Clipboard _clipboard;

    public override void Initialize()
    {
        base.Initialize();

        _clipboard = new Clipboard();
    }

    public override void Update(GameTime gameTime)
    {
        // Gum first, so the clipboard polls this frame's cursor state.
        GumService.Default.Update(gameTime);
        _clipboard.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        // Drawn inside the scene so Core's transition overlay covers it.
        GumService.Default.Draw();
    }

    public override void UnloadContent()
    {
        _clipboard?.Dispose();

        base.UnloadContent();
    }
}
