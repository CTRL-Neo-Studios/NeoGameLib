using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI;

public class NeoPanel : NeoUIElement
{
    public Color Color { get; set; } = Color.White;

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        if (LayoutSize.X <= 0 || LayoutSize.Y <= 0) return;
        NeoUIElement root = this;
        
        while (root.Parent != null)
        {
            root = root.Parent;
        }
        
        if (root is not NeoCanvas canvas || canvas.Bus == null) return;
        spriteBatch.Draw(canvas.Bus.GetWhitePixel(spriteBatch.GraphicsDevice), LayoutPosition, null, Color, 0, Vector2.Zero, LayoutSize, SpriteEffects.None, 0);
    }
}
