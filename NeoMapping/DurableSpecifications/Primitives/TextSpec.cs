using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoGO;
using NeoGameLib.NeoRendering;

namespace NeoGameLib.NeoMapping.DurableSpecifications.Primitives;

public sealed record TextSpec(
    [property: NeoAsset(typeof(SpriteFont))] string Font,
    string Text = "", 
    float Depth = 0f, 
    int Order = 0, 
    int Layer = 1, 
    Vector2? Anchor = null
) : INeoSpec
{
    public void Apply(NeoObject target)
    {
        NeoGame game = NeoGame.Singleton;
        NeoTextRenderer renderer = new(game.RenderBus, game.Assets.Load<SpriteFont>(Font))
        {
            Text = Text,
            Depth = Depth,
            Order = Order,
            Layer = Layer,
        };
        if (Anchor is Vector2 anchor) renderer.Anchor = anchor;
        target.AddComponent(renderer);
    }
}