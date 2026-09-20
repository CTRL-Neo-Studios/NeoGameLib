using System;

namespace NeoGameLib.Common;

// note to future self:
// a standalone reusable timer. NOT a component, not attached to any object, just a
// countdown you make once and Start/Stop/Pause/Resume forever, no need to new one up
// every time you need a delay. (async/await DOES work in monogame btw, but a
// Task.Delay doesn't pause with the game and cancelling it cleanly is a pain, so this)
//
// callbacks (all Action, set like animator.OnAnimationFinished):
//   OnStart / OnStop / OnPause / OnResume fire on the matching call
//   OnFinished fires when time hits zero, then the timer goes back to idle
// no Loop property: repeating = OnFinished = () => Start(), look at you go
//
// it registers with NeoGame.Singleton.TimerBus while running and unregisters on
// stop/finish, so the bus never holds dead timers and restarting is free
public class NeoTimer
{
    public float Duration { get; set; }
    public float TimeLeft { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }

    public Action OnStart;
    public Action OnStop;
    public Action OnPause;
    public Action OnResume;
    public Action OnFinished;

    private bool _registered;

    public NeoTimer(float duration)
    {
        Duration = duration;
    }

    // starts/RESTARTS from full duration, from any state. DONT CALL THIS IN AN UPDATE LOOP
    public void Start()
    {
        Register();

        TimeLeft = Duration;
        IsRunning = true;
        IsPaused = false;
        OnStart?.Invoke();
    }

    public void Stop()
    {
        if (!IsRunning && !IsPaused) return;

        Unregister();
        TimeLeft = 0;
        IsRunning = false;
        IsPaused = false;
        OnStop?.Invoke();
    }

    public void Pause()
    {
        if (!IsRunning || IsPaused) return;

        IsPaused = true;
        OnPause?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        OnResume?.Invoke();
    }

    // the bus calls this, don't touch this
    internal void Tick(float deltaSeconds)
    {
        if (!IsRunning || IsPaused) return;

        TimeLeft -= deltaSeconds;
        if (TimeLeft > 0) return;

        TimeLeft = 0;
        IsRunning = false;
        Unregister();
        OnFinished?.Invoke();
    }

    private void Register()
    {
        if (_registered) return;
        _registered = true;
        NeoGame.Singleton?.TimerBus.Add(this);
    }

    private void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        NeoGame.Singleton?.TimerBus.Remove(this);
    }
}
