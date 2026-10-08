using Microsoft.Xna.Framework;

namespace NeoGameLib.Common;

public enum Direction2D
{
    Left,
    Right,
    Up,
    Down
}

// note to future self:
// small extended functional math for the enum. you could use it with things like NeoSpriteRenderer.IsOverWindowBounds
public static class Direction2DExtensions
{
    public static Vector2 ToVector2(this Direction2D dir)
    {
        return dir switch
        {
            Direction2D.Left => new(-1, 0),
            Direction2D.Right => new(1, 0),
            Direction2D.Up => new(0, -1),
            Direction2D.Down => new(0, 1),
            _ => Vector2.Zero
        };
    }

    public static Direction2D Invert(this Direction2D dir)
    {
        return dir switch
        {
            Direction2D.Left => Direction2D.Right,
            Direction2D.Right => Direction2D.Left,
            Direction2D.Up => Direction2D.Down,
            Direction2D.Down => Direction2D.Up,
            _ => dir
        };
    }
}
