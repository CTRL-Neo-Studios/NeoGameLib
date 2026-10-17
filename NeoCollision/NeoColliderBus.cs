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
//
// collisions now calls every other component that implements the ICollision interface on BOTH objects of the pair.
// In reaction to the world-load-unload architecture change, a world collision-group gate now runs before the interface calls: only the worlds in the same group (or worlds in the ungrouped section) can collide with each other
//
// enter/stay/exit: the bus diffs this frame's overlapping pairs against last frame's set.
// a pair that appears will fire OnCollisionEnter on both sides. if it stays then OnCollision is fired constantly
// a pair that disappears will fire OnCollisionExit on whichever side that is still attached (since by the time this was iterated a side may've been already destroyed or the world's unloaded). the same survivor-gets-the-call logic applies to the exit callback.
public class NeoColliderBus : NeoBus<NeoBoxCollider>
{
    private WorldManager _worldManager;
    private List<NeoCollision> _collisions = new();
    private Rectangle[] _bounds = new Rectangle[0];
    private int[] _groups = new int[0];

    private HashSet<ColliderPair> _active = new(); // overlapping pairs in last frame
    private HashSet<ColliderPair> _next = new(); // overlapping pairs in this frame

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

                // same group runs the check, different groups does not run the check, -1 and other groups does not run the check
                if (_groups[i] != _groups[j] && (_groups[i] >= 0 || _groups[j] >= 0)) continue; // if group gates are different and that groups aren't ungrouped, skip

                NeoBoxCollider a = colliders[i];
                NeoBoxCollider b = colliders[j];

                // an earlier callback this pass may have removed either collider or destroyed its object
                if (!a.IsAttached || !b.IsAttached) continue;

                NeoCollision col = new(a, b, Rectangle.Intersect(_bounds[i], _bounds[j]));
                _collisions.Add(col);
                
                ColliderPair pair = new(a, b);
                bool entered = _next.Add(pair) && !_active.Contains(pair);

                Notify(a, b, col, entered ? CollisionPhase.Enter : CollisionPhase.Stay);
                Notify(b, a, col, entered ? CollisionPhase.Enter : CollisionPhase.Stay);
            }
        }

        // a pair that disappeared in this frame fires exit on whichever side still exists.
        // a collider that was destroyed/marked-for-destruction mid-overlap exit on whichever side still exists a frame later
        foreach (ColliderPair pair in _active)
        {
            if (_next.Contains(pair)) continue;

            if (pair.A.IsAttached) NotifyExit(pair.A, pair.B);
            if (pair.B.IsAttached) NotifyExit(pair.B, pair.A);
        }

        _active.Clear();
        (_active, _next) = (_next, _active);
        return _collisions;
    }

    private static void Notify(NeoBoxCollider self, NeoBoxCollider other, NeoCollision col, CollisionPhase phase)
    {
        if (!self.IsAttached || !other.IsAttached) return;

        if (phase == CollisionPhase.Enter) self.OnPairEnter(other, col);
        else self.OnPairStay(other, col);

        using PassSnapshot<NeoComponent> components = new(self.ParentObject.Components);
        for (int k = 0; k < components.Count; k++)
        {
            if (!self.IsAttached || !other.IsAttached) return;
            NeoComponent c = components[k];
            if (!c.IsAttached || !c.Enabled || c is not ICollision handler) continue;

            if (phase == CollisionPhase.Enter) handler.OnCollisionEnter(other, col);
            else handler.OnCollision(other, col);
        }
    }

    private static void NotifyExit(NeoBoxCollider self, NeoBoxCollider other)
    {
        self.OnPairExit(other);

        using PassSnapshot<NeoComponent> components = new(self.ParentObject.Components);
        for (int k = 0; k < components.Count; k++)
        {
            if (!self.IsAttached) return;
            NeoComponent c = components[k];
            if (c.IsAttached && c.Enabled && c is ICollision handler) handler.OnCollisionExit(other);
        }
    }

    private enum CollisionPhase
    {
        Enter,
        Stay,
    }
}