using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.Common;

namespace NeoGameLib.NeoRendering.Animations;

// note to future self:
// animation data, as in an ordered list of source rects on a sprite sheet plus how long each frame stays on screen
// if you need per-frame timing then you're making a cutscene and fuck you im not gonna write code for playig a fucking cutscene
public class NeoAnimation
{
    public List<Rectangle> Frames { get; }
    public float FrameDuration { get; } // seconds per frame
    public bool Loop { get; }
    public bool HoldLastFrame { get; }

    /// <summary>Optional texture for this clip: while it plays the animator swaps the sprite's texture to it, and texture-less clips swap back to whatever the sprite had before. null = keep the sprite's own texture.</summary>
    public Texture2D? Texture { get; }

    // holdLastFrame: when a non-looping animation ends, keep showing the last frame
    public NeoAnimation(List<Rectangle> frames, float frameDuration, bool loop = true, bool holdLastFrame = true, Texture2D? texture = null)
    {
        Frames = frames;
        FrameDuration = frameDuration;
        Loop = loop;
        HoldLastFrame = holdLastFrame;
        Texture = texture;
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
        return new NeoAnimation(BuildFrames(gridSize, cellSize, horizontal, vertical, padding, margin, horizontalFirst), frameDuration, loop, holdLastFrame);
    }

    /// <summary>grid walk over a whole texture: gridSize is worked out from the sheet and cellSize, and the sheet is stored as the clip's texture so the animator swaps it in automatically.</summary>
    public static NeoAnimation FromGrid(Texture2D sheet, Point cellSize, float frameDuration, bool loop = true,
        Direction2D horizontal = Direction2D.Right, Direction2D vertical = Direction2D.Down,
        Point padding = default, Point margin = default, bool horizontalFirst = true, bool holdLastFrame = true)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        Point gridSize = new(sheet.Width / cellSize.X, sheet.Height / cellSize.Y);
        return new NeoAnimation(BuildFrames(gridSize, cellSize, horizontal, vertical, padding, margin, horizontalFirst), frameDuration, loop, holdLastFrame, sheet);
    }

    // one shared frame-adder so the two walk orders can't drift apart and that they only differ in which axis is the outer loop
    private static List<Rectangle> BuildFrames(Point gridSize, Point cellSize,
        Direction2D horizontal, Direction2D vertical, Point padding, Point margin, bool horizontalFirst)
    {
        List<Rectangle> frames = new();

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

        return frames;
    }
}
