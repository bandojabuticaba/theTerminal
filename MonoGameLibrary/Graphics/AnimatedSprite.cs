using System;
using Microsoft.Xna.Framework;

namespace MonoGameLibrary.Graphics;

public class AnimatedSprite : Sprite 
{
    private int _currentFrame;
    private TimeSpan _elapsed;
    private Animation _animation;

    /// <summary>
    /// Gets or Sets the animation for this animated sprite. Setting it restarts
    /// playback from the animation's first frame.
    /// </summary>
    public Animation Animation
    {
        get => _animation;
        set
        {
            _animation = value;
            _currentFrame = 0;
            _elapsed = TimeSpan.Zero;
            IsFinished = false;
            Region = _animation.Frames[0];
        }
    }

    /// <summary>
    /// Gets or Sets whether the animation starts over after its last frame.
    /// </summary>
    /// <remarks>
    /// Default value is true. When false, the sprite holds the last frame and
    /// <see cref="IsFinished"/> becomes true.
    /// </remarks>
    public bool IsLooping { get; set; } = true;

    /// <summary>
    /// Gets whether a non-looping animation has reached and is holding its last frame.
    /// </summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// Creates a new animated sprite.
    /// </summary>
    public AnimatedSprite() { }

    /// <summary>
    /// Creates a new animated sprite with the specified frames and delay.
    /// </summary>
    /// <param name="animation">The animation for this animated sprite.</param>
    public AnimatedSprite(Animation animation)
    {
        Animation = animation;
    }

    /// <summary>
    /// Updates this animated sprite.
    /// </summary>
    /// <param name="gameTime">A snapshot of the game timing values provided by the framework.</param>
    public void Update(GameTime gameTime)
    {
        if (IsFinished)
        {
            return;
        }

        _elapsed += gameTime.ElapsedGameTime;

        if (_elapsed >= _animation.Delay)
        {
            _elapsed -= _animation.Delay;
            _currentFrame++;

            if (_currentFrame >= _animation.Frames.Count)
            {
                if (IsLooping)
                {
                    _currentFrame = 0;
                }
                else
                {
                    _currentFrame = _animation.Frames.Count - 1;
                    IsFinished = true;
                }
            }

            Region = _animation.Frames[_currentFrame];
        }
    }
}
