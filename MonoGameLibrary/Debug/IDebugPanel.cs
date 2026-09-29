using Microsoft.Xna.Framework;

namespace MonoGameLibrary.Debug;

/// <summary>
/// A panel that draws itself into the shared ImGui debug frame owned by <see cref="DebugOverlay"/>.
/// </summary>
public interface IDebugPanel
{
    void DrawDebug(GameTime gameTime);
}
