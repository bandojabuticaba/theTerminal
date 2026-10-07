using ImGuiNET;
using Microsoft.Xna.Framework;

namespace MonoGameLibrary.Debug;

/// <summary>
/// General-purpose debug stats window (FPS today, room for memory/entity counts later).
/// Registers itself with <see cref="DebugOverlay"/> on construction.
/// </summary>
public class StatsPanel : IDebugPanel
{
    private double _elapsedMilliseconds;
    private int _frameCount;
    private int _fps;

    public StatsPanel()
    {
        DebugOverlay.Register(this);
    }

    public void DrawDebug(GameTime gameTime)
    {
        _frameCount++;
        _elapsedMilliseconds += gameTime.ElapsedGameTime.TotalMilliseconds;
        if (_elapsedMilliseconds >= 1000)
        {
            _elapsedMilliseconds -= 1000;
            _fps = _frameCount;
            _frameCount = 0;
        }

        var presentationParameters = Core.GraphicsDevice.PresentationParameters;
        var displayMode = Core.GraphicsDevice.Adapter.CurrentDisplayMode;

        ImGui.Begin("Stats");
        ImGui.Text($"FPS: {_fps}");
        ImGui.Text($"Game resolution: {presentationParameters.BackBufferWidth}x{presentationParameters.BackBufferHeight}");
        ImGui.Text($"Monitor resolution: {displayMode.Width}x{displayMode.Height}");
        ImGui.End();
    }
}
