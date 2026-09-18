using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoCollision;

// note to future self:
// dumb data holder for a collision pair, both colliders are handed the same instance in OnCollision() so any state you shove on it is shared between the two sides
public class NeoCollision
{
    public NeoBoxCollider A;
    public NeoBoxCollider B;

    // the overlapping rectangle of the two bounds, useful for push-out resolution or whatever the fuck you're making, either ways it has to be a rectangle and ideally NOT rotated
    // if you want to rotate this to a non-right-angle-degree, congrats here's a TODO: implement bounds compatible wiht non-right-angle-degrees
    public Rectangle Overlap;

    public NeoCollision(NeoBoxCollider a, NeoBoxCollider b, Rectangle overlap)
    {
        A = a;
        B = b;
        Overlap = overlap;
    }
}
