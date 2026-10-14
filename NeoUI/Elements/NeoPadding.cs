using System;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoUI.Elements;

/// <summary>
/// Pixels between an element's edge and its children, like the css padding style
/// </summary>
public readonly struct NeoPadding
{
    private readonly float _left, _top, _right, _bottom;

    public float Left { get => _left; init => _left = Check(value); }
    public float Top { get => _top; init => _top = Check(value); }
    public float Right { get => _right; init => _right = Check(value); }
    public float Bottom { get => _bottom; init => _bottom = Check(value); }

    public NeoPadding(float all) : this(all, all, all, all) { }

    public NeoPadding(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    public NeoPadding(float left, float top, float right, float bottom)
    {
        _left = Check(left);
        _top = Check(top);
        _right = Check(right);
        _bottom = Check(bottom);
    }

    /// <summary>returns the offset from the outer top-left corner to the inner top-left corner.</summary>
    public Vector2 TopLeft => new(_left, _top);

    /// <summary>returns the padding sum on each axis</summary>
    public Vector2 Total => new(_left + _right, _top + _bottom);

    public static implicit operator NeoPadding(float all) => new(all);

    private static float Check(float value) => (float.IsFinite(value) && value >= 0) ? 
        value : throw new ArgumentOutOfRangeException(nameof(value), "padding must be finite and non-negative");
}
