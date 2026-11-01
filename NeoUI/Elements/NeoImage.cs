using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoMapping;

namespace NeoGameLib.NeoUI.Elements;

public class NeoImage : NeoUIElement
{
    private Rectangle? _sourceRectangle;
    private Texture2D? _texture;
    private string? _texturePath;

    /// <summary>
    /// The borrowed texture. Resolves TexturePath on first read.
    /// </summary>
    /// <remarks>Assigning a texture in code clears TexturePath, since a live texture can't be written to a file.</remarks>
    public Texture2D? Texture
    {
        get => _texture ??= NeoUIAssets.Load<Texture2D>(_texturePath);
        set { _texture = value; _texturePath = null; }
    }

    /// <summary>Content path of the texture (e.g. "Sprites/gem"), loaded through NeoGame.Assets on first use. This is what UI files store.</summary>
    [NeoAsset(typeof(Texture2D))]
    public string? TexturePath
    {
        get => _texturePath;
        set { _texturePath = value; _texture = null; }
    }
    public Color Color { get; set; } = Color.White;

    public Rectangle? SourceRectangle
    {
        get => _sourceRectangle;
        set
        {
            if (
                value is Rectangle rectangle &&
                (rectangle.X < 0 || 
                 rectangle.Y < 0 || 
                 rectangle.Width < 0 ||
                 rectangle.Height < 0)
            )
                throw new ArgumentOutOfRangeException(nameof(value));
            _sourceRectangle = value;
        }
    }

    /// <summary>The texture's size (or SourceRectangle's), like an html img's intrinsic size. Set Size to override it.</summary>
    protected override Vector2 MeasureContent() => (Texture == null) ? Vector2.Zero : (SourceRectangle?.Size ?? Texture.Bounds.Size).ToVector2();

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        Vector2 size = ScreenTransform.LayoutSize;
        if (Texture == null || size.X <= 0 || size.Y <= 0) return;
        Vector2 sourceSize = (SourceRectangle?.Size ?? Texture.Bounds.Size).ToVector2();
        if (sourceSize.X <= 0 || sourceSize.Y <= 0) return;
        spriteBatch.Draw(
            Texture, 
            ScreenTransform.LayoutPosition, 
            SourceRectangle, 
            Color, 
            0, 
            Vector2.Zero, 
            size / sourceSize, 
            SpriteEffects.None, 
            0
        );
    }
}
