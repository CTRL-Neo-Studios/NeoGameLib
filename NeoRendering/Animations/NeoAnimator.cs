using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoRendering.Animations;

// note to future self:
// NeoAnimator works by setting source rectangle in the update loop to the frames defined in the NeoAnimation object.
// pause = set Enabled to false, resume = set it back
// the sprite renderer is looked up lazily, so the animator doesn't care whether it was added before or after
public class NeoAnimator : NeoComponent
{
    private NeoSpriteRenderer _renderer;
    private Texture2D _baseTexture;
    private bool _swapped; // true while the renderer shows a clip's texture instead of its own
    private NeoAnimation _animation;
    private TimeSpan _elapsed;
    private TimeSpan _frameDuration;
    private bool _finishedFired;

    public int CurrentFrameIndex { get; private set; }

    /// <summary>The clip being played; null before the first Play and after Stop.</summary>
    public NeoAnimation Animation => _animation;

    /// <summary>Is true if a non-looping clip had ran past its last frame.</summary>
    /// <remarks>The polling twin of OnAnimationFinished for state machine transitions i.e. <c>AddTransition(attack, idle, () => animator.IsFinished)</c>.</remarks>
    public bool IsFinished => _finishedFired;

    // note to future self:
    // invokes when a non-looping animation runs past its last frame
    // for attack->idle style switching, NeoAnimatorController + IsFinished is the cleaner way; this stays for one-off reactions
    public Action OnAnimationFinished;

    // finds the sprite renderer on first use and again if it was removed/replaced
    private NeoSpriteRenderer Renderer()
    {
        if (_renderer is not { IsAttached: true })
        {
            _renderer = ParentObject.GetComponent<NeoSpriteRenderer>();
            _swapped = false; // a new renderer keeps its own texture until a textured clip plays
        }
        return _renderer;
    }

    /// <param name="animation">the clip to play</param>
    /// <param name="restart">replay from frame 0 even when this clip is already playing, e.g. a second attack right after the first</param>
    public void Play(NeoAnimation animation, bool restart = false)
    {
        if (animation is null || animation.Frames is null || animation.Frames.Count == 0)
            throw new Exception("Playing a null or empty animation. SOmehow you've managed to do that but congrats");

        if (_animation == animation && !restart)
        {
            // same clip, but the renderer may have been added (or re-added) since: apply the frame again
            ApplyFrame();
            return;
        }

        _animation = animation;
        CurrentFrameIndex = 0;
        _elapsed = TimeSpan.Zero;
        _frameDuration = TimeSpan.FromSeconds(animation.FrameDuration);
        _finishedFired = false;
        ApplyFrame();
    }

    public void Stop()
    {
        _animation = null;
        _finishedFired = false;
    }

    public override void OnUpdate(GameTime gameTime)
    {
        if (_animation is null) return;
        _elapsed += gameTime.ElapsedGameTime;

        // comparing in ticks
        if (_elapsed < _frameDuration) return;

        _elapsed -= _frameDuration;
        CurrentFrameIndex++;

        if (CurrentFrameIndex >= _animation.Frames.Count)
        {
            if (_animation.Loop)
            {
                CurrentFrameIndex = 0;
            }
            else
            {
                // apply the end visual FIRST and fire the callback LAST so a callback that switches animations via Play() always wins
                if (_animation.HoldLastFrame)
                {
                    // stay on the last frame so the sprite doesn't disappear somehow
                    CurrentFrameIndex = _animation.Frames.Count - 1;
                    ApplyFrame();
                }
                else
                {
                    NeoSpriteRenderer renderer = Renderer();
                    if (renderer is not null)
                    {
                        // vanish when finish play.
                        renderer.SourceRectangle = Rectangle.Empty;
                    }
                }

                if (!_finishedFired)
                {
                    _finishedFired = true;
                    OnAnimationFinished?.Invoke();
                }

                return;
            }
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        NeoSpriteRenderer renderer = Renderer();
        if (renderer is null) return;

        if (_animation.Texture is not null)
        {
            if (!_swapped)
            {
                _baseTexture = renderer.Texture;
                _swapped = true;
            }
            renderer.Texture = _animation.Texture;
        }
        else if (_swapped)
        {
            renderer.Texture = _baseTexture;
            _swapped = false;
        }

        renderer.SourceRectangle = _animation.Frames[CurrentFrameIndex];
    }
}
