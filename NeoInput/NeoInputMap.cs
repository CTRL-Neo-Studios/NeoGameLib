using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace NeoGameLib.NeoInput;

// note to future self:
// a named collection of input actions. digital actions fire when ANY of their bound
// keys is down, axis actions map a key pair through a NeoAxisRange. register the map
// on NeoGame.InputBus and it gets refreshed every frame from the bus' single keyboard
// snapshot. querying an action that was never added throws, a typo is a bug and not
// a silent zero

/// <summary>
/// Similar to Unity/Unreal's take on mapping inputs to an action, this one also maps key inputs to actions.
/// These actions invoke when any of their mapped key inputs are down; as for the axis actions, they have to map a key pair through NeoAxisRange.
/// </summary>
public class NeoInputMap
{
    // action name -> bound keys
    private Dictionary<string, List<Keys>> _digitals = new();
    // action name -> axis source
    private Dictionary<string, NeoAxisSource> _axes = new();

    // per-tick results, filled by the bus' Refresh
    private Dictionary<string, bool> _down = new(), _pressed = new(), _released = new();
    private Dictionary<string, float> _axisValues = new();

    // any of the bound keys invokes the action
    public void AddDigital(string action, params Keys[] keys)
    {
        _digitals[action] = new List<Keys>(keys);
    }

    public void AddAxis(string action, NeoAxisRange range, Keys? positive = null, Keys? negative = null)
    {
        _axes[action] = new NeoAxisSource(range, positive, negative);
    }

    /// <summary>
    /// Overrides an EXISTING action's bindings
    /// </summary>
    /// <param name="action">Target input action</param>
    /// <param name="keys">Keys to bind to the overriding action</param>
    /// <exception cref="KeyNotFoundException">throws when the action you're trying to modify doesn't exist</exception>
    public void SetAction(string action, params Keys[] keys)
    {
        if (!_digitals.ContainsKey(action))
            throw new KeyNotFoundException($"digital action '{action}' was never added");

        _digitals[action] = new List<Keys>(keys);
    }

    public void SetAction(string action, NeoAxisRange range, Keys? positive = null, Keys? negative = null)
    {
        if (!_axes.ContainsKey(action))
            throw new KeyNotFoundException($"axis action '{action}' was never added");

        _axes[action] = new NeoAxisSource(range, positive, negative);
    }

    public bool HasAction(string action)
    {
        return _digitals.ContainsKey(action) || _axes.ContainsKey(action);
    }

    // deletes the action entirely
    public void RemoveAction(string action)
    {
        _digitals.Remove(action);
        _axes.Remove(action);
        _down.Remove(action);
        _pressed.Remove(action);
        _released.Remove(action);
        _axisValues.Remove(action);
    }

    // whether key was held in the current tick
    public bool IsDown(string action)
    {
        return _down[action];
    }

    // whether the key is down this tick but not the last tick
    public bool IsPressed(string action)
    {
        return _pressed[action];
    }

    // whether the key is up this tick but not the last tick
    public bool IsReleased(string action)
    {
        return _released[action];
    }

    public float GetAxis(string action)
    {
        return _axisValues[action];
    }

    /// <summary>
    /// registers this input map to the bus to update action states as the first lifecycle of every update-tick loop.
    /// </summary>
    /// <param name="bus">The input bus. Defaults to the singleton input bus if param is left null</param>
    /// <param name="dedupe">If this input map is registered in the bus before, it won't register when you call this function</param>
    public void Subscribe(NeoInputBus bus = null, bool dedupe = true)
    {
        (bus ?? NeoGame.Singleton?.InputBus)?.Register(this, dedupe);
    }

    /// <summary>
    /// unregisters this input map from the given bus.
    /// </summary>
    /// <param name="bus">If the bus is empty, this will attempt to unsubscribe from the default singleton input bus.</param>
    /// <param name="dedupe">If this input map has been registered more than once, unsubscribing with dedupe will unsub ALL instances of this input map from the bus</param>
    public void Unsubscribe(NeoInputBus bus = null, bool dedupe = true)
    {
        (bus ?? NeoGame.Singleton?.InputBus)?.Unregister(this, dedupe);
    }

    // the bus calls this every frame with its prev/current snapshots, don't touch
    internal void Refresh(KeyboardState previous, KeyboardState current)
    {
        foreach (KeyValuePair<string, List<Keys>> entry in _digitals)
        {
            bool wasDown = false;
            bool isDown = false;

            foreach (Keys key in entry.Value) // goes through the mapped keys to this action to check if one of them was pressed or not
            {
                if (previous.IsKeyDown(key)) wasDown = true;
                if (current.IsKeyDown(key)) isDown = true;
            }

            // and sets the corresponding state booleans to the actions
            _down[entry.Key] = isDown;
            _pressed[entry.Key] = isDown && !wasDown;
            _released[entry.Key] = !isDown && wasDown;
        }

        
        foreach (KeyValuePair<string, NeoAxisSource> entry in _axes)
        {
            NeoAxisSource source = entry.Value;
            int raw = 0;
            if (source.Positive.HasValue && current.IsKeyDown(source.Positive.Value)) raw += 1;
            if (source.Negative.HasValue && current.IsKeyDown(source.Negative.Value)) raw -= 1;

            // I actually learned how to use a switch expression in this syntax from this article: https://www.code4it.dev/csharptips/switch-expressions-and-statements/
            // was using it this way because in typescript/javascript switch expressions have a similar syntax so I was wondering whether csharp has one, and it does apparently has one
            _axisValues[entry.Key] = source.Range switch
            {
                NeoAxisRange.Positive => raw > 0 ? 1f : 0f,
                NeoAxisRange.Negative => raw < 0 ? -1f : 0f,
                NeoAxisRange.Full => raw,
                NeoAxisRange.PositiveInverted => raw > 0 ? 0f : 1f,
                NeoAxisRange.NegativeInverted => raw < 0 ? 0f : -1f,
                _ => -raw,
            };
        }
    }

    private struct NeoAxisSource
    {
        public NeoAxisRange Range;
        public Keys? Positive;
        public Keys? Negative;

        public NeoAxisSource(NeoAxisRange range, Keys? positive, Keys? negative)
        {
            Range = range;
            Positive = positive;
            Negative = negative;
        }
    }
}
