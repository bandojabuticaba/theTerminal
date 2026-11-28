using System;
using System.Collections.Generic;
using Gum;
using Gum.DataTypes;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;

namespace theTerminal.GameObjects;

/// <summary>
/// The clipboard (the game's word bank): a bordered frame at the bottom of the
/// screen holding the words the player has collected (see <see cref="TryAdd"/>);
/// it starts empty. The frame is divided into a grid of slots so boxes
/// stay loosely aligned, but each chip owns its own slot independently - they
/// don't have to be packed together, and moving one never shifts any other.
/// Dropping a chip onto another's slot swaps the two; the chip being replaced
/// doesn't move until the drop actually happens. A chip dropped outside the
/// frame is offered to <see cref="WordDroppedOutside"/> (the terminal takes it
/// when it's dropped on the screen) and goes back to its own slot either way:
/// the word is copied, not used up, so it can be put in again. The right end
/// of the frame is kept free of chips (<see cref="ButtonArea"/>) for the
/// terminal's buttons.
/// </summary>
/// <remarks>
/// Dragging is driven entirely by <see cref="Update"/> polling the raw cursor
/// state once per frame, rather than Gum's Push/Dragging/LosePush events. With
/// two overlapping HasEvents elements (the dragged chip on top of another chip),
/// those events fired extra, premature LosePush calls mid-gesture - confirmed by
/// logging showing LosePush firing twice for one drag, with PrimaryDown still
/// true the first time. Polling from one place means only one chip can ever be
/// "the one being dragged" at a time, so that class of bug can't happen here.
/// </remarks>
public class Clipboard : IDisposable
{
    private const float Margin = 0f;
    private const int Rows = 5;
    private const float Padding = 6f;
    private const float Gap = 4f;
    private const float ChipWidth = 130f;
    private const float ChipHeight = 20f;

    // Width kept free at the right end of the frame for the terminal's buttons
    // (see ButtonArea). Leaves room for 10 columns of chips at 1440 wide.
    private const float ButtonAreaWidth = 84f;

    private sealed class ChipEntry
    {
        public ContainerRuntime Chip;
        public Word Word;
        public int SlotIndex;
    }

    private readonly List<ChipEntry> _chips = new();

    private RectangleRuntime _frame;
    private int _columns;
    private int _totalSlots;

    private ChipEntry _draggedEntry;
    private float _dragOffsetX;
    private float _dragOffsetY;

    /// <summary>
    /// Called when a chip is dropped outside the frame, with its word and
    /// where the cursor let go, in screen space.
    /// </summary>
    public Action<Word, Vector2> WordDroppedOutside;

    public Clipboard()
    {
        _frame = CreateFrame();
        // The chips get the frame's inside minus the button area and the gap before it.
        _columns = (int)((_frame.Width - Padding * 2 - ButtonAreaWidth) / (ChipWidth + Gap));
        _totalSlots = _columns * Rows;
    }

    /// <summary>
    /// Adds a chip for <paramref name="word"/> in the first free slot. Returns
    /// false when every slot is taken (not handled further yet).
    /// </summary>
    public bool TryAdd(Word word)
    {
        for (int slot = 0; slot < _totalSlots; slot++)
        {
            if (FindBySlot(slot, excluding: null) == null)
            {
                CreateChip(word, slot);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The space at the right end of the frame, inside its padding, where no
    /// chip goes: the terminal's buttons sit here. In screen space.
    /// </summary>
    public Rectangle ButtonArea => new(
        (int)(_frame.X + _frame.Width - Padding - ButtonAreaWidth),
        (int)(_frame.Y + Padding),
        (int)ButtonAreaWidth,
        (int)(_frame.Height - Padding * 2));

    /// <summary>The top edge of the clipboard frame, in screen space.</summary>
    public float Top => _frame.Y;

    /// <summary>Whether a screen-space point falls inside the clipboard frame.</summary>
    public bool Contains(float x, float y)
    {
        return x >= _frame.X && x <= _frame.X + _frame.Width
            && y >= _frame.Y && y <= _frame.Y + _frame.Height;
    }

    public void Update(GameTime gameTime)
    {
        var cursor = GumService.Default.Cursor;
        float cursorX = cursor.XRespectingGumZoomAndBounds();
        float cursorY = cursor.YRespectingGumZoomAndBounds();

        if (_draggedEntry == null)
        {
            if (cursor.PrimaryPush)
            {
                TryStartDrag(cursorX, cursorY);
            }
            return;
        }

        if (cursor.PrimaryDown)
        {
            _draggedEntry.Chip.X = cursorX + _dragOffsetX;
            _draggedEntry.Chip.Y = cursorY + _dragOffsetY;
        }
        else
        {
            EndDrag(cursorX, cursorY);
        }
    }

    /// <summary>
    /// Removes the frame and every chip from Gum's root, so they don't stay on
    /// screen after the scene that owns this clipboard ends.
    /// </summary>
    public void Dispose()
    {
        var root = GumService.Default.Root.Children;
        foreach (var entry in _chips)
        {
            root.Remove(entry.Chip);
        }
        _chips.Clear();
        root.Remove(_frame);
        _draggedEntry = null;
    }

    private void TryStartDrag(float cursorX, float cursorY)
    {
        // Walk back-to-front so the topmost (most recently added) chip wins if
        // any ever overlap.
        for (int i = _chips.Count - 1; i >= 0; i--)
        {
            var entry = _chips[i];
            var chip = entry.Chip;

            bool isOver = cursorX >= chip.X && cursorX <= chip.X + chip.Width
                       && cursorY >= chip.Y && cursorY <= chip.Y + chip.Height;

            if (isOver)
            {
                _draggedEntry = entry;
                _dragOffsetX = chip.X - cursorX;
                _dragOffsetY = chip.Y - cursorY;
                return;
            }
        }
    }

    private void EndDrag(float cursorX, float cursorY)
    {
        if (!Contains(cursorX, cursorY))
        {
            ChipEntry dropped = _draggedEntry;
            _draggedEntry = null;
            ApplySlotPosition(dropped);
            WordDroppedOutside?.Invoke(dropped.Word, new Vector2(cursorX, cursorY));
            return;
        }

        int targetSlot = GetNearestSlotIndex(_draggedEntry.Chip.X, _draggedEntry.Chip.Y);
        var occupant = FindBySlot(targetSlot, excluding: _draggedEntry);

        if (occupant != null)
        {
            // Swap: whoever was in the target slot takes the dragged chip's old one.
            occupant.SlotIndex = _draggedEntry.SlotIndex;
            ApplySlotPosition(occupant);
        }

        _draggedEntry.SlotIndex = targetSlot;
        ApplySlotPosition(_draggedEntry);

        _draggedEntry = null;
    }

    private static RectangleRuntime CreateFrame()
    {
        float height = Rows * ChipHeight + (Rows - 1) * Gap + Padding * 2;

        var frame = new RectangleRuntime(fullInstantiation: true, GumService.Default.SystemManagers)
        {
            IsFilled = true,
            FillColor = new Color(20, 20, 20),
            StrokeColor = Color.Gray,
            StrokeWidth = 2,
            Width = GumService.Default.CanvasWidth - Margin * 2,
            Height = height,
        };
        frame.X = Margin;
        frame.Y = GumService.Default.CanvasHeight - height - Margin;

        GumService.Default.Root.Children.Add(frame);

        return frame;
    }

    /// <summary>The slot position (top-left) for a given slot index in the grid.</summary>
    private (float X, float Y) GetSlotPosition(int slotIndex)
    {
        int row = slotIndex / _columns;
        int column = slotIndex % _columns;

        float x = _frame.X + Padding + column * (ChipWidth + Gap);
        float y = _frame.Y + Padding + row * (ChipHeight + Gap);
        return (x, y);
    }

    /// <summary>Reverses <see cref="GetSlotPosition"/>: which grid slot is closest to a point.</summary>
    private int GetNearestSlotIndex(float x, float y)
    {
        int column = (int)Math.Round((x - _frame.X - Padding) / (ChipWidth + Gap));
        int row = (int)Math.Round((y - _frame.Y - Padding) / (ChipHeight + Gap));

        column = Math.Clamp(column, 0, _columns - 1);
        row = Math.Clamp(row, 0, Rows - 1);

        return Math.Clamp(row * _columns + column, 0, _totalSlots - 1);
    }

    private ChipEntry FindBySlot(int slotIndex, ChipEntry excluding)
    {
        foreach (var entry in _chips)
        {
            if (entry != excluding && entry.SlotIndex == slotIndex)
            {
                return entry;
            }
        }
        return null;
    }

    private void ApplySlotPosition(ChipEntry entry)
    {
        var (x, y) = GetSlotPosition(entry.SlotIndex);
        entry.Chip.X = x;
        entry.Chip.Y = y;
    }

    private void CreateChip(Word word, int slotIndex)
    {
        // A plain runtime container, not a Forms control: no click/press state
        // machinery, no interactivity events - Update() above is solely
        // responsible for moving it. It's just a positioned box with two
        // children drawn inside it.
        var chip = new ContainerRuntime();
        chip.Width = ChipWidth;
        chip.Height = ChipHeight;

        var background = new RectangleRuntime(fullInstantiation: true, GumService.Default.SystemManagers)
        {
            IsFilled = true,
            FillColor = word.Color,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Width = 0,
            Height = 0,
        };
        chip.Children.Add(background);

        var text = new TextRuntime(fullInstantiation: true, GumService.Default.SystemManagers)
        {
            Text = word.Text,
            Color = Color.White,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Width = 0,
            Height = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        chip.Children.Add(text);

        GumService.Default.Root.Children.Add(chip);

        var entry = new ChipEntry { Chip = chip, Word = word, SlotIndex = slotIndex };
        _chips.Add(entry);
        ApplySlotPosition(entry);
    }
}
