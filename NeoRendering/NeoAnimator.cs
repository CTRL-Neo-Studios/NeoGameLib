using System;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoRendering;

// note to future self:
// NeoAnimator works by setting source rectangle in the update loop to the frames defined in the NeoAnimation object.
// pause = set Enabled to false, resume = set it back
public class NeoAnimator : NeoComponent
{
    private NeoSpriteRenderer _renderer;
    private NeoAnimation _animation;
    private TimeSpan _elapsed;
    private TimeSpan _frameDuration;
    private bool _finishedFired;

    public int CurrentFrameIndex { get; private set; }

    // note to future self:
    // invokes when a non-looping animation runs past its last frame
    // can be used smth like attack->idle state-switching.
    public Action OnAnimationFinished;

    public override void OnAwake()
    {
        _renderer = ParentObject.GetComponent<NeoSpriteRenderer>();
        if (_renderer is null)
            throw new Exception("A Sprite Renderer component is required on an object with NeoAnimator");
    }

    public void Play(NeoAnimation animation)
    {
        if (animation is null || animation.Frames.Count == 0)
            throw new Exception("Playing a null or empty animation. SOmehow you've managed to do that but congrats");

        // note to future self:
        // prevents same animation called in update frame via Play func in case if you fuck up.
        // If that's what you do intend, either Stop() the animation first or Play() a different animation
        if (_animation == animation) return;

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
    }

    public override void OnUpdate(GameTime gameTime)
    {
        if (_animation is null || _renderer is null) return;
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
                else if (_renderer is not null)
                {
                    // vanish when finish play.
                    _renderer.SourceRectangle = Rectangle.Empty;
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
        if (_renderer is not null)
            _renderer.SourceRectangle = _animation.Frames[CurrentFrameIndex];
    }
}
