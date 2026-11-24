using System.Collections.Generic;

namespace theTerminal.GameObjects;

/// <summary>Where a word stands for the player.</summary>
public enum WordStatus
{
    /// <summary>Not taken yet: shown as a link the player can click.</summary>
    Available,

    /// <summary>
    /// Taken (copied to the clipboard). Stays this way even after the word is
    /// used or leaves the clipboard, so it can never be collected twice.
    /// </summary>
    Collected,
}

/// <summary>
/// Which words the player has collected - the single source of truth for a
/// word's <see cref="WordStatus"/>. A word is identified by its text and type,
/// so the same word found on two objects counts once.
/// </summary>
/// <remarks>
/// Kept in memory for now. This is the state to persist later with MonoGame's
/// StorageContainer; the UI only talks to this class, so it won't change then.
/// </remarks>
public class WordCollection
{
    private readonly HashSet<Word> _collected = new();

    public WordStatus StatusOf(Word word) => _collected.Contains(word) ? WordStatus.Collected : WordStatus.Available;

    /// <summary>Marks the word collected. Returns false if it already was.</summary>
    public bool Collect(Word word) => _collected.Add(word);
}
