using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoMapping;

namespace NeoGameLib.NeoUI.Elements;

public class NeoLabel : NeoUIElement
{
    private SpriteFont? _font;
    private string? _fontPath;

    /// <summary>
    /// The font. Resolves FontPath on first read; null with no FontPath means the built-in Inter font is used.
    /// </summary>
    /// <remarks>Assigning a font in code clears FontPath, since a live font can't be written to a file.</remarks>
    public SpriteFont? Font
    {
        get => _font ??= NeoUIAssets.Load<SpriteFont>(_fontPath);
        set { _font = value; _fontPath = null; }
    }

    /// <summary>Content path of the font (e.g. "fonts/ui"), loaded through NeoGame.Assets on first use. This is what UI files store.</summary>
    [NeoAsset(typeof(SpriteFont))]
    public string? FontPath
    {
        get => _fontPath;
        set { _fontPath = value; _font = null; }
    }

    public string Text { get; set; } = "";
    public Color Color { get; set; } = Color.White;

    private SpriteFont? ActiveFont => Font ?? NeoBuiltinContent.DefaultFont;

    protected override Vector2 MeasureContent() => (ActiveFont is { } font && !string.IsNullOrEmpty(Text)) ? 
        font.MeasureString(Text) : Vector2.Zero;

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        if (ActiveFont is { } font && !string.IsNullOrEmpty(Text))
        {
            spriteBatch.DrawString(
                font, 
                Text, 
                ScreenTransform.LayoutPosition, 
                Color
            );
        }
    }
}
