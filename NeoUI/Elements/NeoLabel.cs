using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI.Elements;

public class NeoLabel : NeoUIElement
{
    public SpriteFont? Font { get; set; }
    public string Text { get; set; } = "";
    public Color Color { get; set; } = Color.White;
    /// <summary>
    /// tries to fit on both axes by default via the measured text size. Set the size mode to Fixed to make Size attr come to effect instead.
    /// </summary>
    /// <remarks>Text still draws at its native size without wrapping, scaling, or clipping.</remarks>
    public NeoLabel()
    {
        ScreenTransform.WidthMode = NeoUISizing.Fit;
        ScreenTransform.HeightMode = NeoUISizing.Fit;
    }

    protected override Vector2 MeasureContent() => (Font != null && !string.IsNullOrEmpty(Text)) ? 
        Font.MeasureString(Text) : Vector2.Zero;

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        if (Font != null && !string.IsNullOrEmpty(Text))
        {
            spriteBatch.DrawString(
                Font, 
                Text, 
                ScreenTransform.LayoutPosition, 
                Color
            );
        }
    }
}
