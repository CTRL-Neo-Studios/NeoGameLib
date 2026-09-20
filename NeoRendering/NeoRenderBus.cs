using System.Collections.Generic;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoRendering;

// note to self:
// the render pipeline, a.k.a. minecraft-shit-event-bus-architecture-and-rendering-inspired-rendering-pipeline
// or maybe it's the forge modloader event-bus-shit? idk idgaf they're all shit either ways
// so the neo render bus is basically an manager instance for rendering many sprites, i purposefully made it an instance-able object rather than a static class because i cna implement render layers later
// TODO: implement render layers later based on this architecture
// 
public class NeoRenderBus
{
    private GraphicsDeviceManager _gdm;
    private List<NeoSpriteRenderer> _sprites = new();
    private List<NeoTextRenderer> _texts = new(); // Current solution is to just render texts after sprites. there'll probably be cases where text needs to be behind sprites but if that use case comes, come HERE to change it later
    private int _orderCounter = 0;

    private SpriteEffects placeholderEffect; // TODO: FIgure out what this does and properly propogate this to all sprite renderer components

    internal GraphicsDeviceManager Gdm
    {
        get => _gdm;
    }

    public NeoRenderBus(GraphicsDeviceManager graphicsDeviceManager)
    {
        this._gdm = graphicsDeviceManager;
        this._sprites = new();
        this._orderCounter = 0;

        this.placeholderEffect = new SpriteEffects();
    }

    public void AddSprite(NeoSpriteRenderer sprite)
    {
        if (_sprites.Contains(sprite)) return;

        sprite.Order = _orderCounter++;
        _sprites.Add(sprite);
    }

    public void RemoveSprite(NeoSpriteRenderer sprite)
    {
        _sprites.Remove(sprite);
    }

    // same deal as sprites, texts self-register on awake
    public void AddText(NeoTextRenderer text)
    {
        if (_texts.Contains(text)) return;

        text.Order = _orderCounter++;
        _texts.Add(text);
    }

    public void RemoveText(NeoTextRenderer text)
    {
        _texts.Remove(text);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _sprites.Sort(static (a, b) =>
        {
            int tmp = b.Depth.CompareTo(a.Depth);
            return tmp != 0 ? tmp : a.Order.CompareTo(b.Order); // higher depth is sorted first for render otherwise use order
        });

        foreach (NeoSpriteRenderer sp in _sprites)
        {
            if (sp.Texture is null || !sp.Enabled) continue;

            Transform transform = sp.ParentTransform;
            Vector2 frameSize = (sp.SourceRectangle?.Size ?? sp.Texture.Bounds.Size).ToVector2(); // if source rect is present then use source rect for frame size instead of the sprite texture, because source rect is not null when sprite is animated
            Vector2 origin = frameSize * sp.Anchor;
            spriteBatch.Draw(sp.Texture, transform.Position, sp.SourceRectangle, sp.Color, transform.Rotation, origin, transform.Scale, placeholderEffect, sp.Depth);
        }

        // texts are drawn after sprites so ui text sits on top of the world by default,
        // depth still sorts within the text pass if you need text behind other text
        _texts.Sort(static (a, b) =>
        {
            int tmp = b.Depth.CompareTo(a.Depth);
            return tmp != 0 ? tmp : a.Order.CompareTo(b.Order);
        });

        foreach (NeoTextRenderer text in _texts)
        {
            if (text.Font is null || string.IsNullOrEmpty(text.Text) || !text.Enabled) continue;

            Transform transform = text.ParentTransform;
            Vector2 origin = text.Font.MeasureString(text.Text) * text.Anchor;
            spriteBatch.DrawString(text.Font, text.Text, transform.Position, text.Color, transform.Rotation, origin, transform.Scale, SpriteEffects.None, text.Depth);
        }
    }
}
