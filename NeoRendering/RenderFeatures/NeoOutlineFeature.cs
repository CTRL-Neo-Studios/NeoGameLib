using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoRendering.RenderFeatures;

/// <summary>
/// Draws a solid-color outline behind the sprite.
/// </summary>
/// <remarks>
/// <para>The feature makes a silhouette copy of the sprite texture and draws every pixel as white. Then it draws the silhouette eight times behind the sprite and moves it according to the Thickness attr in eight directions and tints it with the Color attr.</para>
/// <para>Thickness is in world pixels. Also this is Animation-friendly.</para>
/// </remarks>
public class NeoOutlineFeature : NeoSpriteRenderFeature
{
    private const float Diagonal = 0.70710677f; // this is a rough estimate of a square-root of 2 over 2. so like `sqrt(2)/2`. this is precaculated to save up some time in mathematical computations

    private static readonly Vector2[] Directions =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(Diagonal, Diagonal), new(-Diagonal, Diagonal), new(Diagonal, -Diagonal), new(-Diagonal, -Diagonal)
    };
    private Texture2D _source;
    private Texture2D _silhouette;

    public Color Color { get; set; } = Color.White;
    public float Thickness { get; set; } = 2f;

    public override void OnDrawBehind(SpriteBatch spriteBatch, in NeoSpriteDrawInfo info)
    {
        if (Thickness <= 0f) return;

        Texture2D silhouette = GetSilhouette(info.Texture);
        if (silhouette is null) return;

        for (int i = 0; i < Directions.Length; i++)
            info.Draw(spriteBatch, silhouette, Color, Directions[i] * Thickness);
    }

    protected override void OnFeatureDestroy()
    {
        _silhouette?.Dispose();
        _silhouette = null;
        _source = null;
    }

    private Texture2D GetSilhouette(Texture2D texture)
    {
        if (ReferenceEquals(texture, _source)) return _silhouette;

        _silhouette?.Dispose();
        _silhouette = null;
        _source = texture;
        if (texture.Format != SurfaceFormat.Color) return null;

        Color[] pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        // premultiplied white here cuz sprite batch's default AlphaBlend expects a premultiplied alpha. idk why
        for (int i = 0; i < pixels.Length; i++)
        {
            byte a = pixels[i].A;
            pixels[i] = new Color(a, a, a, a);
        }

        _silhouette = new Texture2D(texture.GraphicsDevice, texture.Width, texture.Height);
        _silhouette.SetData(pixels);
        return _silhouette;
    }
}
