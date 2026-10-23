using System;
using Microsoft.Xna.Framework;

namespace NeoGameLib.Common.StateMachine;

// a state built from lambdas instead of a subclass, for simple/one-off states.
// the name only feeds ToString so debug logs and exception messages say which state is which.
internal sealed class NeoLambdaState<TOwner> : NeoState<TOwner>
{
    private readonly string _name;
    private readonly Action _onEnter;
    private readonly Action<GameTime> _onUpdate;
    private readonly Action _onExit;

    public NeoLambdaState(string name, Action onEnter, Action<GameTime> onUpdate, Action onExit)
    {
        _name = name;
        _onEnter = onEnter;
        _onUpdate = onUpdate;
        _onExit = onExit;
    }

    public override void OnEnter() => _onEnter?.Invoke();

    public override void OnUpdate(GameTime gameTime) => _onUpdate?.Invoke(gameTime);

    public override void OnExit() => _onExit?.Invoke();

    public override string ToString() => string.IsNullOrEmpty(_name) ? base.ToString() : _name;
}
