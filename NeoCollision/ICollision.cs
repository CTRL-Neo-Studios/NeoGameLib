using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoCollision;

// note to future self:
// implement this interface if your component on a collider-object needs collision features.
// the bus collider calls every ICollision component on BOTH objects of a colliding pair
public interface ICollision
{
    // other = the collider on the opposing object; collision = the raw pair data
    void OnCollision(NeoBoxCollider other, NeoCollision collision);
}
