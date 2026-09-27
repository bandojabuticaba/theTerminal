using System;
using System.IO;
using Microsoft.Xna.Framework.Content;

namespace MonoGameLibrary.Content;

/// <summary>
/// Wraps a content asset loaded from a <see cref="ContentManager"/> so it can be
/// hot-reloaded from disk while the game is running.
/// </summary>
public class WatchedAsset<T> where T : class
{
    /// <summary>
    /// Gets the currently loaded instance of the asset.
    /// </summary>
    public T Asset { get; private set; }

    /// <summary>
    /// Gets the content path (relative to the content manager's root directory,
    /// without extension) that this asset was loaded from.
    /// </summary>
    public string AssetName { get; }

    /// <summary>
    /// Gets the content manager that owns this asset.
    /// </summary>
    public ContentManager Owner { get; }

    /// <summary>
    /// Gets the last write time of the source .xnb file at the point the currently
    /// loaded <see cref="Asset"/> was read from it.
    /// </summary>
    /// <remarks>
    /// This is the timestamp of the content build that produced the loaded instance,
    /// not the wall clock time at which it was loaded into memory.
    /// </remarks>
    public DateTime UpdatedAt { get; private set; }

    internal WatchedAsset(ContentManager owner, string assetName, T asset)
    {
        Owner = owner;
        AssetName = assetName;
        Asset = asset;
        UpdatedAt = GetXnbLastWriteTime();
    }

    /// <summary>
    /// Checks if the .xnb file backing this asset on disk is newer than the
    /// currently loaded instance, and if so, reloads it.
    /// </summary>
    /// <param name="oldAsset">The previously loaded asset instance, if a reload occurred; otherwise null.</param>
    /// <returns>true if the asset was reloaded; otherwise false.</returns>
    public bool TryRefresh(out T oldAsset)
    {
        oldAsset = null;

        DateTime lastWriteTime = GetXnbLastWriteTime();
        if (lastWriteTime <= UpdatedAt)
        {
            return false;
        }

        if (ContentManagerExtensions.IsFileLocked(GetXnbPath()))
        {
            // The content builder is still writing this file; try again next frame.
            return false;
        }

        oldAsset = Asset;

        Owner.UnloadAsset(AssetName);
        Asset = Owner.Load<T>(AssetName);
        UpdatedAt = lastWriteTime;

        return true;
    }

    private string GetXnbPath()
    {
        // MonoGame's own content loading resolves relative to the executable's
        // directory (AppContext.BaseDirectory), not the process's current
        // working directory - which varies depending on how the game is
        // launched (dotnet run inherits the invoking shell's cwd). Match that
        // resolution here so the file exists/timestamp checks are reliable
        // regardless of launch method.
        return Path.Combine(AppContext.BaseDirectory, Owner.RootDirectory, AssetName + ".xnb");
    }

    private DateTime GetXnbLastWriteTime()
    {
        string path = GetXnbPath();
        return File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.MinValue;
    }
}
