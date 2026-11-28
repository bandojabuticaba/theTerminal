using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace theTerminal.GameObjects;

/// <summary>
/// The shell on the monitor's screen: the lines written so far, then the
/// prompt with the words put in so far and a blinking cursor. The player
/// doesn't type: the input is a sequence of <see cref="Word"/>s dropped from
/// the clipboard, and the terminal types each one out a letter at a time (and
/// erases a removed one the same way, from its end).
/// Long lines wrap at the screen's edge (the input between words), and once
/// the screen is full the oldest lines scroll off the top.
/// </summary>
/// <remarks>
/// The terminal draws into its own render target, the size the screen has on
/// the canvas in terminal mode. The scene then maps that texture onto the
/// screen's four corners through its zoom camera, so the text zooms with the
/// monitor, stays sharp when fully zoomed in, and follows the screen's
/// perspective in the angled frames of the turn. Shader effects for the whole
/// screen (CRT scanlines, glow) can go on that one final draw later.
/// <para>
/// The mapping is a homography: the projective transform that takes the
/// texture's square to any four-cornered shape, the way a flat surface looks
/// in perspective. It's the quad's world matrix, with its divisor in w, so the
/// GPU's own perspective division (and perspective-correct texture
/// interpolation) does the rest - a stock BasicEffect, no custom shader. Two
/// plain textured triangles instead would bend the text along their diagonal.
/// </para>
/// </remarks>
public class Terminal : IDisposable
{
    // What the shell shows before the cursor.
    private const string Prompt = "$ ";

    // How long the cursor stays on, then off, in seconds.
    private const float BlinkInterval = 0.5f;

    // Space between the screen's edge and the text, in render target pixels.
    private const float Padding = 16f;

    private static readonly Color TextColor = new(51, 255, 102);

    // How many lines are kept; older ones are dropped.
    private const int MaxHistory = 500;

    // Typing speed: seconds between letters as the terminal types out a
    // dropped word, or erases a removed one. Higher is slower.
    private const float LetterDelay = 0.09f;

    // Where one row of the input starts in the input text, and how many of its
    // characters it holds.
    private readonly record struct InputRow(int Start, int Length);

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly BasicEffect _effect;

    // A single white pixel, stretched and tinted to draw the cursor.
    private readonly Texture2D _pixel;

    // The cursor block, relative to the top left of its character cell: as
    // tall as a capital letter, sitting on the baseline, and one character
    // wide - a bash-style block that lines up with the text.
    private readonly Rectangle _cursorCell;

    // The terminal at full size, then halved again and again: a hand-made mip
    // chain. In the angled view the whole screen is only a few dozen pixels
    // across, and sampling the full-size text straight down to that skips most
    // of its pixels (it all but vanishes, and sparkles as it moves). Each level
    // averages 2x2 pixels of the one before, and the draw picks the one closest
    // to the screen's size. Not RenderTarget2D's own mipmaps: DesktopVK leaves
    // them empty.
    private readonly List<RenderTarget2D> _levels = new();

    // Stop halving at about this width; the screen is never drawn smaller.
    private const int SmallestLevelWidth = 16;

    // The texture's unit square, as a triangle strip. The world matrix (see
    // SquareToQuad) moves its corners onto the screen's.
    private static readonly VertexPositionTexture[] Square =
    [
        new(new Vector3(0f, 0f, 0f), new Vector2(0f, 0f)),
        new(new Vector3(1f, 0f, 0f), new Vector2(1f, 0f)),
        new(new Vector3(0f, 1f, 0f), new Vector2(0f, 1f)),
        new(new Vector3(1f, 1f, 0f), new Vector2(1f, 1f)),
    ];

    // What's been printed: earlier commands (with their prompt) and output.
    private readonly List<string> _history = new();

    // How many characters fit across the screen, and lines down it, and the
    // widest character's width, which sets how many fit.
    private readonly int _columns;
    private readonly int _rows;
    private readonly float _charWidth;

    private readonly List<Word> _input = new();

    // The input words as they read after the prompt, with a space between
    // each, and what's on screen so far. The screen catches up with the input
    // one letter per keypress (see Update): typing while it's a beginning of
    // the input, erasing from its end while it isn't (a word was removed).
    private string _inputText = "";
    private string _shown = "";
    private float _letterTimer;

    private float _blinkTimer;
    private bool _cursorVisible = true;

    /// <summary>The words put in after the prompt so far, in order.</summary>
    public IReadOnlyList<Word> Input => _input;

    /// <param name="size">The render target's size: the screen's size on the canvas in terminal mode.</param>
    public Terminal(ContentManager content, GraphicsDevice graphicsDevice, Point size)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _font = content.Load<SpriteFont>("fonts/terminal");

        var levelSize = new Point(Math.Max(size.X, 1), Math.Max(size.Y, 1));
        while (true)
        {
            _levels.Add(new RenderTarget2D(graphicsDevice, levelSize.X, levelSize.Y));
            if (levelSize.X / 2 < SmallestLevelWidth || levelSize.Y < 2)
            {
                break;
            }
            levelSize = new Point(levelSize.X / 2, levelSize.Y / 2);
        }

        _effect = new BasicEffect(graphicsDevice) { TextureEnabled = true };

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        // Measured from the font's "H": Cropping.Y is how far below the top of
        // the line its top is, and the glyph's own height reaches the baseline.
        SpriteFont.Glyph capital = _font.GetGlyphs()['H'];
        _cursorCell = new Rectangle(0, capital.Cropping.Y,
            (int)MathF.Round(_font.MeasureString("M").X), capital.BoundsInTexture.Height);

        // The widest character, so a full row never runs off the screen. The
        // font is monospaced, but the baked SpriteFont rounds each character's
        // step on its own, so they differ slightly (see Render).
        _charWidth = Enumerable.Range(32, 126 - 32 + 1).Max(c => _font.MeasureString(((char)c).ToString()).X);
        _columns = Math.Max(1, (int)((_levels[0].Width - Padding * 2) / _charWidth));
        _rows = Math.Max(1, (int)((_levels[0].Height - Padding * 2) / _font.LineSpacing));
    }

    /// <summary>Prints text (one or more lines) above the prompt.</summary>
    public void WriteLine(string text = "")
    {
        foreach (string line in text.Split('\n'))
        {
            _history.Add(line);
        }

        if (_history.Count > MaxHistory)
        {
            _history.RemoveRange(0, _history.Count - MaxHistory);
        }
    }

    /// <summary>
    /// Adds a word at the end of the input. It's typed out a letter at a time
    /// (see <see cref="Update"/>), after anything still being typed.
    /// </summary>
    public void AddWord(Word word)
    {
        StartKeypressesIfIdle();
        _input.Add(word);
        _inputText = string.Join(" ", _input.Select(w => w.Text));
    }

    /// <summary>
    /// Enters the command: the whole input stays on screen after the prompt
    /// (even the part not typed out yet), the way the shell echoes it, and a
    /// new, empty prompt starts on the next line. Returns the command's words.
    /// </summary>
    public IReadOnlyList<Word> Submit()
    {
        Word[] command = [.. _input];
        WriteLine(Prompt + _inputText);

        _input.Clear();
        _inputText = "";
        _shown = "";
        ShowCursor();
        return command;
    }

    /// <summary>
    /// Takes the last word off the input, if there is one. Whatever of it is
    /// on screen is erased a letter at a time from the end, with the space
    /// before it, like holding backspace (see <see cref="Update"/>).
    /// </summary>
    public void RemoveLastWord()
    {
        if (_input.Count > 0)
        {
            StartKeypressesIfIdle();
            _input.RemoveAt(_input.Count - 1);
            _inputText = string.Join(" ", _input.Select(w => w.Text));
        }
        ShowCursor();
    }

    /// <summary>
    /// When the screen has caught up with the input, makes the next keypress
    /// happen straight away, so a drop or a backspace answers at once.
    /// </summary>
    private void StartKeypressesIfIdle()
    {
        if (_shown == _inputText)
        {
            _letterTimer = LetterDelay;
        }
    }

    /// <summary>
    /// One keypress toward the input: types its next letter while the screen
    /// shows a beginning of it, or erases the last letter on screen while it
    /// doesn't (a removed word still showing). Where a keyboard sound goes.
    /// </summary>
    private void PressKey()
    {
        _shown = _inputText.StartsWith(_shown, StringComparison.Ordinal)
            ? _inputText[..(_shown.Length + 1)]
            : _shown[..^1];
        ShowCursor();
    }

    /// <summary>
    /// Lights the cursor and restarts its blink, the way real terminals keep
    /// it lit while typing.
    /// </summary>
    private void ShowCursor()
    {
        _cursorVisible = true;
        _blinkTimer = 0f;
    }

    public void Update(GameTime gameTime)
    {
        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_shown != _inputText)
        {
            _letterTimer += elapsed;
            while (_letterTimer >= LetterDelay && _shown != _inputText)
            {
                _letterTimer -= LetterDelay;
                PressKey();
            }
        }

        _blinkTimer += elapsed;
        if (_blinkTimer >= BlinkInterval)
        {
            _blinkTimer -= BlinkInterval;
            _cursorVisible = !_cursorVisible;
        }
    }

    /// <summary>
    /// Redraws the terminal and its smaller copies. Call it before anything
    /// else is drawn this frame: it switches render targets and restores the
    /// previous ones, whose contents may be lost in the switch.
    /// </summary>
    public void Render()
    {
        RenderTargetBinding[] previous = _graphicsDevice.GetRenderTargets();
        _graphicsDevice.SetRenderTarget(_levels[0]);

        // Transparent, so the screen's own glass shows behind the text.
        _graphicsDevice.Clear(Color.Transparent);

        // Only the last screenful of rows: the prompt sits at the bottom once
        // the text fills the screen, and the rest has scrolled off the top.
        List<string> rows = VisibleRows();

        // The cursor goes where the last row ends, measured rather than its
        // length times a character's width: the baked font's characters aren't
        // all exactly as wide, and the difference adds up along a row.
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        for (int i = 0; i < rows.Count; i++)
        {
            _spriteBatch.DrawString(_font, rows[i], new Vector2(Padding, Padding + i * _font.LineSpacing), TextColor);
        }
        if (_cursorVisible)
        {
            Rectangle cursor = _cursorCell;
            cursor.Offset(
                (int)MathF.Round(Padding + _font.MeasureString(rows[^1]).X),
                (int)(Padding + (rows.Count - 1) * _font.LineSpacing));
            _spriteBatch.Draw(_pixel, cursor, TextColor);
        }
        _spriteBatch.End();

        // Each level from the one before, at exactly half size, so linear
        // sampling lands between 4 texels and averages them.
        for (int i = 1; i < _levels.Count; i++)
        {
            _graphicsDevice.SetRenderTarget(_levels[i]);
            _graphicsDevice.Clear(Color.Transparent);
            _spriteBatch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.LinearClamp);
            _spriteBatch.Draw(_levels[i - 1], _levels[i].Bounds, Color.White);
            _spriteBatch.End();
        }

        _graphicsDevice.SetRenderTargets(previous);
    }

    /// <summary>
    /// The rows on screen, top to bottom: the end of the history, then the
    /// prompt line as typed out so far, all wrapped at the screen's width. The
    /// cursor goes at the end of the last row.
    /// </summary>
    private List<string> VisibleRows()
    {
        List<string> rows = PromptRows();

        // From the bottom up, stopping once the screen is full, so a long
        // history isn't wrapped every frame just to be scrolled off.
        for (int i = _history.Count - 1; i >= 0 && rows.Count < _rows; i--)
        {
            rows.InsertRange(0, Wrap(_history[i]));
        }

        if (rows.Count > _rows)
        {
            rows.RemoveRange(0, rows.Count - _rows);
        }
        return rows;
    }

    /// <summary>
    /// The prompt and the input as far as it's on screen. The rows are laid
    /// out for whole words first - the input while it's being typed, what's
    /// on screen while it's being erased - so a word that won't fit starts on
    /// the next row from its first letter instead of jumping there halfway
    /// through; then each row shows only its part that's on screen.
    /// </summary>
    private List<string> PromptRows()
    {
        var rows = new List<string>();
        string text = _inputText.StartsWith(_shown, StringComparison.Ordinal) ? _inputText : _shown;
        List<InputRow> layout = LayOutInput(text);

        for (int i = 0; i < layout.Count; i++)
        {
            InputRow row = layout[i];

            // A row with nothing on screen yet isn't shown (the first always
            // is: it holds the prompt).
            if (i > 0 && row.Start >= _shown.Length)
            {
                break;
            }

            int shown = Math.Clamp(_shown.Length - row.Start, 0, row.Length);
            rows.Add((i == 0 ? Prompt : "") + text.Substring(row.Start, shown));
        }

        if (rows[^1].Length >= _columns)
        {
            // The cursor itself wraps onto the next row.
            rows.Add("");
        }
        return rows;
    }

    /// <summary>
    /// Splits input text (words with a space between each) into rows, between
    /// words: a word that doesn't fit on the row starts the next one, and the
    /// space before it is left out (only a word longer than a whole row is
    /// cut). The first row has the prompt in front of it.
    /// </summary>
    private List<InputRow> LayOutInput(string text)
    {
        var layout = new List<InputRow>();
        int rowStart = 0;
        int column = Prompt.Length;
        int position = 0;

        string[] words = text.Length == 0 ? [] : text.Split(' ');
        for (int w = 0; w < words.Length; w++)
        {
            string word = words[w];
            bool first = w == 0;
            int separator = first ? 0 : 1;

            if (!first && column + separator + word.Length > _columns)
            {
                // Next row, skipping the space in front of the word.
                layout.Add(new InputRow(rowStart, position - rowStart));
                position += separator;
                rowStart = position;
                column = 0;
            }
            else
            {
                position += separator;
                column += separator;
            }

            // A word longer than the rest of the row is cut wherever it reaches the edge.
            int remaining = word.Length;
            while (column + remaining > _columns && column < _columns)
            {
                int fits = _columns - column;
                position += fits;
                remaining -= fits;
                layout.Add(new InputRow(rowStart, position - rowStart));
                rowStart = position;
                column = 0;
            }
            position += remaining;
            column += remaining;
        }

        layout.Add(new InputRow(rowStart, position - rowStart));
        return layout;
    }

    /// <summary>Cuts a line into rows of at most the screen's width (an empty line is one empty row).</summary>
    private List<string> Wrap(string line)
    {
        var rows = new List<string>();
        for (int start = 0; start < line.Length; start += _columns)
        {
            rows.Add(line.Substring(start, Math.Min(_columns, line.Length - start)));
        }
        if (rows.Count == 0)
        {
            rows.Add("");
        }
        return rows;
    }

    /// <summary>
    /// Draws the terminal onto the screen, stretched in perspective to its
    /// corners.
    /// </summary>
    /// <param name="corners">The screen's corners in world space: top left, top right, bottom right, bottom left.</param>
    /// <param name="view">The scene's camera, from world space to the canvas.</param>
    /// <param name="opacity">How opaque it's drawn, from 0 (invisible) to 1.</param>
    public void Draw(Vector2[] corners, Matrix view, float opacity = 1f)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        _effect.Texture = LevelFor(corners, view);
        _effect.World = SquareToQuad(corners[0], corners[1], corners[2], corners[3]);
        _effect.View = view;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, 0f, 1f);

        // The texture is premultiplied, so fading scales every channel.
        _effect.DiffuseColor = new Vector3(opacity);
        _effect.Alpha = opacity;

        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, Square, 0, 2);
        }
    }

    /// <summary>
    /// The smallest level still at least as wide as the screen is drawn on the
    /// canvas (its longer horizontal edge, the near one in perspective), so the
    /// text is only ever scaled down by less than half.
    /// </summary>
    private RenderTarget2D LevelFor(Vector2[] corners, Matrix view)
    {
        float top = Vector2.Distance(Vector2.Transform(corners[0], view), Vector2.Transform(corners[1], view));
        float bottom = Vector2.Distance(Vector2.Transform(corners[3], view), Vector2.Transform(corners[2], view));
        float width = Math.Max(top, bottom);

        for (int i = _levels.Count - 1; i > 0; i--)
        {
            if (_levels[i].Width >= width)
            {
                return _levels[i];
            }
        }
        return _levels[0];
    }

    /// <summary>
    /// The homography taking the unit square's corners (0,0), (1,0), (1,1),
    /// (0,1) to p0, p1, p2, p3, as a world matrix: a point (u, v) lands on
    /// x = (a u + b v + c) / w and y = (d u + e v + f) / w, with
    /// w = g u + h v + 1. When the quad is a parallelogram g = h = 0 and
    /// it's just an affine stretch. (Heckbert's square-to-quad mapping.)
    /// </summary>
    private static Matrix SquareToQuad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float dx1 = p1.X - p2.X, dx2 = p3.X - p2.X, dx3 = p0.X - p1.X + p2.X - p3.X;
        float dy1 = p1.Y - p2.Y, dy2 = p3.Y - p2.Y, dy3 = p0.Y - p1.Y + p2.Y - p3.Y;

        float denominator = dx1 * dy2 - dx2 * dy1;
        float g = (dx3 * dy2 - dx2 * dy3) / denominator;
        float h = (dx1 * dy3 - dx3 * dy1) / denominator;

        float a = p1.X - p0.X + g * p1.X, b = p3.X - p0.X + h * p3.X, c = p0.X;
        float d = p1.Y - p0.Y + g * p1.Y, e = p3.Y - p0.Y + h * p3.Y, f = p0.Y;

        // MonoGame multiplies row vectors: (u, v, 0, 1) * M = (x w, y w, 0, w).
        return new Matrix(
            a, d, 0f, g,
            b, e, 0f, h,
            0f, 0f, 1f, 0f,
            c, f, 0f, 1f);
    }

    public void Dispose()
    {
        _effect.Dispose();
        _pixel.Dispose();
        foreach (RenderTarget2D level in _levels)
        {
            level.Dispose();
        }
        _spriteBatch.Dispose();
    }
}
