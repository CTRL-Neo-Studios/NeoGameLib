using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoUI.Elements;

namespace NeoGameLib.NeoUI;

// note to future self:
// how every UI attribute value is written in a .neoui.xml. one grammar, used by the loader, the writer and (later) the schema, so they can't disagree.
// floats are ALWAYS invariant-culture: a machine with a comma decimal separator must not change what a file means.
//
//   float      12   12.5   -3
//   Vector2    "x y"                   (also "x" alone = both)
//   Color      #RRGGBB or #RRGGBBAA    raw RGBA exactly as MonoGame's Color stores it (premultiplied), alpha defaults to FF
//   NeoPadding "all" | "h v" | "l t r b"
//   enum       camelCase               (flexDirection="column")
//   bool       true | false
//   anchor     a preset keyword (top-left ... stretch) or "x y" (a point) or "minX minY maxX maxY"
internal static class NeoUIValues
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // uGUI's 4x4 anchor presets, y pointing down. (name, min, max)
    internal static readonly (string Name, Vector2 Min, Vector2 Max)[] AnchorPresets =
    {
        ("top-left", new(0, 0), new(0, 0)), ("top", new(0.5f, 0), new(0.5f, 0)), ("top-right", new(1, 0), new(1, 0)),
        ("left", new(0, 0.5f), new(0, 0.5f)), ("center", new(0.5f, 0.5f), new(0.5f, 0.5f)), ("right", new(1, 0.5f), new(1, 0.5f)),
        ("bottom-left", new(0, 1), new(0, 1)), ("bottom", new(0.5f, 1), new(0.5f, 1)), ("bottom-right", new(1, 1), new(1, 1)),
        ("stretch-top", new(0, 0), new(1, 0)), ("stretch-middle", new(0, 0.5f), new(1, 0.5f)), ("stretch-bottom", new(0, 1), new(1, 1)),
        ("stretch-left", new(0, 0), new(0, 1)), ("stretch-center", new(0.5f, 0), new(0.5f, 1)), ("stretch-right", new(1, 0), new(1, 1)),
        ("stretch", new(0, 0), new(1, 1)),
    };

    internal static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    internal static string Pascal(string name) => char.ToUpperInvariant(name[0]) + name[1..];

    #region floats and vectors

    internal static string Float(float value) => value.ToString("R", Inv);

    internal static float ParseFloat(string text)
    {
        if (!float.TryParse(text.Trim(), NumberStyles.Float, Inv, out float value) || 
            !float.IsFinite(value))
        {
            throw new FormatException($"'{text}' is not a number");
        }
        
        return value;
    }

    internal static string Vector(Vector2 v) => $"{Float(v.X)} {Float(v.Y)}";

    internal static Vector2 ParseVector(string text)
    {
        string[] parts = Split(text);
        return parts.Length switch
        {
            1 => new Vector2(ParseFloat(parts[0])),
            2 => new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1])),
            _ => throw new FormatException($"'{text}' is not a vector, write \"x y\""),
        };
    }

    #endregion
    #region colors

    internal static string ColorText(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";

    internal static Color ParseColor(string text)
    {
        string hex = text.Trim();
        if (!hex.StartsWith('#') || (hex.Length != 7 && hex.Length != 9)) throw new FormatException($"'{text}' is not a color, write #RRGGBB or #RRGGBBAA");

        byte Part(int i) => byte.Parse(hex.AsSpan(1 + i * 2, 2), NumberStyles.HexNumber, Inv);
        try
        {
            return new Color(Part(0), Part(1), Part(2), hex.Length == 9 ? Part(3) : (byte)255);
        }
        catch (FormatException)
        {
            throw new FormatException($"'{text}' is not a color, write #RRGGBB or #RRGGBBAA");
        }
    }
    
    #endregion
    #region padding

    internal static string PaddingText(NeoPadding p)
    {
        if (p.Left == p.Right && p.Top == p.Bottom) return (p.Left == p.Top) ? (Float(p.Left)) : $"{Float(p.Left)} {Float(p.Top)}";
        
        return $"{Float(p.Left)} {Float(p.Top)} {Float(p.Right)} {Float(p.Bottom)}";
    }

    internal static NeoPadding ParsePadding(string text)
    {
        float[] v = Split(text).Select(ParseFloat).ToArray();
        return v.Length switch
        {
            1 => new NeoPadding(v[0]),
            2 => new NeoPadding(v[0], v[1]),
            4 => new NeoPadding(v[0], v[1], v[2], v[3]),
            _ => throw new FormatException($"'{text}' is not padding, write \"all\", \"horizontal vertical\" or \"left top right bottom\""),
        };
    }

    #endregion
    #region rectangles

    internal static string RectText(Rectangle r) => $"{r.X.ToString(Inv)} {r.Y.ToString(Inv)} {r.Width.ToString(Inv)} {r.Height.ToString(Inv)}";

    internal static Rectangle ParseRect(string text)
    {
        string[] parts = Split(text);
        if (parts.Length != 4 || 
            !parts.All(p => int.TryParse(p, NumberStyles.Integer, Inv, out _)))
        {
            throw new FormatException($"'{text}' is not a rectangle, write \"x y width height\" in whole pixels");
        }

        return new Rectangle(int.Parse(parts[0], Inv), int.Parse(parts[1], Inv), int.Parse(parts[2], Inv), int.Parse(parts[3], Inv));
    }

    #endregion
    #region enums and bools

    internal static string EnumText(Enum value) => Camel(value.ToString());

    internal static object ParseEnum(Type type, string text)
    {
        foreach (string name in Enum.GetNames(type))
        {
            if (string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase)) 
                return Enum.Parse(type, name);
        }

        string options = string.Join(", ", Enum.GetNames(type).Select(Camel));
        throw new FormatException($"'{text}' is not one of: {options}");
    }

    internal static string BoolText(bool value) => value ? "true" : "false";

    internal static bool ParseBool(string text) => text.Trim() switch
    {
        "true" => true,
        "false" => false,
        _ => throw new FormatException($"'{text}' is not a bool, write true or false"),
    };

    #endregion
    #region anchors

    internal static string AnchorText(Vector2 min, Vector2 max)
    {
        foreach ((string name, Vector2 pMin, Vector2 pMax) in AnchorPresets)
        {
            if (pMin == min && pMax == max)
                return name;
        }

        return min == max ? Vector(min) : $"{Vector(min)} {Vector(max)}";
    }

    internal static (Vector2 Min, Vector2 Max) ParseAnchor(string text)
    {
        string t = text.Trim();
        foreach ((string name, Vector2 pMin, Vector2 pMax) in AnchorPresets)
        {
            if (name == t) 
                return (pMin, pMax);
        }

        string[] parts = Split(t);
        return parts.Length switch
        {
            2 => (new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1])), new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1]))),
            4 => (new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1])), new Vector2(ParseFloat(parts[2]), ParseFloat(parts[3]))),
            _ => throw new FormatException($"'{text}' is not an anchor, write a preset ({string.Join(", ", AnchorPresets.Select(p => p.Name))}), \"x y\" or \"minX minY maxX maxY\""),
        };
    }

    // anchors and pivots live inside the parent's box. ScreenTransform's setters enforce it; checking while parsing lets the loader report it at the attribute
    internal static Vector2 CheckNormalized(Vector2 v)
    {
        if (v.X < 0 || 
            v.Y < 0 || 
            v.X > 1 || 
            v.Y > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(v), "anchors and pivots must be between 0 and 1");
        }
        
        return v;
    }

    internal static (Vector2 Min, Vector2 Max) CheckNormalized((Vector2 Min, Vector2 Max) range)
    {
        CheckNormalized(range.Min);
        CheckNormalized(range.Max);
        return range;
    }

    private static string[] Split(string text) => text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    
    #endregion
}
