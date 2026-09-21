using System;
using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
// the namespace NeoGameLib.NeoCollision shadows its own NeoCollision class from here,
// alias the type so the namespace can keep its name, C# being C#
using NeoCollisionData = NeoGameLib.NeoCollision.NeoCollision;

namespace NeoGameLib.Common;

// this is basically half a Rigidbody2D

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

    private NeoBoxCollider _collider;
    private Vector2 _targetDir = new(0);
    private Vector2 _currentDir = new(0);
    private float _fallVelocity;
    private KeyboardState _prevKeyboard;

    public override void OnAwake()
    {
        _collider = ParentObject.GetComponent<NeoBoxCollider>();
    }

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

    public override void OnUpdate(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
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

        // grounding is basically touching a ground-tagged object while not jumping
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

        // you can bhop
        if (KeyJump.HasValue && kb.IsKeyDown(KeyJump.Value) && _prevKeyboard.IsKeyUp(KeyJump.Value))
            Jump();

        // gravity
        if (Grounded) _fallVelocity = 0;
        else _fallVelocity += Gravity * dt;

        // dir movement lerp smoothing
        if (UseLerp)
            _currentDir = Vector2.Lerp(_currentDir, _targetDir, 1f - MathF.Exp(-LerpSnappiness * dt));
        else
            _currentDir = _targetDir;

        // applying all movement vectors
        ParentTransform.Position += _currentDir * Speed * dt + new Vector2(0, _fallVelocity) * dt;

        _prevKeyboard = kb;
    }

    private bool HasGroundTag(NeoObject obj)
    {
        if (obj is null) return false;
        
        return obj.HasComponent(GroundComponentType);
    }
}
