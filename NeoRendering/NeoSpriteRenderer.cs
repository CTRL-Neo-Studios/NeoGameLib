using NeoGameLib.Common;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoRendering;

public class NeoSpriteRenderer : NeoComponent
{
    private NeoRenderBus _bus;
    private Texture2D _texture;
    private Color _color = Color.White;
    private float _depth;
    private Vector2 _anchor = new(0.5f);
    private int _order;

    public Texture2D Texture
    {
        get => _texture;
        set => _texture = value;
    }

    // note to self:
    // this is the variable the animator sets to animate frames on a spritesheet. both the bounds and render bus origin follows this variable now so that the collider and anchor wouldn't be affected while the sprite was animated
    // also keep in mind, null = whole texture
    public Rectangle? SourceRectangle { get; set; }

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
    
    // note to self:
    // these are WORLD-space bounds. texture-local bounds are always (0,0,w,h) no matter what so all the window-edge checks below never fired for small sprites.
    // also fuck monogame for not making this clear
    // TODO: currently the bounds only work when rotation is 0, so in the future if sb rotates this then this would need an entirely different approach to calculate bounds
    public Rectangle Bounds
    {
        get
        {
            if (_texture is null) return Rectangle.Empty;

            // bounds follow the current frame (source rect) so a collider doesn't grow
            // to the whole sprite sheet while animating
            Vector2 frameSize = (SourceRectangle?.Size ?? _texture.Bounds.Size).ToVector2();
            Vector2 origin = frameSize * _anchor;
            Vector2 topLeft = ParentTransform.Position - origin * ParentTransform.Scale;
            Vector2 size = frameSize * ParentTransform.Scale;
            return new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)size.X, (int)size.Y);
        }
    }

    public bool WithinViewportBounds
    {
        get
        {
            Viewport viewport = _bus.Gdm.GraphicsDevice.Viewport;
            return Bounds.Left >= 0 && Bounds.Right <= viewport.Width &&
                   Bounds.Top >= 0 && Bounds.Bottom <= viewport.Height;
        }
    }

    public bool VisibleInViewportBounds
    {
        get
        {
            Viewport viewport = _bus.Gdm.GraphicsDevice.Viewport;
            return Bounds.Right >= 0 && Bounds.Left <= viewport.Width &&
                   Bounds.Bottom >= 0 && Bounds.Top <= viewport.Height;
        }
    }

    public NeoSpriteRenderer(NeoRenderBus bus, Texture2D? texture = null)
    {
        _bus = bus;
        _texture = texture;
    }

    public override void OnAwake()
    {
        _bus.AddSprite(this);
    }

    public override void OnDestroy()
    {
        _bus.RemoveSprite(this);
    }

    // note to self:
    // detect whether one of the sprite's bound is out of the window, like the sprite edge going over the window edge
    // e.g. when top edge is outside of the viewport and direction is set to top, returns true
    public virtual bool IsOverWindowBounds(Direction2D dir)
    {
        switch (dir)
        {
            case Direction2D.Left:
                return Bounds.Left < 0;
            case Direction2D.Right:
                return Bounds.Right > _bus.Gdm.GraphicsDevice.Viewport.Width;
            case Direction2D.Down:
                return Bounds.Bottom > _bus.Gdm.GraphicsDevice.Viewport.Height;
            case Direction2D.Up:
                return Bounds.Top < 0;
            default:
                return false;
        }
    }
}
