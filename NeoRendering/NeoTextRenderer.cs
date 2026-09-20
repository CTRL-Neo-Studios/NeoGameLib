using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoRendering;

// note to future self:
// draws a string from a SpriteFont. similar to the sprite renderer but for text
public class NeoTextRenderer : NeoComponent
{
    private NeoRenderBus _bus;
    private SpriteFont _font;
    private string _text = "";
    private Color _color = Color.White;
    private float _depth;
    private Vector2 _anchor = new(0.5f);
    private int _order;

    public SpriteFont Font
    {
        get => _font;
        set => _font = value;
    }

    public string Text
    {
        get => _text;
        set => _text = value;
    }

    public Color Color
    {
        get => _color;
        set => _color = value;
    }

    public float Depth
    {
        get => _depth;
        set => _depth = value;
    }

    public Vector2 Anchor
    {
        get => _anchor;
        set => _anchor = value;
    }

    public int Order
    {
        get => _order;
        set => _order = value;
    }

    public NeoTextRenderer(NeoRenderBus bus, SpriteFont? font = null)
    {
        _bus = bus;
        _font = font;
    }

    public override void OnAwake()
    {
        _bus.AddText(this);
    }

    public override void OnDestroy()
    {
        _bus.RemoveText(this);
    }
}
