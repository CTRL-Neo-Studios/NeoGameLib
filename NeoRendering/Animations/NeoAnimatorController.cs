using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NeoGameLib.Common.StateMachine;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoRendering.Animations;

// note to future self:
// the animator state machine which is NeoAnimator + State Machine
//
// update order matters a bit: add the controller AFTER the components it reads (brain, mover) so it sees their state from this tick instead of the last tick's.
// the controller finds or adds the NeoAnimator itself and keeps it right before itself

/// <summary>Ticks a <see cref="NeoStateMachine{TOwner}"/> of <see cref="NeoAnimationState"/>s against this object's <see cref="NeoAnimator"/>.</summary>
/// <remarks>The controller adds a NeoAnimator when the object doesn't have one, and the animator finds the NeoSpriteRenderer whenever it's added, so component order doesn't matter. Add the controller after the components its transitions read.</remarks>
public class NeoAnimatorController : NeoComponent
{
    public NeoAnimator Animator { get; private set; }

    public NeoStateMachine<NeoAnimator> Machine { get; private set; }

    public NeoAnimationState AddState(NeoAnimation clip)
    {
        if (Machine is null) throw new InvalidOperationException("add the controller to an object before calling AddState");
        return Machine.Add(new NeoAnimationState(clip));
    }

    public override void OnAwake()
    {
        Animator = ParentObject.GetComponent<NeoAnimator>();
        if (Animator is null)
        {
            Animator = ParentObject.AddComponent<NeoAnimator>();

            // components tick in add order: put the animator right before this controller so IsFinished is fresh when the machine polls it
            List<NeoComponent> components = ParentObject.Components;
            int controllerIndex = components.IndexOf(this);
            int animatorIndex = components.IndexOf(Animator);
            if (animatorIndex > controllerIndex)
            {
                components.RemoveAt(animatorIndex);
                components.Insert(controllerIndex, Animator);
            }
        }

        Machine = new NeoStateMachine<NeoAnimator>(Animator);
    }

    public override void OnUpdate(GameTime gameTime) => Machine.Update(gameTime);

    public override void OnDestroy() => Machine?.Stop();
}
