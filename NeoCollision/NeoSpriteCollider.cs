using NeoGameLib.NeoGO;
using NeoGameLib.NeoRendering;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoCollision;

// note to future self:
// this is literally just a box collider that uses the bounds from the sprite renderer on the SAME object so you don't have to keep Size synced with the texture size.
// the renderer bounds are already transform-aware (position, scale, anchor) so this gets all of that for free
// also this collider component inherits the same rotation=0 limitation from the renderer bounds, duh
public class NeoSpriteCollider : NeoBoxCollider
{
    public override Rectangle Bounds
    {
        get
        {
            NeoSpriteRenderer renderer = ParentObject.GetComponent<NeoSpriteRenderer>();
            return renderer is null ? Rectangle.Empty : renderer.Bounds;
        }
    }
}
