using System.Collections.Generic;
using NeoGameLib.Common;
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
// cameras registers themselves on awake like every other bus item. On render, cameras are stacked in Order with CullingMask filtering sprite/text Layers. If there are no enabled cameras in the world, the bus defaults to identity pass, a.k.a the pre-camera behavior.
public class NeoRenderBus
{
    private GraphicsDeviceManager _gdm;
    private List<NeoSpriteRenderer> _sprites = new();
    private List<NeoTextRenderer> _texts = new(); // texts render after sprites so they sit on top of the world; screen-space UI is drawn by NeoUI in its own pass, not here
    private List<NeoCamera> _cameras = new();
    private int _orderCounter = 0;
    private long _cameraRegistrationCounter = 0;
    private bool _cameraOrderDirty;

    internal GraphicsDeviceManager Gdm
    {
        get => _gdm;
    }

    /// <summary>
    /// Shorthand for GraphicsDeviceManager.GraphicsDevice.Viewport.
    /// </summary>
    public Viewport GraphicsViewport => _gdm.GraphicsDevice.Viewport;

    /// <summary>
    /// every registered camera in render order including the disabled cameras
    /// </summary>
    /// <remarks>you won't be using this. ever. But if you do, make sure you skip your logic with the disabled cameras.</remarks>
    public IReadOnlyList<NeoCamera> Cameras
    {
        get
        {
            EnsureCameraOrder();
            return _cameras;
        }
    }

    /// <summary>
    /// the first enabled camera by the camera-set-order-param, highest order + enabled wins
    /// </summary>
    public NeoCamera MainCamera
    {
        get
        {
            EnsureCameraOrder();
            foreach (NeoCamera cam in _cameras)
                if (cam.Enabled) return cam;
            return null;
        }
    }

    public bool HasAnyCamera
    {
        get
        {
            foreach (NeoCamera cam in _cameras)
                if (cam.Enabled) return true;
            return false;
        }
    }

    // the main camera's view matrix, or identity when no camera is enabled
    public Matrix ViewMatrix
    {
        get
        {
            NeoCamera cam = MainCamera;
            return (cam is null) ? Matrix.Identity : cam.GetViewMatrix(_gdm.GraphicsDevice.Viewport);
        }
    }

    // window pixel conversions through the main camera, identity when no camera is enabled
    public Vector2 WorldToScreen(Vector2 world)
    {
        NeoCamera cam = MainCamera;
        return (cam is null) ? world : cam.WorldToScreen(world, GraphicsViewport) + new Vector2(GraphicsViewport.X, GraphicsViewport.Y);
    }

    public Vector2 ScreenToWorld(Vector2 screen)
    {
        NeoCamera cam = MainCamera;
        return (cam is null) ? screen : cam.ScreenToWorld(screen - new Vector2(GraphicsViewport.X, GraphicsViewport.Y), GraphicsViewport);
    }

    internal void RegisterCamera(NeoCamera camera)
    {
        if (_cameras.Contains(camera)) return;

        camera.RegOrder = _cameraRegistrationCounter++;
        _cameras.Add(camera);
        _cameraOrderDirty = true;
    }

    internal void UnregisterCamera(NeoCamera camera)
    {
        _cameras.Remove(camera);
    }

    internal void InvalidateCameraOrder() => _cameraOrderDirty = true;

    private void EnsureCameraOrder()
    {
        if (!_cameraOrderDirty) return;

        _cameraOrderDirty = false;
        _cameras.Sort(static (a, b) =>
        {
            int tmp = a.Order.CompareTo(b.Order);
            return tmp != 0 ? tmp : a.RegOrder.CompareTo(b.RegOrder);
        });
    }

    public NeoRenderBus(GraphicsDeviceManager graphicsDeviceManager)
    {
        this._gdm = graphicsDeviceManager;
        this._sprites = new();
        this._orderCounter = 0;
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

    /// <summary>
    /// draws everything (the no-camera default fallback)
    /// </summary>
    /// <remarks>Identity pass</remarks>
    public void Draw(SpriteBatch spriteBatch)
    {
        DrawSprites(spriteBatch, null);
        DrawTexts(spriteBatch, null);
    }

    /// <summary>
    /// Draws only the sprite/text layers that the camera's cullingMask selects.
    /// </summary>
    /// <remarks>The Camera Pass</remarks>
    public void Draw(SpriteBatch spriteBatch, NeoCamera camera)
    {
        int mask = camera.CullingMask;
        DrawSprites(spriteBatch, mask);
        DrawTexts(spriteBatch, mask);
    }

    /// <summary>
    /// Draws sprite batch sprites in the render-stacking order.
    /// </summary>
    /// <remarks>
    /// sorts per camera pass; sort once per frame into per-camera buckets if sprite counts grow
    /// </remarks>
    private void DrawSprites(SpriteBatch spriteBatch, int? mask)
    {
        _sprites.Sort(static (a, b) =>
        {
            int tmp = b.Depth.CompareTo(a.Depth);
            return tmp != 0 ? tmp : a.Order.CompareTo(b.Order); // higher depth is sorted first for render otherwise use order
        });

        using PassSnapshot<NeoSpriteRenderer> sprites = new(_sprites);
        for (int s = 0; s < sprites.Count; s++)
        {
            NeoSpriteRenderer sp = sprites[s];
            if (!sp.IsAttached || sp.Texture is null || !sp.Enabled) continue;
            if (mask.HasValue && (sp.Layer & mask.Value) == 0) continue;

            Transform transform = sp.ParentTransform;
            Vector2 frameSize = (sp.SourceRectangle?.Size ?? sp.Texture.Bounds.Size).ToVector2(); // if source rect is present then use source rect for frame size instead of the sprite texture, because source rect is not null when sprite is animated
            Vector2 origin = frameSize * sp.Anchor, scale = transform.Scale;
            SpriteEffects effects = ResolveFlip(frameSize, ref origin, ref scale);
            NeoSpriteDrawInfo info = new(sp.Texture, sp.SourceRectangle, transform.Position, sp.Color, transform.Rotation, origin, scale, effects, sp.Depth);
            using PassSnapshot<NeoSpriteRenderFeature> features = new(sp.Features);

            // features draw in submission order: behind layers, the sprite, then front layers
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i].IsAttached && features[i].Enabled)
                    features[i].OnDrawBehind(spriteBatch, info);
            }

            if (!sp.IsAttached) continue;
            info.Draw(spriteBatch, sp.Texture, sp.Color);

            for (int i = 0; i < features.Count; i++)
            {
                if (features[i].IsAttached && features[i].Enabled)
                    features[i].OnDrawFront(spriteBatch, info);
            }
        }
    }

    /// <summary>
    /// Draws text.
    /// </summary>
    private void DrawTexts(SpriteBatch spriteBatch, int? mask)
    {
        _texts.Sort(static (a, b) =>
        {
            int tmp = b.Depth.CompareTo(a.Depth);
            return tmp != 0 ? tmp : a.Order.CompareTo(b.Order);
        });

        using PassSnapshot<NeoTextRenderer> texts = new(_texts);
        for (int t = 0; t < texts.Count; t++)
        {
            NeoTextRenderer text = texts[t];
            if (!text.IsAttached || text.Font is null || string.IsNullOrEmpty(text.Text) || !text.Enabled) continue;
            if (mask.HasValue && (text.Layer & mask.Value) == 0) continue;

            Transform transform = text.ParentTransform;
            Vector2 size = text.Font.MeasureString(text.Text),
                origin = size * text.Anchor,
                scale = transform.Scale;
            SpriteEffects effects = ResolveFlip(size, ref origin, ref scale);
            spriteBatch.DrawString(text.Font, text.Text, transform.Position, text.Color, transform.Rotation, origin, scale, effects, text.Depth);
        }
    }

    /// <summary>
    /// <para>SpriteBatch builds the sprite rendering quad from `size * scale`, so a negative scale flips the quad's winding and the default `RasterizerState.CullCounterClockwise` culls it. Making both of the axes negative would make the thing flip the winding twice, which is why `(-1, -1)` still works somehow.</para>
    /// <para>the render bus makes the scale absolute and converts the negative/positive meaning to `SpriteEffects.FlipHorizontally` / `FlipVertically` instead. SpriteEffects only mirrors the pixels inside the same box and that the sprite origin mirrors (`size - origin`) to put the box on the other side of the position as well. both the sprites and texts go through the same render process with this sprite effect.</para>
    /// </summary>
    /// <returns>SpriteEffects post-processing</returns>
    private static SpriteEffects ResolveFlip(Vector2 size, ref Vector2 origin, ref Vector2 scale)
    {
        SpriteEffects effects = SpriteEffects.None;

        if (scale.X < 0f)
        {
            effects |= SpriteEffects.FlipHorizontally;
            origin.X = size.X - origin.X;
            scale.X = -scale.X;
        }

        if (scale.Y < 0f)
        {
            effects |= SpriteEffects.FlipVertically;
            origin.Y = size.Y - origin.Y;
            scale.Y = -scale.Y;
        }

        return effects;
    }
}
