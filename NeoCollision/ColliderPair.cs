using System;
using System.Runtime.CompilerServices;

namespace NeoGameLib.NeoCollision;

// order-insensitive helper struct for collider pairs.
// hashing has to be symmetric in order to prevent (a,b) and (b,a) being accounted as two separate pairs
public readonly struct ColliderPair : IEquatable<ColliderPair>
{
    public readonly NeoBoxCollider A;
    public readonly NeoBoxCollider B;

    public ColliderPair(NeoBoxCollider a, NeoBoxCollider b)
    {
        A = a;
        B = b;
    }

    public bool Equals(ColliderPair other) => (A == other.A && B == other.B) || (A == other.B && B == other.A);

    public override bool Equals(object obj) => obj is ColliderPair other && Equals(other);

    public override int GetHashCode()
    {
        int ha = RuntimeHelpers.GetHashCode(A);
        int hb = RuntimeHelpers.GetHashCode(B);
        return ha < hb ? HashCode.Combine(ha, hb) : HashCode.Combine(hb, ha);
    }
}