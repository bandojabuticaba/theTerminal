using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameLibrary.Graphics;

/// <summary>
/// A sprite sheet exported by TexturePacker's MonoGame exporter: the texture,
/// plus a .txt with one sprite per line. Unlike <see cref="TextureAtlas"/>, the
/// sprites can be trimmed and rotated (see <see cref="PackedSprite"/>), so
/// TexturePacker's own output loads as is, without a hand-written definition.
/// </summary>
public class TexturePackerSheet
{
    private readonly Dictionary<string, PackedSprite> _sprites = new();

    /// <summary>The texture the sprites are packed into.</summary>
    public Texture2D Texture { get; }

    private TexturePackerSheet(Texture2D texture)
    {
        Texture = texture;
    }

    /// <summary>Gets the sprite exported from the image at <paramref name="name"/> (its path without extension, e.g. "chair/turn-1").</summary>
    public PackedSprite GetSprite(string name)
    {
        return _sprites[name];
    }

    /// <summary>
    /// Loads a sheet from its .txt, relative to the content root. The texture is
    /// the content asset of the same name (e.g. "images/room.txt" loads "images/room").
    /// </summary>
    public static TexturePackerSheet FromFile(ContentManager content, string fileName)
    {
        string texturePath = Path.ChangeExtension(fileName, null);
        var sheet = new TexturePackerSheet(content.Load<Texture2D>(texturePath));

        string filePath = Path.Combine(content.RootDirectory, fileName);

        using Stream stream = TitleContainer.OpenStream(filePath);
        using var reader = new StreamReader(stream);

        // Each line is:
        //   name;rotated;x;y;width;height;originalWidth;originalHeight;pivotX;pivotY
        // x, y, width and height are the region in the sheet, as stored (so
        // already turned if rotated). The pivot is the original image's center,
        // in fractions of the trimmed sprite's upright size, which is how the
        // exporter records where the trimming cut from.
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(';');
            string name = fields[0];
            bool isRotated = fields[1] == "1";
            int x = int.Parse(fields[2], CultureInfo.InvariantCulture);
            int y = int.Parse(fields[3], CultureInfo.InvariantCulture);
            int width = int.Parse(fields[4], CultureInfo.InvariantCulture);
            int height = int.Parse(fields[5], CultureInfo.InvariantCulture);
            int originalWidth = int.Parse(fields[6], CultureInfo.InvariantCulture);
            int originalHeight = int.Parse(fields[7], CultureInfo.InvariantCulture);
            float pivotX = float.Parse(fields[8], CultureInfo.InvariantCulture);
            float pivotY = float.Parse(fields[9], CultureInfo.InvariantCulture);

            int uprightWidth = isRotated ? height : width;
            int uprightHeight = isRotated ? width : height;
            var offset = new Vector2(
                MathF.Round(originalWidth / 2f - pivotX * uprightWidth),
                MathF.Round(originalHeight / 2f - pivotY * uprightHeight));

            var region = new TextureRegion(sheet.Texture, x, y, width, height);
            sheet._sprites.Add(name, new PackedSprite(region, isRotated, offset));
        }

        return sheet;
    }
}
