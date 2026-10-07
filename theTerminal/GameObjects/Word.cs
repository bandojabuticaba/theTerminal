using Microsoft.Xna.Framework;

namespace theTerminal.GameObjects;

/// <summary>
/// The different kinds of words the player can collect. Drives the color its
/// chip is drawn with in the clipboard (and, later, the terminal).
/// </summary>
public enum WordType
{
    Command,
    Parameter,
    File,
    Directory,
}

/// <summary>
/// A single word the player has collected: its text, what kind of thing it
/// represents, and the color its chip is drawn with.
/// </summary>
public class Word
{
    public string Text { get; }
    public WordType Type { get; }
    public Color Color => GetColor(Type);

    public Word(string text, WordType type)
    {
        Text = text;
        Type = type;
    }

    private static Color GetColor(WordType type) => type switch
    {
        WordType.Command => Color.Blue,
        WordType.Parameter => Color.Orange,
        WordType.File => Color.Green,
        WordType.Directory => Color.Purple,
        _ => Color.Gray,
    };
}
