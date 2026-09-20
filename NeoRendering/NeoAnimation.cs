using System.Collections.Generic;
using NeoGameLib.Common;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoRendering;

// note to future self:
// animation data, as in an ordered list of source rects on a sprite sheet plus how long each frame stays on screen
// if you need per-frame timing then you're making a cutscene and fuck you im not gonna write code for playig a fucking cutscene
public class NeoAnimation
{
    public List<Rectangle> Frames { get; }
    public float FrameDuration { get; } // seconds per frame
    public bool Loop { get; }
    public bool HoldLastFrame { get; }

    // holdLastFrame: when a non-looping animation ends, keep showing the last frame
    // (default). false = clear the sprite so it vanishes into thin air like a ghost.
    // does jack shit when Loop is true, obv
    public NeoAnimation(List<Rectangle> frames, float frameDuration, bool loop = true, bool holdLastFrame = true)
    {
        Frames = frames;
        FrameDuration = frameDuration;
        Loop = loop;
        HoldLastFrame = holdLastFrame;
    }

    // note to self:
    // procedural grid walking! i drew a 24 frame animation and I didn't feel like calculating bounds by hand and adding them each one by one, so...
    // gridSize = cells per row/column, cellSize = pixels per cell
    // padding = gap BETWEEN cells, margin = offset from the sheet's top-left corner to the first cell in pixels.
    // horizontal dir = the sweep across columns, Right = normal left-to-right reading, Left = mirrored
    // vertical dir = the sweep down rows, Down = normal top-to-bottom, Up = walk the sheet from the bottom
    // horizontalFirst = walk rows before columns (the normal way), false = walk down each column first (the fucking deranged way but just in case if sb is deranged like this hey you can use it)
    public static NeoAnimation FromGrid(Point gridSize, Point cellSize, float frameDuration, bool loop = true,
        Direction2D horizontal = Direction2D.Right, Direction2D vertical = Direction2D.Down,
        Point padding = default, Point margin = default, bool horizontalFirst = true, bool holdLastFrame = true)
    {
        List<Rectangle> frames = new();

        // one shared frame-adder so the two walk orders can't drift apart and that they only differ in which axis is the outer loop
        void AddFrame(int col, int row)
        {
            int x = (horizontal == Direction2D.Right ? col : gridSize.X - 1 - col) * (cellSize.X + padding.X) + margin.X;
            int y = (vertical == Direction2D.Down ? row : gridSize.Y - 1 - row) * (cellSize.Y + padding.Y) + margin.Y;
            frames.Add(new Rectangle(x, y, cellSize.X, cellSize.Y));
        }

        if (horizontalFirst)
        {
            for (int row = 0; row < gridSize.Y; row++)
                for (int col = 0; col < gridSize.X; col++)
                    AddFrame(col, row);
        }
        else
        {
            for (int col = 0; col < gridSize.X; col++)
                for (int row = 0; row < gridSize.Y; row++)
                    AddFrame(col, row);
        }

        return new NeoAnimation(frames, frameDuration, loop, holdLastFrame);
    }
}
