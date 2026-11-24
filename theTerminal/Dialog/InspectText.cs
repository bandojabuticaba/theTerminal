namespace theTerminal.Dialog;

/// <summary>
/// What the details panel shows for one <see cref="GameObjects.Inspectables.Inspectable"/>:
/// its title and its text. Loaded from a stage's inspect file by
/// <see cref="InspectTextFile"/>.
/// </summary>
public sealed record InspectText(string Name, string Text)
{
    /// <summary>A placeholder shown in place of an ID with no entry, so the gap is obvious on screen.</summary>
    public static InspectText Missing(string id)
    {
        string marker = $"[missing: {id}]";
        return new InspectText(marker, marker);
    }
}
