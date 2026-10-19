using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoMapping.DurableSpecifications.Primitives;

public sealed class SpriteColliderSpec : INeoSpec
{
    public void Apply(NeoObject target)
    {
        target.AddComponent<NeoSpriteCollider>();
    }
}