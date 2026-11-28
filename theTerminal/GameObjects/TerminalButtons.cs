using System;
using Gum;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals.V3;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;

namespace theTerminal.GameObjects;

/// <summary>
/// The terminal's Enter and Backspace buttons, stacked in the space the
/// clipboard keeps free at its right end (see <see cref="Clipboard.ButtonArea"/>).
/// Gum Forms buttons: they aren't dragged, so their own Click event is safe
/// here (unlike the clipboard's chips, see <see cref="Clipboard"/>).
/// </summary>
public class TerminalButtons : IDisposable
{
    private const float Gap = 4f;

    private static readonly Color BackgroundColor = new(60, 60, 60);
    private static readonly Color TextColor = Color.White;

    // Gum's default text is a little big for the narrow button area:
    // "Backspace" would wrap onto two lines at full size.
    private const float TextScale = 0.8f;

    private readonly Button _enter;
    private readonly Button _backspace;

    /// <summary>Called when Enter is clicked.</summary>
    public Action EnterClicked;

    /// <summary>Called when Backspace is clicked.</summary>
    public Action BackspaceClicked;

    /// <summary>
    /// Whether the buttons can be clicked. Disabled ones are drawn grayed out
    /// and ignore clicks.
    /// </summary>
    public bool IsEnabled
    {
        get => _enter.IsEnabled;
        set
        {
            _enter.IsEnabled = value;
            _backspace.IsEnabled = value;
        }
    }

    /// <param name="area">Where the buttons go, in screen space: Enter on top, Backspace below.</param>
    public TerminalButtons(Rectangle area)
    {
        float height = (area.Height - Gap) / 2f;

        _enter = CreateButton("Enter", area.X, area.Y, area.Width, height);
        _enter.Click += (_, _) => EnterClicked?.Invoke();

        _backspace = CreateButton("Backspace", area.X, area.Y + height + Gap, area.Width, height);
        _backspace.Click += (_, _) => BackspaceClicked?.Invoke();
    }

    /// <summary>Removes the buttons from Gum's root, so they don't outlive their scene.</summary>
    public void Dispose()
    {
        GumService.Default.Root.Children.Remove(_enter.Visual);
        GumService.Default.Root.Children.Remove(_backspace.Visual);
    }

    private static Button CreateButton(string text, float x, float y, float width, float height)
    {
        var button = new Button { Text = text };

        // Units set explicitly: pixels from the canvas's top left, and a size
        // in pixels, whatever the visual's defaults are.
        button.Visual.XUnits = GeneralUnitType.PixelsFromSmall;
        button.Visual.YUnits = GeneralUnitType.PixelsFromSmall;
        button.Visual.XOrigin = HorizontalAlignment.Left;
        button.Visual.YOrigin = VerticalAlignment.Top;
        button.Visual.WidthUnits = DimensionUnitType.Absolute;
        button.Visual.HeightUnits = DimensionUnitType.Absolute;
        button.Visual.X = x;
        button.Visual.Y = y;
        button.Visual.Width = width;
        button.Visual.Height = height;

        var visual = (ButtonVisual)button.Visual;
        visual.BackgroundColor = BackgroundColor;
        visual.ForegroundColor = TextColor;
        visual.TextInstance.FontScale = TextScale;

        GumService.Default.Root.Children.Add(button.Visual);
        return button;
    }
}
