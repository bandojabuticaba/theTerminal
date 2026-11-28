using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Content;
using MonoGameLibrary.Content;

namespace theTerminal.Shell;

/// <summary>
/// The files on a stage's computer, kept in memory, and the directory the
/// player is in. Loaded from <c>filesystem/&lt;stage&gt;.json</c> (copied to
/// the output as-is, not built into an .xnb), where the tree is written as it
/// looks: an object is a directory, a string is a file and its text.
/// </summary>
/// <remarks>
/// A missing file doesn't crash: the computer is just empty, and Debug builds
/// log a warning. A malformed one still throws, as that's a bug to fix.
/// </remarks>
public sealed class FileSystem
{
    // Comments and trailing commas are allowed so the files are comfortable to
    // edit by hand (like the inspect texts).
    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The top directory, "/".</summary>
    public DirectoryNode Root { get; }

    /// <summary>The directory the player is in, which commands act on.</summary>
    public DirectoryNode CurrentDirectory { get; set; }

    private FileSystem(DirectoryNode root)
    {
        Root = root;
        CurrentDirectory = root;
    }

    /// <param name="stage">The stage's file name without extension, e.g. "neoroom".</param>
    public static FileSystem Load(ContentManager content, string stage)
    {
        string path = $"filesystem/{stage}.json";

        try
        {
            // content.OpenStream, not TitleContainer.OpenStream directly: it
            // works around a stream bug on the native backends (see its docs).
            using Stream stream = content.OpenStream(path);
            using JsonDocument document = JsonDocument.Parse(stream, Options);
            JsonElement json = document.RootElement;

            var fileSystem = new FileSystem(ReadDirectory("", json.GetProperty("root")));

            if (json.TryGetProperty("start", out JsonElement start))
            {
                fileSystem.CurrentDirectory = fileSystem.FindDirectory(start.GetString())
                    ?? throw new InvalidDataException($"{path}: start directory '{start.GetString()}' doesn't exist.");
            }
            return fileSystem;
        }
        catch (IOException)
        {
            // File (or folder) not found. A malformed file throws JsonException
            // or InvalidDataException instead, on purpose.
            Debug.WriteLine($"[FileSystem] {path} not found; stage '{stage}' has an empty computer.");
            return new FileSystem(new DirectoryNode(""));
        }
    }

    /// <summary>
    /// The directory at an absolute path ("/", "/secrets", "/a/b"), or null if
    /// there's no such directory.
    /// </summary>
    public DirectoryNode FindDirectory(string path)
    {
        DirectoryNode directory = Root;
        foreach (string name in path.Split('/', System.StringSplitOptions.RemoveEmptyEntries))
        {
            directory = directory.FindDirectory(name);
            if (directory == null)
            {
                return null;
            }
        }
        return directory;
    }

    private static DirectoryNode ReadDirectory(string name, JsonElement json)
    {
        var directory = new DirectoryNode(name);
        foreach (JsonProperty entry in json.EnumerateObject())
        {
            directory.Add(entry.Value.ValueKind switch
            {
                JsonValueKind.Object => ReadDirectory(entry.Name, entry.Value),
                JsonValueKind.String => new FileNode(entry.Name, entry.Value.GetString()),
                _ => throw new InvalidDataException(
                    $"'{entry.Name}' must be an object (a directory) or a string (a file), not {entry.Value.ValueKind}."),
            });
        }
        return directory;
    }
}
