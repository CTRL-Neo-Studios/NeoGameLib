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
//
// note to future self:
// OnCollision is user code and can do ANYTHING mid-pass: destroy objects, remove colliders, unload/load worlds.
// so the pass runs over a snapshot (see PassSnapshot) with bounds and groups cached at the start:
// - a collider removed/destroyed mid-pass gets no more callbacks this pass, and no pair is reported with stale indices
// - a collider added mid-pass (e.g. a world loaded from a callback) starts colliding next pass
// - Collisions only holds pairs whose callbacks actually runs
public class NeoColliderBus : NeoBus<NeoBoxCollider>
{
    private WorldManager _worldManager;
    private List<NeoCollision> _collisions = new();
    private Rectangle[] _bounds = new Rectangle[0];
    private int[] _groups = new int[0];

    public List<NeoCollision> Collisions => _collisions;

    // note to self:
    // the live registered collider list mainly for the mover to do per-axis collision correction (using last frame's Collisions would make collisions lag one frame, and it is VERY NOTICABLE)
    public List<NeoBoxCollider> Colliders => Items;

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

        using PassSnapshot<NeoBoxCollider> colliders = new(Items);
        int count = colliders.Count;
        if (_bounds.Length < count)
        {
            _bounds = new Rectangle[count];
            _groups = new int[count];
        }

        // caches the bounds and collision groups once per collider per frame.
        // group `-1` == ungrouped
        for (int i = 0; i < count; i++)
        {
            NeoBoxCollider c = colliders[i];
            bool live = c.IsAttached && c.Enabled;
            _bounds[i] = live ? c.Bounds : Rectangle.Empty;
            _groups[i] = live ? _worldManager?.GetCollisionGroupId(c.ParentObject.World?.Name) ?? -1 : -1;
        }

        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (!_bounds[i].Intersects(_bounds[j])) continue;

                // world gate: same group proceeds while -1 or differently gated groups are skipped (-1 means ungrouped)
                // same group runs the check, different groups does not run the check, -1 and other groups does not run the check
                if (_groups[i] != _groups[j] && (_groups[i] >= 0 || _groups[j] >= 0)) continue; // if group gates are different and that groups aren't ungrouped, skip

                NeoBoxCollider a = colliders[i];
                NeoBoxCollider b = colliders[j];

                // an earlier callback this pass may have removed either collider or destroyed its object
                if (!a.IsAttached || !b.IsAttached) continue;

                NeoCollision col = new(a, b, Rectangle.Intersect(_bounds[i], _bounds[j]));
                _collisions.Add(col);

                NotifyHandlers(a, b, col); // calling ICollision interfaces on object A
                if (a.IsAttached && b.IsAttached) NotifyHandlers(b, a, col); // calling ICollision interfaces on object B
            }
        }

        return _collisions;
    }

    private static void NotifyHandlers(NeoBoxCollider self, NeoBoxCollider other, NeoCollision col)
    {
        using PassSnapshot<NeoComponent> components = new(self.ParentObject.Components);
        for (int k = 0; k < components.Count; k++)
        {
            if (!self.IsAttached || !other.IsAttached) return;
            NeoComponent c = components[k];
            if (c.IsAttached && c.Enabled && c is ICollision handler) handler.OnCollision(other, col);
        }
    }
}
