using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using MonoGameLibrary.Graphics;

namespace MonoGameLibrary.Debug;

/// <summary>
/// Owns the single ImGui frame (BeforeLayout/AfterLayout) used for all debug UI each draw call.
/// Panels register themselves here instead of driving the ImGui frame on their own, since ImGui
/// only tolerates one NewFrame/Render pair per game frame.
/// </summary>
public static class DebugOverlay
{
    private static readonly HashSet<IDebugPanel> s_panels = new HashSet<IDebugPanel>();

    /// <summary>
    /// Whether the debug overlay is drawn. Set this from code (e.g. Game1) to show/hide it.
    /// </summary>
    public static bool Visible { get; set; }

    public static void Register(IDebugPanel panel)
    {
        s_panels.Add(panel);
    }

    public static void Unregister(IDebugPanel panel)
    {
        s_panels.Remove(panel);
    }

    [Conditional("DEBUG")]
    public static void Draw(GameTime gameTime)
    {
        if (!Visible) return;

        Core.ImGuiRenderer.BeforeLayout(gameTime);

        foreach (var panel in s_panels)
        {
            panel.DrawDebug(gameTime);
        }

        Material.DrawAllVisible();

        Core.ImGuiRenderer.AfterLayout();
    }
}
