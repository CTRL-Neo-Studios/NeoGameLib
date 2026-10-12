using System;
using System.Collections.Generic;
using NeoGameLib.Common;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace NeoGameLib.NeoRendering;

public class NeoSpriteRenderer : NeoComponent
{
    private NeoRenderBus _bus;
    private Texture2D _texture;
    private Color _color = Color.White;
    private float _depth;
    private Vector2 _anchor = new(0.5f);
    private int _order;

    // the render features on this object, in add order. NeoSpriteRenderFeature adds and removes itself
    internal readonly List<NeoSpriteRenderFeature> Features = new();

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

    /// <summary>
    /// this is a bitmask layer for camera culling.
    /// </summary>
    /// <remarks>
    /// <para>So bits. we know bits, right? 1 = 0001, 2 = 0010, 4 = 0100, and so on.</para>
    /// <para>This is basically a bitmask. So, cameras only renders this object when their masks share one bit.</para>
    /// <para>For example: Camera 1 (<c>0000 0101</c> Renders default + Layer #2), Camera 2 (<c>0000 0001</c> Renders ONLY default)</para>
    /// <para>Object A (<c>0000 0001</c> At Layer Default), Object B (<c>0000 0100</c> At Layer #2)</para>
    /// <para>Get the idea yet? So, basically, when (Layer &amp; camera.cullingMask) != 0, which is when (camera 1)<c>0000 0101</c> is compared with (object A)<c>0000 0001</c> and that at least one bit overlaps, the object is rendered by the camera.</para>
    /// </remarks>
    /// <example>
    /// <para>Setting the object's render layer as Layer #2 would be: <c>Layer = 1 &lt;&lt; 2;</c></para>
    /// <para>To layers 3 and 7: <c>Layer = (1 &lt;&lt; 3) | (1 &lt;&lt; 7);</c></para>
    /// </example>
    public int Layer { get; set; } = 1;
    
    // note to self:
    // these are WORLD-space bounds. texture-local bounds are always (0,0,w,h) no matter what so all the window-edge checks below never fired for small sprites.
    // also fuck monogame for not making this clear
    // TODO: currently the bounds only work when rotation is 0, so in the future if sb rotates this then this would need an entirely different approach to calculate bounds
    public Rectangle Bounds
    {
        get
        {
            if (_texture is null) return Rectangle.Empty;

            // bounds follow the current frame (source rect) so a collider doesn't grow to the whole sprite sheet while animating
            Vector2 frameSize = (SourceRectangle?.Size ?? _texture.Bounds.Size).ToVector2(), origin = frameSize * _anchor;

            // the bounds are now an absolute positive rectangle by deriving it from the min/max of the two transformed frame corners. `NeoBoxCollider` uses this absolute scale values for size as well
            Vector2 a = ParentTransform.Position - origin * ParentTransform.Scale, b = a + frameSize * ParentTransform.Scale;
            Vector2 topLeft = Vector2.Min(a, b), size = Vector2.Max(a, b) - topLeft;
            return new Rectangle((int) topLeft.X, (int) topLeft.Y, (int) size.X, (int) size.Y);
        }
    }

    /// <summary>
    /// The screen-space box of this sprite, after the main camera projection.
    /// </summary>
    /// <remarks>
    /// <para>This is used for viewport checks and camera-culling. Bounds stays in world space, and the rectangle bound is constructed via the four corners of the frame.</para>
    /// </remarks>
    public Rectangle ScreenBounds
    {
        get
        {
            if (_texture is null) return Rectangle.Empty;

            Vector2 frameSize = (SourceRectangle?.Size ?? _texture.Bounds.Size).ToVector2();
            if (frameSize.X <= 0f || frameSize.Y <= 0f) return Rectangle.Empty;

            Transform t = ParentTransform;
            if (t.Scale.X == 0f || t.Scale.Y == 0f) return Rectangle.Empty;

            Vector2 c00 = LocalToScreen(Vector2.Zero);
            Vector2 c10 = LocalToScreen(new Vector2(frameSize.X, 0f));
            Vector2 c01 = LocalToScreen(new Vector2(0f, frameSize.Y));
            Vector2 c11 = LocalToScreen(frameSize);

            float minX = MathF.Min(MathF.Min(c00.X, c10.X), MathF.Min(c01.X, c11.X));
            float maxX = MathF.Max(MathF.Max(c00.X, c10.X), MathF.Max(c01.X, c11.X));
            float minY = MathF.Min(MathF.Min(c00.Y, c10.Y), MathF.Min(c01.Y, c11.Y));
            float maxY = MathF.Max(MathF.Max(c00.Y, c10.Y), MathF.Max(c01.Y, c11.Y));

            return new Rectangle((int)MathF.Floor(minX), (int)MathF.Floor(minY),
                (int)MathF.Ceiling(maxX - minX), (int)MathF.Ceiling(maxY - minY));
        }
    }

    /// <summary>
    /// True when the full sprite is inside the viewport, in screen space. Follows the camera.
    /// </summary>
    public bool WithinViewportBounds
    {
        get
        {
            Viewport viewport = _bus.Gdm.GraphicsDevice.Viewport;
            Rectangle bounds = ScreenBounds;
            if (bounds.IsEmpty) return false;
            return bounds.Left >= viewport.X && bounds.Right <= viewport.X + viewport.Width &&
                   bounds.Top >= viewport.Y && bounds.Bottom <= viewport.Y + viewport.Height;
        }
    }

    /// <summary>
    /// True when any part of the sprite is inside the viewport, in screen space. Use this to skip drawing.
    /// </summary>
    public bool VisibleInViewportBounds
    {
        get
        {
            Viewport viewport = _bus.Gdm.GraphicsDevice.Viewport;
            Rectangle bounds = ScreenBounds;
            if (bounds.IsEmpty) return false;
            return bounds.Right >= viewport.X && bounds.Left <= viewport.X + viewport.Width &&
                   bounds.Bottom >= viewport.Y && bounds.Top <= viewport.Y + viewport.Height;
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

    /// <summary>
    /// True when the given edge of the sprite is past the viewport edge, in screen space. Follows the camera.
    /// </summary>
    /// <remarks>
    /// <para>Example: Direction2D.Up is true when the top edge is above the viewport top.</para>
    /// </remarks>
    public virtual bool IsOverWindowBounds(Direction2D dir)
    {
        Viewport viewport = _bus.Gdm.GraphicsDevice.Viewport;
        Rectangle bounds = ScreenBounds;

        switch (dir)
        {
            case Direction2D.Left:
                return bounds.Left < viewport.X;
            case Direction2D.Right:
                return bounds.Right > viewport.X + viewport.Width;
            case Direction2D.Down:
                return bounds.Bottom > viewport.Y + viewport.Height;
            case Direction2D.Up:
                return bounds.Top < viewport.Y;
            default:
                return false;
        }
    }

    /// <summary>
    /// True when the given window-pixel point is inside the sprite's frame.
    /// </summary>
    /// <remarks>
    /// <para>order of ops: converts the point to world space with the main camera, subtracts the sprite position, undoes the sprite rotation and scale, then adds the anchor offset, and then checks the result against the frame rectangle.</para>
    /// </remarks>
    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        if (_texture is null) return false;

        Vector2 frameSize = (SourceRectangle?.Size ?? _texture.Bounds.Size).ToVector2();
        if (frameSize.X <= 0f || frameSize.Y <= 0f) return false;

        if (ParentTransform.Scale.X == 0f || ParentTransform.Scale.Y == 0f) return false;

        Vector2 worldPos = _bus.ScreenToWorld(screenPoint);
        Vector2 relativePos = worldPos - ParentTransform.Position;

        float cos = MathF.Cos(-ParentTransform.Rotation);
        float sin = MathF.Sin(-ParentTransform.Rotation);
        Vector2 unrotated = new(relativePos.X * cos - relativePos.Y * sin, relativePos.X * sin + relativePos.Y * cos);
        unrotated /= ParentTransform.Scale;

        Vector2 local = unrotated + frameSize * _anchor;
        return local.X >= 0f && local.Y >= 0f && local.X <= frameSize.X && local.Y <= frameSize.Y;
    }

    /// <summary>
    /// True when the mouse cursor is inside the sprite's frame. Calls ContainsScreenPoint with the current mouse position.
    /// </summary>
    public bool IsCursorOnSprite()
    {
        return ContainsScreenPoint(Mouse.GetState().Position.ToVector2());
    }

    // basically reverse ContainsScreenPoint
    // frame point -> sprite transform -> world -> main camera -> screen.
    private Vector2 LocalToScreen(Vector2 framePoint)
    {
        Vector2 frameSize = (SourceRectangle?.Size ?? _texture.Bounds.Size).ToVector2();
        Vector2 rel = (framePoint - (frameSize * _anchor)) * ParentTransform.Scale;

        float cos = MathF.Cos(ParentTransform.Rotation);
        float sin = MathF.Sin(ParentTransform.Rotation);
        Vector2 world = ParentTransform.Position + new Vector2(rel.X * cos - rel.Y * sin, rel.X * sin + rel.Y * cos);

        return _bus.WorldToScreen(world);
    }
}
