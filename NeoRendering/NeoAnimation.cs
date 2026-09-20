using System.Collections.Generic;
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

    public NeoAnimation(List<Rectangle> frames, float frameDuration, bool loop = true)
    {
        Frames = frames;
        FrameDuration = frameDuration;
        Loop = loop;
    }
}
