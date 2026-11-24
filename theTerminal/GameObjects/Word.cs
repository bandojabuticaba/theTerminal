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
/// A single word the player can collect: its text, what kind of thing it
/// represents, and the color its chip is drawn with. A record, so two words
/// with the same text and type are equal (see <see cref="WordCollection"/>).
/// </summary>
public record Word(string Text, WordType Type)
{
    public Color Color => GetColor(Type);

    private static Color GetColor(WordType type) => type switch
    {
        WordType.Command => Color.Blue,
        WordType.Parameter => Color.Orange,
        WordType.File => Color.Green,
        WordType.Directory => Color.Purple,
        _ => Color.Gray,
    };
}
