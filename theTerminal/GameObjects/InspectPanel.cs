using System;
using Gum;
using Gum.DataTypes;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using theTerminal.GameObjects.Inspectables;

namespace theTerminal.GameObjects;

/// <summary>
/// The panel with more details about an <see cref="Inspectable"/> the player
/// clicked. Prototype layout: a bordered box centered above the clipboard with
/// the object's name and description. Hidden until <see cref="Show"/>.
/// </summary>
/// <remarks>
/// Every size sets its units explicitly - Gum's defaults for TextRuntime
/// aren't plain pixels, which let the text overflow the box. The panel's
/// height follows its children, so it grows to fit longer descriptions.
/// </remarks>
public class InspectPanel : IDisposable
{
    private const float PanelWidth = 420f;
    private const float Padding = 16f;
    private const float TitleHeight = 24f;

    private readonly ContainerRuntime _panel;
    private readonly TextRuntime _title;
    private readonly TextRuntime _description;

    public bool IsOpen => _panel.Visible;

    /// <param name="areaHeight">Height of the area to center the panel in, from the top of the canvas.</param>
    public InspectPanel(float areaHeight)
    {
        // Centered on its position (the origins), so it stays centered in the
        // area however tall the description makes it. Height is the children's
        // extent plus Padding, i.e. padding below the description.
        _panel = new ContainerRuntime
        {
            WidthUnits = DimensionUnitType.Absolute,
            Width = PanelWidth,
            HeightUnits = DimensionUnitType.RelativeToChildren,
            Height = Padding,
            XOrigin = HorizontalAlignment.Center,
            YOrigin = VerticalAlignment.Center,
            X = GumService.Default.CanvasWidth / 2f,
            Y = areaHeight / 2f,
            Visible = false,
        };

        var background = new RectangleRuntime(fullInstantiation: true, GumService.Default.SystemManagers)
        {
            IsFilled = true,
            FillColor = new Color(20, 20, 20),
            StrokeColor = Color.Gray,
            StrokeWidth = 2,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Width = 0,
            Height = 0,
        };
        _panel.Children.Add(background);

        _title = new TextRuntime(fullInstantiation: true, GumService.Default.SystemManagers)
        {
            Color = Color.White,
            X = Padding,
            Y = Padding,
            WidthUnits = DimensionUnitType.Absolute,
            Width = PanelWidth - Padding * 2,
            HeightUnits = DimensionUnitType.Absolute,
            Height = TitleHeight,
        };
        _panel.Children.Add(_title);

        // A fixed width makes Gum wrap the description onto several lines; its
        // height follows the wrapped text.
        _description = new TextRuntime(fullInstantiation: true, GumService.Default.SystemManagers)
        {
            Color = Color.LightGray,
            X = Padding,
            Y = Padding + TitleHeight,
            WidthUnits = DimensionUnitType.Absolute,
            Width = PanelWidth - Padding * 2,
            HeightUnits = DimensionUnitType.RelativeToChildren,
            Height = 0,
        };
        _panel.Children.Add(_description);

        GumService.Default.Root.Children.Add(_panel);
    }

    public void Show(Inspectable inspectable)
    {
        _title.Text = inspectable.Name;
        _description.Text = inspectable.Description;
        _panel.Visible = true;
    }

    public void Hide()
    {
        _panel.Visible = false;
    }

    /// <summary>Whether a screen-space point falls inside the panel.</summary>
    public bool Contains(float x, float y)
    {
        // Absolute* are the laid-out edges, already accounting for the origins
        // and the height that follows the text.
        return x >= _panel.AbsoluteLeft && x <= _panel.AbsoluteRight
            && y >= _panel.AbsoluteTop && y <= _panel.AbsoluteBottom;
    }

    /// <summary>Removes the panel from Gum's root, so it doesn't outlive its scene.</summary>
    public void Dispose()
    {
        GumService.Default.Root.Children.Remove(_panel);
    }
}
