using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using theTerminal.GameObjects;

namespace theTerminal.Dialog;

/// <summary>
/// One piece of a parsed line: plain text, or a collectable word when
/// <see cref="Word"/> is set (then <see cref="Text"/> is the word's text).
/// </summary>
public readonly record struct TextSegment(string Text, Word Word)
{
    public bool IsWord => Word is not null;
}

/// <summary>
/// Parses the markup used in dialog and inspect texts. A collectable word is
/// written inline as <c>{text:type}</c>, where type is a <see cref="WordType"/>
/// name (case-insensitive), e.g. "A sticky note reads {list:command}."
/// </summary>
public static partial class TextMarkup
{
    [GeneratedRegex(@"\{([^{}:]+):(\w+)\}")]
    private static partial Regex WordTag();

    public static IReadOnlyList<TextSegment> Parse(string line)
    {
        var segments = new List<TextSegment>();
        if (string.IsNullOrEmpty(line))
        {
            return segments;
        }

        int index = 0;
        foreach (Match match in WordTag().Matches(line))
        {
            if (match.Index > index)
            {
                segments.Add(new TextSegment(line[index..match.Index], null));
            }

            string text = match.Groups[1].Value;
            string typeName = match.Groups[2].Value;

            if (Enum.TryParse(typeName, ignoreCase: true, out WordType type))
            {
                segments.Add(new TextSegment(text, new Word(text, type)));
            }
            else
            {
                // Shown as written, so the typo is visible on screen.
                Debug.WriteLine($"[TextMarkup] Unknown word type '{typeName}' in \"{line}\".");
                segments.Add(new TextSegment(match.Value, null));
            }

            index = match.Index + match.Length;
        }

        if (index < line.Length)
        {
            segments.Add(new TextSegment(line[index..], null));
        }

        return segments;
    }
}
