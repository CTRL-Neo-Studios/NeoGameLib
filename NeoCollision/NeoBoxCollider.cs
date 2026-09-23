using System;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoCollision;

// note to future self:
// with the creation of this beauty colliders are NOT bound to sprites anymore
// Now a collider is just a rectangle in world space and any object can have one. if a sprite needs a collider you add a NeoSpriteCollider alongside NeoSpriteRenderer on the same object.
// the box is defined as Offset + Size relative to the parent transform so moving or scaling the object moves and scales the box automatically (remember the hour you lost to the not-scaling-to-2 bug? you're fucking welcome)
// rotation is ignored for now, same deal as the sprite renderer bounds, rotated colliders would need OBBs and that's a whole different pile of fuck

// note to self:
// just thought of a random DE quote while writing these
// What's that with third person shit? Cuno is FIRST PERSON
// that fucking kid
public class NeoBoxCollider : NeoComponent
{
    public Vector2 Offset { get; set; } = new(0);
    public Vector2 Size { get; set; } = new(1f);

    // note to self:
    // world-space bounds, WORLD-SPACE
    // also, note that the negative transform scale inverts the offset (mirrored object = mirrored box) but the size is absolute because a rectangle with negative width makes Intersects always return false and that's the kind of bug that'll fuck you in the arse if I didn't write and test it beforehand so you're welcome future-self
    public virtual Rectangle Bounds
    {
        get
        {
            Vector2 scale = ParentTransform.Scale;
            Vector2 pos = ParentTransform.Position + Offset * scale;
            Vector2 size = Size * new Vector2(MathF.Abs(scale.X), MathF.Abs(scale.Y));
            return new Rectangle((int)(pos.X - size.X / 2f), (int)(pos.Y - size.Y / 2f), (int)size.X, (int)size.Y);
        }
    }

    public override void OnAwake()
    {
        NeoGame.Singleton?.ColliderBus.Add(this);
    }

    public override void OnDestroy()
    {
        NeoGame.Singleton?.ColliderBus.Remove(this);
    }
}
