// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Diagnostics;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Measures how often frames are being produced and what each one costs, for the demos that draw continuously.
    ///     <para>
    ///         The two numbers answer different questions and it is worth keeping them apart. <b>ms/frame</b> is what the
    ///         work costs — composing a scene and rendering it to a string — and is what moves when the picture being
    ///         drawn gets bigger or smaller. <b>fps</b> is how often that work actually happened, which is decided by
    ///         whatever frame budget the demo set itself and by how quickly the host loop comes back round, so it says
    ///         nothing about the cost at all. They only meet if a frame ever costs more than its budget, at which point
    ///         fps falls and ms/frame is the reason.
    ///     </para>
    ///     <para>
    ///         Both are averaged over <see cref="_samplePeriod" /> rather than taken from the last frame. Dividing one
    ///         frame's time into a second gives a number that jumps several frames wide on any scheduling hiccup and
    ///         cannot be read; averaging over a stretch costs nothing, still reacts within an eyeblink, and holds still
    ///         enough to compare against.
    ///     </para>
    ///     <para>
    ///         Do not be surprised by fps a little under whatever a demo asked for, on Windows especially: this app's
    ///         loop sleeps a millisecond between ticks and the default timer granularity there is nearer fifteen, so a
    ///         demo's frame budget is tested rather less often than its code suggests. That is the host's loop showing
    ///         through, and ms/frame staying low while fps sits below target is exactly how it shows.
    ///     </para>
    /// </summary>
    internal sealed class FrameCounter
    {
        /// <summary>How long the numbers are averaged over before they are published.</summary>
        private static readonly TimeSpan _samplePeriod = TimeSpan.FromMilliseconds(500);

        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private TimeSpan _costThisSample;
        private int _framesThisSample;

        /// <summary>Frames produced per second over the last complete period; zero until the first one is up.</summary>
        public double FramesPerSecond { get; private set; }

        /// <summary>What producing a frame cost on average over the last complete period.</summary>
        public double MillisecondsPerFrame { get; private set; }

        /// <summary>Folds one frame into the running measurements, publishing them when a period is up.</summary>
        /// <param name="cost">What producing this frame took.</param>
        public void Record(TimeSpan cost)
        {
            _framesThisSample++;
            _costThisSample += cost;

            if (_clock.Elapsed < _samplePeriod)
                return;

            FramesPerSecond = _framesThisSample / _clock.Elapsed.TotalSeconds;
            MillisecondsPerFrame = _costThisSample.TotalMilliseconds / _framesThisSample;

            _framesThisSample = 0;
            _costThisSample = TimeSpan.Zero;
            _clock.Restart();
        }

        /// <summary>The measurements as a line of text, or a note that there are none yet.</summary>
        public string Describe()
        {
            // A rate invented from a single frame would be a lie told precisely, so until a period has passed this says
            // so instead.
            return FramesPerSecond > 0
                ? $"{FramesPerSecond:F1} fps | {MillisecondsPerFrame:F2} ms/frame"
                : "measuring...";
        }
    }
}
