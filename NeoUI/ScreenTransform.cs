using System;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoUI;

/// <summary>
/// The screen-space transform of a UI element. It adds layout related attrs i.e. Size, anchors, Pivot, sizing modes, etc.
/// </summary>
/// <remarks>
/// <para>Same model as Unity's uGUI RectTransform, except that y points DOWN (anchor (0, 0) is the top-left, not the bottom-left).</para>
/// </remarks>
public class ScreenTransform : Transform
{
    private Vector2 _size;
    private Vector2 _anchorMin = new(0.5f);
    private Vector2 _anchorMax = new(0.5f);
    private Vector2 _pivot = new(0.5f);

    /// <summary>
    /// The box size on point axes. On stretched axes this is the delta added to the anchor span (uGUI's sizeDelta)
    /// </summary>
    public Vector2 Size
    {
        get => _size;
        set { ValidateVector(value, false); _size = value; }
    }

    /// <summary>The corner of the parent's inner box that the element's top-left hangs from, 0-1 per axis. Default (0.5, 0.5).</summary>
    public Vector2 AnchorMin
    {
        get => _anchorMin;
        set { ValidateNormalized(value); _anchorMin = value; }
    }

    /// <summary>The corner of the parent's inner box that the element's bottom-right hangs from, 0-1 per axis. Default (0.5, 0.5).</summary>
    public Vector2 AnchorMax
    {
        get => _anchorMax;
        set { ValidateNormalized(value); _anchorMax = value; }
    }

    /// <summary>
    /// Shorthand for a point anchor: setting it sets both AnchorMin and AnchorMax. Reading it returns AnchorMin, so read the two properties if the element can be stretched.
    /// </summary>
    public Vector2 Anchor
    {
        get => _anchorMin;
        set { ValidateNormalized(value); _anchorMin = _anchorMax = value; }
    }

    public Vector2 Pivot
    {
        get => _pivot;
        set { ValidateNormalized(value); _pivot = value; }
    }

    /// <summary>True when the anchors differ on x; width follows parent</summary>
    public bool StretchesX => _anchorMin.X != _anchorMax.X;

    /// <summary>True when the anchors differ on y; height follows parent</summary>
    public bool StretchesY => _anchorMin.Y != _anchorMax.Y;

    // note to self:
    // insets are just another view of Position + Size (uGUI's offsetMin/offsetMax), so they depend on Pivot.
    // inset = distance from the anchor-range edge to the box edge, positive pointing inwards. they only mean "inset" on stretched axes, on point axes they're just the box edges relative to the anchor point.
    // setting one keeps the opposite inset where it is.
    // named Inset* because Transform.Left/Right already are direction vectors.

    /// <summary>Distance from the left anchor edge to the box's left edge</summary>
    public float InsetLeft
    {
        get => Position.X - _size.X * _pivot.X;
        set => SetInsets(0, value, InsetRight);
    }

    /// <summary>Distance from the box's right edge to the right anchor edge</summary>
    public float InsetRight
    {
        get => -(Position.X + _size.X * (1 - _pivot.X));
        set => SetInsets(0, InsetLeft, value);
    }

    /// <summary>Distance from the top anchor edge to the box's top edge</summary>
    public float InsetTop
    {
        get => Position.Y - _size.Y * _pivot.Y;
        set => SetInsets(1, value, InsetBottom);
    }

    /// <summary>Distance from the box's bottom edge to the bottom anchor edge</summary>
    public float InsetBottom
    {
        get => -(Position.Y + _size.Y * (1 - _pivot.Y));
        set => SetInsets(1, InsetTop, value);
    }

    public NeoUISizing WidthMode { get; set; } = NeoUISizing.Auto;

    public NeoUISizing HeightMode { get; set; } = NeoUISizing.Auto;

    public Vector2 DesiredSize { get; internal set; }

    /// <summary>The top-left of the untransformed box, in canvas units, from the most recent update tick or draw pass.</summary>
    public Vector2 LayoutPosition { get; internal set; }

    /// <summary>The size of the untransformed box, in canvas units, from the most recent update tick or draw pass.</summary>
    public Vector2 LayoutSize { get; internal set; }

    /// <summary>True when Rotation or Scale changes how the element draws.</summary>
    public bool HasVisualTransform => Rotation != 0 || Scale != Vector2.One;

    private void SetInsets(int axis, float start, float end)
    {
        if (!float.IsFinite(start) || !float.IsFinite(end))
            throw new ArgumentOutOfRangeException(nameof(start), "insets must be finite");

        float pivot = axis == 0 ? _pivot.X : _pivot.Y;
        float delta = -(start + end);
        float position = start + delta * pivot;

        if (axis == 0)
        {
            _size.X = delta;
            Position = new Vector2(position, Position.Y);
        }
        else
        {
            _size.Y = delta;
            Position = new Vector2(Position.X, position);
        }
    }

    /// <summary>
    /// Resolves one axis of the box inside the parent's inner box. Stretched axes (anchors differ) follow the anchor span plus the Size delta; otherwise Fill takes the parent's full length and anything else takes the measured desired length.
    /// </summary>
    internal static void PlaceAxis(float parentMin, float parentLength, float anchorMin, float anchorMax, float pivot, float position, float sizeDelta, float desired, bool fill, out float layoutMin, out float layoutLength)
    {
        float from = parentMin + parentLength * anchorMin;
        float to = parentMin + parentLength * anchorMax;

        layoutLength = anchorMin != anchorMax
            ? MathF.Max(0, to - from + sizeDelta)
            : MathF.Max(0, fill ? parentLength : desired);

        layoutMin = MathHelper.Lerp(from, to, pivot) + position - layoutLength * pivot;
    }

    private static void ValidateVector(Vector2 value, bool nonnegative)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || (nonnegative && (value.X < 0 || value.Y < 0)))
            throw new ArgumentOutOfRangeException(nameof(value), nonnegative ? "UI coordinates must be finite and non-negative" : "UI coordinates must be finite");
    }

    private static void ValidateNormalized(Vector2 value)
    {
        ValidateVector(value, true);
        if (value.X > 1 || value.Y > 1) throw new ArgumentOutOfRangeException(nameof(value), "anchors and pivots must be between 0 and 1");
    }
}
