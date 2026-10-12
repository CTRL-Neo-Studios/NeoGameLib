using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoRendering;

/// <summary>
/// draw values of a sprite in the current rendering pass.
/// negative-value flipping, origin and scale are already corrected.
/// </summary>
public readonly struct NeoSpriteDrawInfo
{
    public Texture2D Texture { get; }
    public Rectangle? SourceRectangle { get; }
    public Vector2 Position { get; }
    public Color Color { get; }
    public float Rotation { get; }
    public Vector2 Origin { get; }
    public Vector2 Scale { get; }
    public SpriteEffects Effects { get; }
    public float Depth { get; }

    public NeoSpriteDrawInfo(Texture2D texture, Rectangle? sourceRectangle, Vector2 position, Color color,
        float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float depth)
    {
        Texture = texture;
        SourceRectangle = sourceRectangle;
        Position = position;
        Color = color;
        Rotation = rotation;
        Origin = origin;
        Scale = scale;
        Effects = effects;
        Depth = depth;
    }

    /// <summary>
    /// Draws a texture with this sprite's transform with the world-space offset applied on top ofit. Default offset is 0
    /// </summary>
    /// <remarks>The texture MUST have the same layout as the sprite texture cuz the source rect is the same.</remarks>
    public void Draw(SpriteBatch spriteBatch, Texture2D texture, Color color, Vector2 offset = default)
    {
        spriteBatch.Draw(texture, Position + offset, SourceRectangle, color, Rotation, Origin, Scale, Effects, Depth);
    }
}