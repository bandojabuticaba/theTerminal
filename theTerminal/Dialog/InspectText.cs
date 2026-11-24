using System;
using System.Collections.Generic;

namespace theTerminal.Dialog;

/// <summary>
/// What the details panel shows for one <see cref="GameObjects.Inspectables.Inspectable"/>:
/// its title and the lines shown on successive clicks. Loaded from a stage's
/// inspect file by <see cref="InspectTextFile"/>.
/// </summary>
public sealed record InspectText(string Name, IReadOnlyList<string> Lines)
{
    /// <summary>
    /// The line for the given click: the first click shows the first line, and
    /// so on, with the last line repeating once they run out.
    /// </summary>
    /// <param name="timesInspected">How many times it has been clicked, counting this click (1 or more).</param>
    public string LineFor(int timesInspected)
    {
        if (Lines is null || Lines.Count == 0)
        {
            return string.Empty;
        }

        int index = Math.Clamp(timesInspected - 1, 0, Lines.Count - 1);
        return Lines[index];
    }

    /// <summary>A placeholder shown in place of an ID with no entry, so the gap is obvious on screen.</summary>
    public static InspectText Missing(string id)
    {
        string marker = $"[missing: {id}]";
        return new InspectText(marker, [marker]);
    }
}
