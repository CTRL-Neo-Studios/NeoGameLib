using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NeoGameLib.Common.StateMachine;

/// <summary>
/// One state of a <see cref="NeoStateMachine{TOwner}"/>. Subclass it and override whichever hooks you need.
/// </summary>
public abstract class NeoState<TOwner>
{
    public NeoStateMachine<TOwner> Machine { get; internal set; }

    public TOwner Owner => Machine.Owner;

    internal readonly List<(NeoState<TOwner> To, Func<bool> When)> Transitions = new();

    /// <summary>Fires when the machine switches into this state, after the previous state's OnExit.</summary>
    public virtual void OnEnter() { }

    /// <summary>Fires every <see cref="NeoStateMachine{TOwner}.Update"/> while this is the current state. Also fires on the entry tick for transitions, but not when the previous state switches via ChangeState from its own OnUpdate (then it starts next tick).</summary>
    public virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>Fires when the machine transitions away from this state or stops.</summary>
    public virtual void OnExit() { }
}