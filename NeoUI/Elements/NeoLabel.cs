using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI.Elements;

public class NeoLabel : NeoUIElement
{
    public SpriteFont? Font { get; set; }
    public string Text { get; set; } = "";
    public Color Color { get; set; } = Color.White;

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
