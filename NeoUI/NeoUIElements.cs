using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI;

// THIS IS TEMPORARY.
// TODO: Move the elements out to separate files

public class NeoPanel : NeoUIElement
{
    public Color Color { get; set; } = Color.White;

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        if (LayoutSize.X <= 0 || LayoutSize.Y <= 0) return;
        NeoUIElement root = this;
        while (root.Parent != null) root = root.Parent;
        if (root is not NeoCanvas canvas || canvas.Bus == null) return;
        spriteBatch.Draw(canvas.Bus.GetWhitePixel(spriteBatch.GraphicsDevice), LayoutPosition, null,
            Color, 0, Vector2.Zero, LayoutSize, SpriteEffects.None, 0);
    }
}

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
        if (Texture == null || LayoutSize.X <= 0 || LayoutSize.Y <= 0) return;
        Vector2 sourceSize = (SourceRectangle?.Size ?? Texture.Bounds.Size).ToVector2();
        if (sourceSize.X <= 0 || sourceSize.Y <= 0) return;
        spriteBatch.Draw(Texture, LayoutPosition, SourceRectangle, Color, 0, Vector2.Zero,
            LayoutSize / sourceSize, SpriteEffects.None, 0);
    }
}

public class NeoLabel : NeoUIElement
{
    /// <summary>Borrowed font; the UI never disposes it or its texture.</summary>
    public SpriteFont? Font { get; set; }
    public string Text { get; set; } = "";
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// Defaults to the font's measured text size.
    /// When false, Size controls layout only; text still draws at its native size without wrapping, scaling or clipping.
    /// A missing font or empty text = 0 when AutoSize is enabled.
    /// </summary>
    public bool AutoSize { get; set; } = true;

    protected override Vector2 MeasureSize() => AutoSize ? (
            (Font != null && !string.IsNullOrEmpty(Text)) ? Font.MeasureString(Text) : Vector2.Zero
        ) : base.MeasureSize();

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        if (Font != null && !string.IsNullOrEmpty(Text))
            spriteBatch.DrawString(Font, Text, LayoutPosition, Color);
    }
}
