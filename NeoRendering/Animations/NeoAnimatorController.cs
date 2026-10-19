using System;
using Microsoft.Xna.Framework;
using NeoGameLib.Common;
using NeoGameLib.Common.StateMachine;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoRendering.Animations;

// note to future self:
// the animator state machine which is NeoAnimator + State Machine
//
// update order matters a bit: add the controller AFTER the components it reads (brain, mover) so it sees their state from this tick instead of the last tick's.

/// <summary>Ticks a <see cref="NeoStateMachine{TOwner}"/> of <see cref="NeoAnimationState"/>s against this object's <see cref="NeoAnimator"/>.</summary>
/// <remarks>Needs a NeoAnimator (and so a NeoSpriteRenderer) added before it.</remarks>
public class NeoAnimatorController : NeoComponent
{
    public NeoAnimator Animator { get; private set; }

    public NeoStateMachine<NeoAnimator> Machine { get; private set; }

    public NeoAnimationState AddState(NeoAnimation clip) => Machine.Add(new NeoAnimationState(clip));

    public override void OnAwake()
    {
        Animator = ParentObject.GetComponent<NeoAnimator>() ?? throw new Exception("A NeoAnimator component is required on an object with NeoAnimatorController, add it first");
        Machine = new NeoStateMachine<NeoAnimator>(Animator);
    }

    public override void OnUpdate(GameTime gameTime) => Machine.Update(gameTime);

    public override void OnDestroy() => Machine.Stop();
}
