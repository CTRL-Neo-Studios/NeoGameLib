using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NeoGameLib.Common.StateMachine;

// note to future self:
// a NON-component standalone state machine
// see instructions.md 0.4.0 State Machines

public class NeoStateMachine<TOwner>
{
    // a chain of changes requested from OnEnter/OnExit/OnStateChanged longer than this is two states bouncing off each other forever
    private const int MaxChainedChanges = 32;

    private readonly List<(NeoState<TOwner> To, Func<bool> When)> _anyTransitions = new();
    private bool _changing;
    private bool _hasPending;
    private NeoState<TOwner> _pending;
    private bool _pendingRestart;

    public TOwner Owner { get; }

    public NeoState<TOwner> Current { get; private set; }

    public NeoState<TOwner> Previous { get; private set; }

    public bool IsRunning => Current is not null;

    public float TimeInState { get; private set; }

    public Action<NeoState<TOwner>, NeoState<TOwner>> OnStateChanged;

    public NeoStateMachine(TOwner owner)
    {
        Owner = owner;
    }

    /// <summary>Registers a state with this machine and returns it</summary>
    public T Add<T>(T state) where T : NeoState<TOwner>
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Machine == this) return state;
        if (state.Machine is not null)
            throw new InvalidOperationException($"{state.GetType().Name} already belongs to another state machine, make a new instance instead");

        state.Machine = this;
        return state;
    }

    /// <summary>Registers a state built from lambdas and returns it. name only feeds ToString, so debug logs can name the state.</summary>
    public NeoState<TOwner> AddState(string name = null, Action onEnter = null, Action<GameTime> onUpdate = null, Action onExit = null)
    {
        return Add(new NeoLambdaState<TOwner>(name, onEnter, onUpdate, onExit));
    }

    /// <summary>Switches from → to on the first Update where <paramref name="when"/> returns true while in <paramref name="from"/>.</summary>
    /// <remarks>Checked in add order, after every any-state transition. First match wins; at most one condition transition fires per Update (states can still switch more than once per tick through ChangeState/Start/Stop).</remarks>
    public void AddTransition(NeoState<TOwner> from, NeoState<TOwner> to, Func<bool> when)
    {
        Require(from);
        Require(to);
        ArgumentNullException.ThrowIfNull(when);
        if (from == to) throw new ArgumentException("a state can't transition into itself, use ChangeState(state, restart: true) to re-enter it");

        from.Transitions.Add((to, when));
    }

    /// <summary>Switches to <paramref name="to"/> from whatever state is current, e.g. any → Dead.</summary>
    /// <remarks>Checked before the current state's own transitions, in add order. Skipped while <paramref name="to"/> is already current</remarks>
    public void AddAnyTransition(NeoState<TOwner> to, Func<bool> when)
    {
        Require(to);
        ArgumentNullException.ThrowIfNull(when);
        _anyTransitions.Add((to, when));
    }

    /// <summary>Enters <paramref name="initial"/>. On a running machine this exits the current state first, re-entering even if it's the same one.</summary>
    public void Start(NeoState<TOwner> initial) => ChangeState(initial, restart: true);

    /// <summary>
    /// Exits the current state and enters <paramref name="next"/>. Asking for the current state does nothing unless <paramref name="restart"/> is true.
    /// </summary>
    /// <remarks>Called from OnEnter/OnExit/OnStateChanged, the change is queued and applied right after the running one finishes; the last request wins.</remarks>
    public void ChangeState(NeoState<TOwner> next, bool restart = false)
    {
        Require(next);
        Request(next, restart);
    }

    public void Stop() => Request(null, false);

    public void Update(GameTime gameTime)
    {
        if (Current is null) return;

        TimeInState += (float)gameTime.ElapsedGameTime.TotalSeconds;

        NeoState<TOwner> target = FindTransition();
        if (target is not null) Request(target, false);

        Current?.OnUpdate(gameTime);
    }

    private NeoState<TOwner> FindTransition()
    {
        for (int i = 0; i < _anyTransitions.Count; i++)
        {
            (NeoState<TOwner> to, Func<bool> when) = _anyTransitions[i];
            if (to != Current && when()) return to;
        }

        List<(NeoState<TOwner> To, Func<bool> When)> own = Current.Transitions;
        for (int i = 0; i < own.Count; i++)
            if (own[i].When()) return own[i].To;

        return null;
    }

    // next == set null to make it stop
    private void Request(NeoState<TOwner> next, bool restart)
    {
        if (_changing)
        {
            _pending = next;
            _pendingRestart = restart;
            _hasPending = true;
            return;
        }

        _hasPending = false;
        for (int chain = 0; ; chain++)
        {
            if (chain == MaxChainedChanges)
                throw new InvalidOperationException($"more than {MaxChainedChanges} state changes chained from OnEnter/OnExit/OnStateChanged; two states are probably switching to each other forever");

            Switch(next, restart);
            if (!_hasPending) return;

            next = _pending;
            restart = _pendingRestart;
            _pending = null;
            _hasPending = false;
        }
    }

    private void Switch(NeoState<TOwner> next, bool restart)
    {
        if (next == Current && !restart) return;

        NeoState<TOwner> from = Current;
        _changing = true;
        try
        {
            from?.OnExit();
            Previous = from;
            Current = next;
            TimeInState = 0;
            next?.OnEnter();
            OnStateChanged?.Invoke(from, next);
        }
        finally
        {
            _changing = false;
        }
    }

    private void Require(NeoState<TOwner> state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Machine != this) throw new InvalidOperationException($"{state.GetType().Name} isn't part of this state machine, Add() it first");
    }
}
