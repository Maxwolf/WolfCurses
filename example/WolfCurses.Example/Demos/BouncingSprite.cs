// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using WolfCurses.Graphics;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     A <see cref="Sprite" /> that carries a direction and a strip of frames: it moves, turns round at the edges,
    ///     and plays its animation as time passes.
    ///     <para>
    ///         All of this lives in the example rather than the library on purpose. <see cref="Sprite" /> has a settable
    ///         <see cref="Sprite.Image" /> precisely so that an animated sprite needs nothing new from the library — the
    ///         thing driving it swaps the picture for the next frame and the scene neither knows nor cares. This class is
    ///         the proof of that claim, and worth keeping as one until it is clear what a library-side animated sprite
    ///         would do that this does not.
    ///     </para>
    ///     <para>
    ///         Each instance owns its <b>own</b> frames rather than sharing one strip, because the demo scales every
    ///         sprite differently and a picture cannot be two sizes at once. It also starts on a frame of its caller's
    ///         choosing, which is what keeps a crowd of them from flashing in lockstep like one animation drawn five
    ///         times.
    ///     </para>
    /// </summary>
    internal sealed class BouncingSprite
    {
        /// <summary>
        ///     Shortest frame this will honour before treating the delay as "as fast as you can" and slowing it down.
        ///     GIF counts in hundredths and a great many files say zero; see the animated GIF demo for the same floor and
        ///     why a decoder is the wrong place for it.
        /// </summary>
        private static readonly TimeSpan _fastFrameThreshold = TimeSpan.FromMilliseconds(20);

        /// <summary>What a frame declaring no meaningful delay is shown for instead.</summary>
        private static readonly TimeSpan _fastFrameDelay = TimeSpan.FromMilliseconds(100);

        private readonly TimeSpan[] _delays;
        private readonly PixelBuffer[] _frames;

        private int _frameIndex;
        private TimeSpan _sinceFrameChanged;

        /// <summary>Initializes a new instance of the <see cref="BouncingSprite" /> class.</summary>
        /// <param name="frames">This sprite's own frames, already at the size it is to be drawn.</param>
        /// <param name="delays">How long each frame lasts, one per frame.</param>
        /// <param name="startFrame">Which frame to open on, so a crowd does not animate in lockstep.</param>
        /// <param name="x">Starting distance from the scene's left edge.</param>
        /// <param name="y">Starting distance from the scene's top edge.</param>
        /// <param name="deltaX">Pixels moved right each frame; negative for left.</param>
        /// <param name="deltaY">Pixels moved down each frame; negative for up.</param>
        public BouncingSprite(PixelBuffer[] frames, TimeSpan[] delays, int startFrame, int x, int y, int deltaX,
            int deltaY)
        {
            _frames = frames;
            _delays = delays;
            _frameIndex = startFrame % frames.Length;

            Sprite = new Sprite(_frames[_frameIndex], x, y);
            DeltaX = deltaX;
            DeltaY = deltaY;
        }

        /// <summary>The sprite itself, which is what goes into a <see cref="SpriteScene" />.</summary>
        public Sprite Sprite { get; }

        /// <summary>Pixels moved right each frame; negative for left.</summary>
        public int DeltaX { get; private set; }

        /// <summary>Pixels moved down each frame; negative for up.</summary>
        public int DeltaY { get; private set; }

        /// <summary>Moves the sprite one frame, turns it round at the edges, and advances its animation.</summary>
        /// <param name="elapsed">Real time since this was last called.</param>
        /// <param name="sceneWidth">Width of the scene it is bouncing around inside.</param>
        /// <param name="sceneHeight">Height of the scene it is bouncing around inside.</param>
        public void Advance(TimeSpan elapsed, int sceneWidth, int sceneHeight)
        {
            Move(sceneWidth, sceneHeight);
            Animate(elapsed);
        }

        /// <summary>Moves one step and reflects off the walls.</summary>
        private void Move(int sceneWidth, int sceneHeight)
        {
            Sprite.X += DeltaX;
            Sprite.Y += DeltaY;

            // Reflecting means clamping to the wall as well as reversing: without the clamp a sprite that overshot by a
            // pixel would spend the next frame still outside, reverse again, and shiver against the edge rather than
            // leave it.
            var maxX = Math.Max(0, sceneWidth - Sprite.Width);
            var maxY = Math.Max(0, sceneHeight - Sprite.Height);

            if (Sprite.X <= 0 || Sprite.X >= maxX)
            {
                Sprite.X = Math.Clamp(Sprite.X, 0, maxX);
                DeltaX = -DeltaX;
            }

            if (Sprite.Y <= 0 || Sprite.Y >= maxY)
            {
                Sprite.Y = Math.Clamp(Sprite.Y, 0, maxY);
                DeltaY = -DeltaY;
            }
        }

        /// <summary>Swaps in the next frame once the current one has had its time.</summary>
        private void Animate(TimeSpan elapsed)
        {
            if (_frames.Length < 2)
                return;

            _sinceFrameChanged += elapsed;

            var delay = _delays[_frameIndex];
            if (delay < _fastFrameThreshold)
                delay = _fastFrameDelay;

            if (_sinceFrameChanged < delay)
                return;

            _sinceFrameChanged = TimeSpan.Zero;
            _frameIndex = (_frameIndex + 1) % _frames.Length;

            // The whole of what makes this sprite animated: the scene is asked for nothing new, it simply draws
            // whatever picture the sprite is holding the next time it composes.
            Sprite.Image = _frames[_frameIndex];
        }
    }
}
