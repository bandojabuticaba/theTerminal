using System.Collections.Generic;

namespace theTerminal.Shell;

/// <summary>A file or directory in the <see cref="FileSystem"/>.</summary>
public abstract class FileSystemNode
{
    public string Name { get; }

    /// <summary>The directory this is in; null for the root.</summary>
    public DirectoryNode Parent { get; private set; }

    protected FileSystemNode(string name)
    {
        Name = name;
    }

    internal void SetParent(DirectoryNode parent) => Parent = parent;
}

/// <summary>A file: a name and its text.</summary>
public sealed class FileNode : FileSystemNode
{
    public string Content { get; }

    public FileNode(string name, string content) : base(name)
    {
        Content = content;
    }
}

/// <summary>A directory: the files and directories in it, in the order they were added.</summary>
public sealed class DirectoryNode : FileSystemNode
{
    private readonly List<FileSystemNode> _children = new();

    public IReadOnlyList<FileSystemNode> Children => _children;

    public DirectoryNode(string name) : base(name)
    {
    }

    public void Add(FileSystemNode child)
    {
        child.SetParent(this);
        _children.Add(child);
    }

    /// <summary>The directory directly inside this one with that name, or null.</summary>
    public DirectoryNode FindDirectory(string name)
    {
        foreach (FileSystemNode child in _children)
        {
            if (child is DirectoryNode directory && child.Name == name)
            {
                return directory;
            }
        }
        return null;
    }
}
