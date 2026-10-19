using System;
using NeoGameLib.Common;
using NeoGameLib.Common.StateMachine;

namespace NeoGameLib.NeoRendering.Animations;

/// <summary>A state that plays one clip on enter. Subclass it for per-state logic (sounds, hitboxes) and call base.OnEnter().</summary>
public class NeoAnimationState : NeoState<NeoAnimator>
{
    public NeoAnimation Clip { get; }

    public NeoAnimationState(NeoAnimation clip)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
    }

    public override void OnEnter() => Owner.Play(Clip, restart: true);
}