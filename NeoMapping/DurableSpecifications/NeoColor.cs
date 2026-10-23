using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoMapping.DurableSpecifications;

/// <summary>An RGBA color as data: 0-255 per channel, straight (non-premultiplied) alpha like CSS rgba, alpha defaults to 255 (opaque). Serializes as <c>{ "r": .., "g": .., "b": .., "a": .. }</c>.</summary>
/// <remarks>
/// <para>MonoGame's Color can't be used in specs directly: it serializes as r/g/b/a PLUS packedValue, and a struct's missing "a" would deserialize as 0 (fully transparent).</para>
/// <para>This is a class record on purpose: System.Text.Json only honours constructor defaults (A = 255) through a class constructor, never a struct's.</para>
/// </remarks>
public sealed record NeoColor(byte R, byte G, byte B, byte A = 255)
{
    // SpriteBatch blends premultiplied, so a raw new Color(255, 0, 0, 128) would draw brighter, not half-transparent
    public Color ToColor() => Color.FromNonPremultiplied(R, G, B, A);
}
