using System;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoUI;

/// <summary>
/// The screen-space transform of a UI element. It adds layout related attrs i.e. Size, Anchor, Pivot, sizing modes, etc.
/// </summary>
public class ScreenTransform : Transform
{
    private Vector2 _size;
    private Vector2 _anchor = new(0.5f);
    private Vector2 _pivot = new(0.5f);

    public Vector2 Size
    {
        get => _size;
        set { ValidateVector(value, true); _size = value; }
    }

    public Vector2 Anchor
    {
        get => _anchor;
        set { ValidateNormalized(value); _anchor = value; }
    }

    public Vector2 Pivot
    {
        get => _pivot;
        set { ValidateNormalized(value); _pivot = value; }
    }

    public NeoUISizing WidthMode { get; set; } = NeoUISizing.Fixed;

    public NeoUISizing HeightMode { get; set; } = NeoUISizing.Fixed;

    public Vector2 DesiredSize { get; internal set; }

    /// <summary>The top-left of the untransformed box, in canvas units, from the most recent update tick or draw pass.</summary>
    public Vector2 LayoutPosition { get; internal set; }

    /// <summary>The size of the untransformed box, in canvas units, from the most recent update tick or draw pass.</summary>
    public Vector2 LayoutSize { get; internal set; }

    /// <summary>True when Rotation or Scale changes how the element draws.</summary>
    public bool HasVisualTransform => Rotation != 0 || Scale != Vector2.One;

    private static void ValidateVector(Vector2 value, bool nonnegative)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || (nonnegative && (value.X < 0 || value.Y < 0)))
            throw new ArgumentOutOfRangeException(nameof(value), "UI coordinates must be finite and non-negative sizes");
    }

    private static void ValidateNormalized(Vector2 value)
    {
        ValidateVector(value, true);
        if (value.X > 1 || value.Y > 1) throw new ArgumentOutOfRangeException(nameof(value), "anchors and pivots must be between 0 and 1");
    }
}
