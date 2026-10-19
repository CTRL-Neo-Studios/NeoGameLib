using Microsoft.Xna.Framework;
using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoMapping.DurableSpecifications.Primitives;

public sealed record BoxColliderSpec(
    Vector2 Size, 
    Vector2 Offset = default, 
    bool IsTrigger = false
) : INeoSpec
{
    public void Apply(NeoObject target)
    {
        target.AddComponent(new NeoBoxCollider { Size = Size, Offset = Offset, IsTrigger = IsTrigger });
    }
}