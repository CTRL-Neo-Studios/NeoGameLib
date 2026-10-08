using System;
using System.Collections.Generic;
using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
// the namespace NeoGameLib.NeoCollision shadows its own NeoCollision class from here,
// alias the type so the namespace can keep its name, C# being C#
using NeoCollisionData = NeoGameLib.NeoCollision.NeoCollision;

namespace NeoGameLib.Common;

// this is basically half a Rigidbody2D

// note to future self from version 0.2.3:
// movement is now corrected by other colliders, and uses a different way of moving:
// each axis now moves separately and is pushed back out of everything it clips.
//
// what counts as "solid", per se:
// - both lists empty  -> everything is solid (the default)
// - whitelist set     -> mover can ONLY clip through whitelisted objects
// - blacklist set     -> mover can only clip through objects NOT in blacklist
// if both lists are set, whitelist overrides blacklist.
public class NeoMover : NeoComponent
{
    public Keys? KeyUp, KeyDown, KeyLeft, KeyRight, KeyJump;

    public float Speed = 200f;
    public float Gravity = 2000f; // px/s^2, 0 = no gravity
    public float JumpVelocity = 600f; // px/s upward
    public bool Omnidirectional = true; // false = up/down input ignored, vertical is gravity+jump only
    public bool UseLerp = true;
    public float LerpSnappiness = 10f; // higher = snappier
    public Type GroundComponentType; // e.g. typeof(GroundTag)

    public bool Grounded { get; private set; }

    public List<Type> CollisionWhitelist { get; set; } = new();
    public List<Type> CollisionBlacklist { get; set; } = new();

    // note to future self:
    // exposed if you want to do custom collision correction
    public Vector2 Velocity { get; private set; }

    private NeoBoxCollider _collider;
    private Vector2 _targetDir = new(0);
    private Vector2 _currentDir = new(0);
    private float _fallVelocity;
    private KeyboardState _prevKeyboard;

    public override void OnAwake()
    {
        _collider = ParentObject.GetComponent<NeoBoxCollider>();
    }

    public void AddToWhitelist<T>() where T : NeoComponent { CollisionWhitelist.Add(typeof(T)); }
    public void RemoveFromWhitelist<T>() where T : NeoComponent { CollisionWhitelist.Remove(typeof(T)); }
    public void AddToBlacklist<T>() where T : NeoComponent { CollisionBlacklist.Add(typeof(T)); }
    public void RemoveFromBlacklist<T>() where T : NeoComponent { CollisionBlacklist.Remove(typeof(T)); }

    public void Move(Vector2 direction)
    {
        _targetDir = direction;
    }

    // only jumps while grounded
    public void Jump()
    {
        if (!Grounded) return;
        _fallVelocity = -JumpVelocity;
    }

    /// <summary>
    /// Adds a velocity kick.
    /// </summary>
    public void AddImpulse(Vector2 impulse)
    {
        _fallVelocity += impulse.Y;
        if (Speed != 0) _currentDir.X += impulse.X / Speed;
    }

    public override void OnUpdate(GameTime gameTime)
    {
        float dt = (float) gameTime.ElapsedGameTime.TotalSeconds;

        // keyboard logic runs only when keys are actually configured so a pure Move func driven mover won't poll user input. Though I doubt I'll make a pure Move-func driven mover someday...
        if (KeyUp.HasValue || KeyDown.HasValue || KeyLeft.HasValue || KeyRight.HasValue || KeyJump.HasValue)
        {
            KeyboardState kb = Keyboard.GetState();

            if (KeyUp.HasValue || KeyDown.HasValue || KeyLeft.HasValue || KeyRight.HasValue)
            {
                Vector2 input = Vector2.Zero;
                if (KeyLeft.HasValue && kb.IsKeyDown(KeyLeft.Value)) input.X -= 1;
                if (KeyRight.HasValue && kb.IsKeyDown(KeyRight.Value)) input.X += 1;
                if (Omnidirectional)
                {
                    if (KeyUp.HasValue && kb.IsKeyDown(KeyUp.Value)) input.Y -= 1;
                    if (KeyDown.HasValue && kb.IsKeyDown(KeyDown.Value)) input.Y += 1;
                }
                if (input != Vector2.Zero) input.Normalize(); // do NOT normalize zero, MonoGame gives you NaN for some god damn reason
                _targetDir = input;
            }

            // counter strike valve source engine bhop time
            if (KeyJump.HasValue && kb.IsKeyDown(KeyJump.Value) && _prevKeyboard.IsKeyUp(KeyJump.Value))
                Jump();

            _prevKeyboard = kb;
        }

        // grounding is basically touching a ground-tagged object while NOT jumping
        Grounded = false;
        if (_collider is not null && GroundComponentType is not null && _fallVelocity >= 0)
        {
            var collisions = NeoGame.Singleton?.ColliderBus?.Collisions;
            if (collisions is not null)
            {
                foreach (NeoCollisionData col in collisions)
                {
                    NeoBoxCollider other = col.A == _collider ? col.B : col.B == _collider ? col.A : null;
                    if (other is null) continue;
                    if (!HasGroundTag(other.ParentObject)) continue;

                    Grounded = true;
                    break;
                }
            }
        }

        // gravity
        if (Grounded) _fallVelocity = 0;
        else _fallVelocity += Gravity * dt;

        // dir-based movement with option to enable/disable dir lerping
        _currentDir = UseLerp ? Vector2.Lerp(_currentDir, _targetDir, 1f - MathF.Exp(-LerpSnappiness * dt)) : _targetDir;

        // Velocity attr overrides movement, mainly because it's exposed for custom collision correction
        Velocity = _currentDir * Speed + new Vector2(0, _fallVelocity);

        // if we just apply opposite velocity when colliding, sliding along colliders won't exist, since the mover could move in a direction that won't overlap with other colliders but the collision detection still picks up between it and other colliders, which stops it from sliding.
        // like an object with infinite friction.
        // So per axis correction solves the infinite friction issue as you can move in a non-overlap direction with correction only applied in one direction instead of all directions. Congrats! Now go make the binding of isaac
        Vector2 xVelMove = new(Velocity.X * dt, 0);
        Vector2 yVelMove = new(0, Velocity.Y * dt);
        ParentTransform.Position += xVelMove;
        ResolveCollisions(xVelMove);
        ParentTransform.Position += yVelMove;
        ResolveCollisions(yVelMove);
    }

    private void ResolveCollisions(Vector2 moved)
    {
        if (_collider is null || !_collider.Enabled) return; // won't resolve collisions if there's no collider on the object that the mover is attached to

        List<NeoBoxCollider> cs = NeoGame.Singleton?.ColliderBus?.Colliders; // stands for colliders snapshot, future me, in case if you're wondering why it's named counter strike
        if (cs is null) return;

        foreach (NeoBoxCollider other in cs)
        {
            // if the other collider is this one, or other collider is disabled,
            // OR other component is a "hanging" component whose parent object does not exist in the world,
            // OR a object with a collider that this can pass through, skip
            if (other == _collider || !other.Enabled || other.ParentObject is null || CanPassThrough(other.ParentObject)) continue;
            
            Rectangle overlap = Rectangle.Intersect(_collider.Bounds, other.Bounds);
            if (overlap.IsEmpty) continue; // skip if there's no overlap

            Vector2 pos = ParentTransform.Position;
            pos.X += moved.X > 0 ? -overlap.Width : moved.X < 0 ? overlap.Width : 0;
            pos.Y += moved.Y > 0 ? -overlap.Height : moved.Y < 0 ? overlap.Height : 0;
            
            ParentTransform.Position = pos;

            // reset direction per axis (input or gravity re-adds the move dir next tick)
            if (moved.X != 0) _currentDir.X = 0;
            if (moved.Y != 0) _currentDir.Y = _fallVelocity = 0;
        }
    }

    // whitelist = pass through these, blacklist = block ONLY these, both empty = block everything. see the notes above
    private bool CanPassThrough(NeoObject other)
    {
        if (other is null) return true;

        if (CollisionWhitelist.Count > 0)
            return CollisionWhitelist.Exists(other.HasComponent); // whitelist overrides blacklist

        if (CollisionBlacklist.Count > 0)
            return !CollisionBlacklist.Exists(other.HasComponent);

        return false;
    }

    private bool HasGroundTag(NeoObject obj)
    {
        return obj is not null && obj.HasComponent(GroundComponentType);
    }
}
