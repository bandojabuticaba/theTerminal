using System;
using System.Collections.Generic;
using Gum;
using Gum.Converters;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using theTerminal.Dialog;
using theTerminal.GameObjects.Inspectables;

namespace theTerminal.GameObjects;

/// <summary>
/// The panel with more details about an <see cref="Inspectable"/> the player
/// clicked. Prototype layout: a bordered box centered above the clipboard with
/// the object's name and description. Hidden until <see cref="Show"/>.
/// Collectable words in the description ({text:type}, see <see cref="TextMarkup"/>)
/// are drawn as underlined links the player can click (see <see cref="TryGetWordAt"/>).
/// </summary>
/// <remarks>
/// <para>
/// Every size sets its units explicitly - Gum's defaults for TextRuntime
/// aren't plain pixels, which let the text overflow the box. The panel's
/// height follows its children, so it grows to fit longer descriptions.
/// </para>
/// <para>
/// A single TextRuntime draws a paragraph as one block, so one word inside it
/// can't be underlined or clicked. The description is instead a wrapping
/// left-to-right stack of words, each its own small element; the stack's
/// spacing stands in for the spaces between them. Text with no space between
/// it and a link ("{list:command}.") is kept together in one word so it never
/// wraps apart.
/// </para>
/// </remarks>
public class InspectPanel : IDisposable
{
    private const float PanelWidth = 420f;
    private const float Padding = 16f;
    private const float TitleHeight = 24f;

    // Used if the font's space width can't be measured.
    private const float FallbackSpaceWidth = 5f;

    // Link colors, the lighter dark-theme variants of the browser's unvisited
    // blue and visited purple, readable on the panel's near-black background.
    private static readonly Color TextColor = Color.LightGray;
    private static readonly Color LinkColor = new(138, 180, 248);      // #8AB4F8
    private static readonly Color CollectedColor = new(197, 138, 249); // #C58AF9

    private sealed class Link
    {
        public Word Word;
        public ContainerRuntime Area;
        public TextRuntime Text;
        public RectangleRuntime Underline;
    }

    private readonly ContainerRuntime _panel;
    private readonly TextRuntime _title;
    private readonly ContainerRuntime _description;
    private readonly List<Link> _links = new();

    private WordCollection _words;

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

        // Fixed width, so the stack wraps onto a new row when a word doesn't
        // fit; its height follows the rows.
        _description = new ContainerRuntime
        {
            X = Padding,
            Y = Padding + TitleHeight,
            WidthUnits = DimensionUnitType.Absolute,
            Width = PanelWidth - Padding * 2,
            HeightUnits = DimensionUnitType.RelativeToChildren,
            Height = 0,
            ChildrenLayout = ChildrenLayout.LeftToRightStack,
            WrapsChildren = true,
            StackSpacing = MeasureSpaceWidth(),
        };
        _panel.Children.Add(_description);

        GumService.Default.Root.Children.Add(_panel);
    }

    /// <param name="words">Decides whether each link is drawn as available or collected.</param>
    public void Show(Inspectable inspectable, WordCollection words)
    {
        _words = words;
        _title.Text = inspectable.Name;
        BuildDescription(TextMarkup.Parse(inspectable.Description));
        _panel.Visible = true;
    }

    public void Hide()
    {
        _panel.Visible = false;
    }

    /// <summary>The collectable word under a screen-space point, if any.</summary>
    public bool TryGetWordAt(float x, float y, out Word word)
    {
        if (IsOpen)
        {
            foreach (Link link in _links)
            {
                if (IsInside(link.Area, x, y))
                {
                    word = link.Word;
                    return true;
                }
            }
        }

        word = null;
        return false;
    }

    /// <summary>Recolors the links from their current status, e.g. after one was collected.</summary>
    public void RefreshLinks()
    {
        foreach (Link link in _links)
        {
            Color color = _words?.StatusOf(link.Word) == WordStatus.Collected ? CollectedColor : LinkColor;
            link.Text.Color = color;
            link.Underline.FillColor = color;
            link.Underline.StrokeColor = color;
        }
    }

    /// <summary>Whether a screen-space point falls inside the panel.</summary>
    public bool Contains(float x, float y) => IsInside(_panel, x, y);

    /// <summary>Removes the panel from Gum's root, so it doesn't outlive its scene.</summary>
    public void Dispose()
    {
        GumService.Default.Root.Children.Remove(_panel);
    }

    private static bool IsInside(ContainerRuntime element, float x, float y)
    {
        // Absolute* are the laid-out edges, already accounting for the origins,
        // the stack and the height that follows the text.
        return x >= element.AbsoluteLeft && x <= element.AbsoluteRight
            && y >= element.AbsoluteTop && y <= element.AbsoluteBottom;
    }

    /// <summary>
    /// Rebuilds the description's words. Splits on spaces into words; each word
    /// is a small horizontal stack of its pieces (plain text and links), so a
    /// link and the punctuation touching it stay together.
    /// </summary>
    private void BuildDescription(IReadOnlyList<TextSegment> segments)
    {
        _description.Children.Clear();
        _links.Clear();

        ContainerRuntime word = null;

        void EndWord() => word = null;

        void AddPiece(GraphicalUiElement piece)
        {
            if (word == null)
            {
                word = NewStack(spacing: 0f);
                _description.Children.Add(word);
            }
            word.Children.Add(piece);
        }

        foreach (TextSegment segment in segments)
        {
            if (segment.IsWord)
            {
                AddPiece(CreateLink(segment.Word));
                continue;
            }

            string[] parts = segment.Text.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                // Every space in the text ends the current word.
                if (i > 0)
                {
                    EndWord();
                }
                if (parts[i].Length > 0)
                {
                    AddPiece(CreateText(parts[i], TextColor));
                }
            }
        }

        RefreshLinks();
    }

    private ContainerRuntime CreateLink(Word word)
    {
        // Sized to the text; the underline sits on its bottom edge.
        ContainerRuntime area = NewStack(spacing: 0f);
        area.ChildrenLayout = ChildrenLayout.Regular;

        TextRuntime text = CreateText(word.Text, LinkColor);
        area.Children.Add(text);

        var underline = new RectangleRuntime(fullInstantiation: true, GumService.Default.SystemManagers)
        {
            IsFilled = true,
            WidthUnits = DimensionUnitType.RelativeToParent,
            Width = 0,
            HeightUnits = DimensionUnitType.Absolute,
            Height = 1,
            YUnits = GeneralUnitType.PixelsFromLarge,
            YOrigin = VerticalAlignment.Bottom,
            Y = 0,
        };
        area.Children.Add(underline);

        _links.Add(new Link { Word = word, Area = area, Text = text, Underline = underline });
        return area;
    }

    private static ContainerRuntime NewStack(float spacing) => new()
    {
        WidthUnits = DimensionUnitType.RelativeToChildren,
        Width = 0,
        HeightUnits = DimensionUnitType.RelativeToChildren,
        Height = 0,
        ChildrenLayout = ChildrenLayout.LeftToRightStack,
        StackSpacing = spacing,
    };

    private static TextRuntime CreateText(string value, Color color) => new(fullInstantiation: true, GumService.Default.SystemManagers)
    {
        Text = value,
        Color = color,
        WidthUnits = DimensionUnitType.RelativeToChildren,
        Width = 0,
        HeightUnits = DimensionUnitType.RelativeToChildren,
        Height = 0,
    };

    /// <summary>The font's space width: "a a" minus "aa", as laid out by Gum.</summary>
    private static float MeasureSpaceWidth()
    {
        float width = CreateText("a a", TextColor).AbsoluteWidth - CreateText("aa", TextColor).AbsoluteWidth;
        return width > 0f ? width : FallbackSpaceWidth;
    }
}
