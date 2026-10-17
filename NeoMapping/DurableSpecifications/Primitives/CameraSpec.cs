using NeoGameLib.NeoGO;
using NeoGameLib.NeoRendering;

namespace NeoGameLib.NeoMapping.DurableSpecifications.Primitives;

public sealed record CameraSpec(
    float Zoom = 1f, 
    int Order = 0, 
    int CullingMask = -1
) : INeoSpec
{
    public void Apply(NeoObject target)
    {
        NeoGame game = NeoGame.Singleton;
        target.AddComponent(new NeoCamera(game.RenderBus) { Zoom = Zoom, Order = Order, CullingMask = CullingMask });
    }
}