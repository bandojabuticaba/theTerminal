using System;
using System.Collections.Generic;
using System.Linq;
using theTerminal.GameObjects;

namespace theTerminal.Shell;

/// <summary>
/// Runs the commands entered in the terminal against the stage's
/// <see cref="FileSystem"/>, and gives back what they print. The first word is
/// the command; the rest are its arguments.
/// </summary>
/// <remarks>
/// Commands so far:
/// <list type="bullet">
/// <item><c>list</c> - the contents of the current directory, one per line,
/// sorted by name, directories marked with a trailing "/". An empty directory
/// prints nothing, like <c>ls</c>. Arguments are ignored for now.</item>
/// </list>
/// Anything else prints "&lt;word&gt;: command not found", like bash.
/// </remarks>
public sealed class CommandInterpreter
{
    private readonly FileSystem _fileSystem;

    public CommandInterpreter(FileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>Runs a command and returns the lines it prints (none for an empty command).</summary>
    public IEnumerable<string> Execute(IReadOnlyList<Word> words)
    {
        if (words.Count == 0)
        {
            return [];
        }

        string command = words[0].Text;
        return command switch
        {
            "list" => List(),
            _ => [$"{command}: command not found"],
        };
    }

    private IEnumerable<string> List()
    {
        return _fileSystem.CurrentDirectory.Children
            .OrderBy(node => node.Name, StringComparer.Ordinal)
            .Select(node => node is DirectoryNode ? node.Name + "/" : node.Name);
    }
}
