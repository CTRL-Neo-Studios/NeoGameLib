using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoGO;
using NeoGameLib.NeoRendering;

namespace NeoGameLib.NeoMapping.DurableSpecifications.Primitives;

public sealed record SpriteSpec(
    [property: NeoAsset(typeof(Texture2D))] string Texture,
    float Depth = 0f, 
    int Order = 0, 
    int Layer = 1, 
    Vector2? Anchor = null, 
    TileRect? Source = null,
    NeoColor Color = null // tint; null keeps the renderer's default (white = untinted)
) : INeoSpec
{
    public void Apply(NeoObject target)
    {
        NeoGame game = NeoGame.Singleton;
        NeoSpriteRenderer renderer = new(game.RenderBus, game.Assets.Load<Texture2D>(Texture))
        {
            Depth = Depth,
            Order = Order,
            Layer = Layer,
        };
        if (Anchor is Vector2 anchor) renderer.Anchor = anchor;
        if (Source is TileRect source) renderer.SourceRectangle = new Rectangle(source.X, source.Y, source.Width, source.Height);
        if (Color is not null) renderer.Color = Color.ToColor();
        target.AddComponent(renderer);
    }
}