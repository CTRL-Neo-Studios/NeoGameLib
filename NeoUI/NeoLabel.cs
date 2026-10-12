using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI;

public class NeoLabel : NeoUIElement
{
    public SpriteFont? Font { get; set; }
    public string Text { get; set; } = "";
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// Defaults to the font's measured text size.
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
