using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI.Elements;

public class NeoPanel : NeoUIElement
{
    public Color Color { get; set; } = Color.White;

    public NeoPadding Padding { get; set; }

    private protected override NeoPadding ContentPadding => Padding;

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        Vector2 size = ScreenTransform.LayoutSize;
        if (
            size.X <= 0 || 
            size.Y <= 0 || 
            Color.A == 0 || 
            Canvas is not { Bus: not null } canvas
        ) return;
        
        spriteBatch.Draw(
            canvas.Bus.GetWhitePixel(spriteBatch.GraphicsDevice), 
            ScreenTransform.LayoutPosition, 
            null, 
            Color, 
            0, 
            Vector2.Zero, 
            size, 
            SpriteEffects.None, 
            0
        );
    }
}
