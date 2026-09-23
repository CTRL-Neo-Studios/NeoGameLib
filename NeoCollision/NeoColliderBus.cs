using System.Collections.Generic;
using NeoGameLib.Common;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoCollision;

// note to future self:
// the collider bus runs detection for each registered collider in each frame.
// colliders register themselves in OnAwake via the neogame singleton, same as the render bus, so nothing new to see here
//
// detection is basically a O(n^2) pairing double-iter-loop with every collider tested against every other once per frame.
// bounds are cached once per frame so a pair test is just calling the Rectangle.Intersect func.
// TODO: to future self you should optimize this double-iter-loop later but for the current complexity of projects it'll do
//
// collisions now calls every other component that implements the ICollision interface on BOTH objects of the pair.
// In reaction to the world-load-unload architecture change, a world collision-group gate now runs before the interface calls: only the worlds in the same group (or worlds in the ungrouped section) can collide with each other
// For this part of the explanation you're better off checking out instructions.md, i don't want to copy-paste architecture explanations everywhere
//
// also TODO for future features to impl: enter/stay/exit tracking, CCD
// enter/stay/exit is basically like 40-ish lines of diffing a HashSet of active pairs, you can add it ONLY when a damage system needs to fire once per contact and not every frame
public class NeoColliderBus : NeoBus<NeoBoxCollider>
{
    private WorldManager _worldManager;
    private List<NeoCollision> _collisions = new();
    private Rectangle[] _bounds = new Rectangle[0];
    private int[] _groups = new int[0];

    public List<NeoCollision> Collisions => _collisions;

    // note to self:
    // worldManager is optional.
    // passing in a null would make everything collide with everything. passing one in would make world collision groups on it start running collision checks
    public NeoColliderBus(WorldManager worldManager = null)
    {
        _worldManager = worldManager;
    }

    public List<NeoCollision> Update()
    {
        _collisions.Clear();

        if (_bounds.Length < Items.Count)
        {
            _bounds = new Rectangle[Items.Count];
            _groups = new int[Items.Count];
        }

        // caches the bounds and collision groups once per collider per frame.
        // group `-1` == ungrouped
        for (int i = 0; i < Items.Count; i++)
        {
            _bounds[i] = Items[i].Enabled ? Items[i].Bounds : Rectangle.Empty;
            _groups[i] = _worldManager?.GetCollisionGroupId(Items[i].ParentObject.World?.Name) ?? -1;
        }

        for (int i = 0; i < Items.Count; i++)
        {
            for (int j = i + 1; j < Items.Count; j++)
            {
                if (!_bounds[i].Intersects(_bounds[j])) continue;

                // world gate: same group proceeds while -1 or differently gated groups are skipped (-1 means ungrouped)
                // same group runs the check, different groups does not run the check, -1 and other groups does not run the check
                if (_groups[i] != _groups[j] && (_groups[i] >= 0 || _groups[j] >= 0)) continue; // if group gates are different and that groups aren't ungrouped, skip

                NeoBoxCollider a = Items[i];
                NeoBoxCollider b = Items[j];

                NeoCollision col = new(a, b, Rectangle.Intersect(_bounds[i], _bounds[j]));
                _collisions.Add(col);

                for (int k = 0; k < a.ParentObject.Components.Count; k++)
                {
                    NeoComponent c = a.ParentObject.Components[k];
                    if (c.Enabled && c is ICollision handler) handler.OnCollision(b, col);
                } // calling ICollision interfaces on object A
                
                for (int k = 0; k < b.ParentObject.Components.Count; k++)
                {
                    NeoComponent c = b.ParentObject.Components[k];
                    if (c.Enabled && c is ICollision handler) handler.OnCollision(a, col);
                } // calling ICollision interfaces on object B
            }
        }

        return _collisions;
    }
}
