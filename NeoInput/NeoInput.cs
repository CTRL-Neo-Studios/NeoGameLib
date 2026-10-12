using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace NeoGameLib.NeoInput;

// note to future self:
// shorthand facade over a hidden input map. the map builds itself on first use, binds the
// five mouse buttons as actions, and registers with NeoGame.Singleton.InputBus. querying
// before the game initializes its input bus throws, same as any unregistered map.
public static class Input
{
    private static NeoInputMap _map;
    private static NeoInputBus _bus;

    public static NeoMouseAction LeftMouseButton { get; } = new("LeftMouseButton");
    public static NeoMouseAction MiddleMouseButton { get; } = new("MiddleMouseButton");
    public static NeoMouseAction RightMouseButton { get; } = new("RightMouseButton");
    public static NeoMouseAction XButton1 { get; } = new("XButton1");
    public static NeoMouseAction XButton2 { get; } = new("XButton2");

    // the current mouse position in window pixels
    public static Vector2 MousePosition => Mouse.GetState().Position.ToVector2();

    internal static NeoInputMap Map
    {
        get
        {
            if (_map is null)
            {
                _map = new NeoInputMap();
                _map.AddMouseButton("LeftMouseButton", MouseButtons.Left);
                _map.AddMouseButton("MiddleMouseButton", MouseButtons.Middle);
                _map.AddMouseButton("RightMouseButton", MouseButtons.Right);
                _map.AddMouseButton("XButton1", MouseButtons.XButton1);
                _map.AddMouseButton("XButton2", MouseButtons.XButton2);
            }

            // register with the game bus on first use or retry if the game is not initialized yet
            if (_bus is null)
            {
                _bus = NeoGame.Singleton?.InputBus;
                _bus?.Add(_map);
            }

            return _map;
        }
    }

    // neoGame calls this before its input refresh so the map exists and gets state in the very same frame before anything uses it
    internal static void EnsureRegistered()
    {
        _ = Map;
    }
}

public sealed class NeoMouseAction
{
    private readonly string _action;

    internal NeoMouseAction(string action)
    {
        _action = action;
    }

    // held in the current tick
    public bool IsDown() => Input.Map.IsDown(_action);

    // went down this tick
    public bool IsPressed() => Input.Map.IsPressed(_action);

    // went up this tick
    public bool IsReleased() => Input.Map.IsReleased(_action);
}
