using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI.Elements;

public class NeoImage : NeoUIElement
{
    private Rectangle? _sourceRectangle;

    /// <summary>the borrowed texture</summary>
    public Texture2D? Texture { get; set; }
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
