using Gum;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary;
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
    private Monitor _monitor;

    public override void Initialize()
    {
        base.Initialize();

        _clipboard = new Clipboard();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        _monitor = new Monitor(Content);

        // Centered horizontally, with a small top margin above the clipboard frame.
        float x = (Core.GraphicsDevice.Viewport.Width - _monitor.Width) / 2f;
        _monitor.Position = new Vector2(x, 40f);
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
        // PointClamp keeps the pixel art crisp instead of blurring on the scale-up.
        Core.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _monitor.Draw(Core.SpriteBatch);
        Core.SpriteBatch.End();

        GumService.Default.Draw();
    }

    public override void UnloadContent()
    {
        _clipboard?.Dispose();

        base.UnloadContent();
    }
}
