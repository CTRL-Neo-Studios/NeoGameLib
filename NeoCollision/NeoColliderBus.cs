using System.Collections.Generic;
using NeoGameLib.Common;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoCollision;

// note to future self:
// the collider bus runs detection for each registered collider in each frame.
// colliders register themselves in OnAwake via the neogame singleton, same as the render bus, so nothing new to see here
//
// detection is basically a O(n^2) all-pairs sweep with every collider tested against every other once per frame.
// bounds are cached once per frame so a pair test is just calling the Rectangle.Intersect func.
// TODO: You should optimize this double for loop later but for the current complexity of projects it'll do
//
// also TODO for future features to impl: enter/stay/exit tracking, layer masks, CCD
// enter/stay/exit is basically like 40-ish lines of diffing a HashSet of active pairs, you can add it ONLY when a damage system needs to fire once per contact and not every frame
//
// also this bus is world-agnostic, colliders from different worlds WILL collide with each other.
// YOU HAVE BEEN WARNED
public class NeoColliderBus : NeoBus<NeoBoxCollider>
{
    private List<NeoCollision> _collisions = new();
    private Rectangle[] _bounds = new Rectangle[0];

    public List<NeoCollision> Collisions => _collisions;

    public List<NeoCollision> Update()
    {
        _collisions.Clear();

        if (_bounds.Length < Items.Count)
            _bounds = new Rectangle[Items.Count];

        // cache the bounds once per frame, skipping disabled colliders which intersects with nothing because they return an empty rectangle bound
        for (int i = 0; i < Items.Count; i++)
            _bounds[i] = Items[i].Enabled ? Items[i].Bounds : Rectangle.Empty;

        for (int i = 0; i < Items.Count; i++)
        {
            for (int j = i + 1; j < Items.Count; j++)
            {
                if (!_bounds[i].Intersects(_bounds[j])) continue;

                NeoBoxCollider a = Items[i];
                NeoBoxCollider b = Items[j];
                NeoCollision col = new(a, b, Rectangle.Intersect(_bounds[i], _bounds[j]));
                _collisions.Add(col);
                a.OnCollision(col);
                b.OnCollision(col);
            }
        }

        return _collisions;
    }
}
