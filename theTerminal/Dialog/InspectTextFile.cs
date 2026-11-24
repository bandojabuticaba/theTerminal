using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Content;
using MonoGameLibrary.Content;

namespace theTerminal.Dialog;

/// <summary>
/// The inspect texts of one stage, loaded from
/// <c>dialog/&lt;language&gt;/inspect/&lt;stage&gt;.json</c> (copied to the
/// output as-is, not built into an .xnb). Keys are local to the stage, so two
/// stages can each have a "mouse"; two objects in the same stage just use two
/// keys (e.g. "mouse.desk" and "mouse.broken").
/// </summary>
/// <remarks>
/// A missing file or key doesn't crash: the panel shows "[missing: stage.key]"
/// and Debug builds log a warning, so a typo is obvious on screen.
/// </remarks>
public sealed class InspectTextFile
{
    /// <summary>The language folder texts are read from.</summary>
    public const string Language = "en";

    // Comments and trailing commas are allowed so the files are comfortable to
    // edit by hand.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _stage;
    private readonly Dictionary<string, InspectText> _texts;

    private InspectTextFile(string stage, Dictionary<string, InspectText> texts)
    {
        _stage = stage;
        _texts = texts;
    }

    /// <param name="stage">The stage's file name without extension, e.g. "neoroom".</param>
    public static InspectTextFile Load(ContentManager content, string stage)
    {
        string path = $"dialog/{Language}/inspect/{stage}.json";
        Dictionary<string, InspectText> texts;

        try
        {
            // content.OpenStream, not TitleContainer.OpenStream directly: it
            // works around a stream bug on the native backends (see its docs).
            using Stream stream = content.OpenStream(path);
            texts = JsonSerializer.Deserialize<Dictionary<string, InspectText>>(stream, Options) ?? new();
        }
        catch (IOException)
        {
            // File (or folder) not found. A malformed file still throws
            // JsonException on purpose: that's a bug to fix, not a gap to show.
            Debug.WriteLine($"[InspectTextFile] {path} not found; every text in stage '{stage}' will show as missing.");
            texts = new();
        }

        return new InspectTextFile(stage, texts);
    }

    /// <summary>The text for <paramref name="key"/>, or a "[missing: ...]" placeholder.</summary>
    public InspectText Get(string key)
    {
        if (_texts.TryGetValue(key, out InspectText text))
        {
            return text;
        }

        string id = $"{_stage}.{key}";
        Debug.WriteLine($"[InspectTextFile] No inspect text for '{id}'.");
        return InspectText.Missing(id);
    }
}
