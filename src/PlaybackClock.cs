// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Diagnostics;

namespace WolfCurses
{
    /// <summary>
    ///     Where you are in something that has a length: a video, a piece of audio, an animation, a recorded
    ///     session being played back. It answers "what time is it in the media", survives being paused, and can be
    ///     put somewhere else without being restarted.
    ///     <para>
    ///         <b>This is a position, not a pace, and that is the whole difference from
    ///         <see cref="IntervalTimer" />.</b> That type drops a late period on purpose, because repaying the debt
    ///         is a sprite teleporting. This one must never drop anything: a frame's time is a fact about the media
    ///         rather than about how often somebody asked, so falling behind means <i>skipping frames to catch
    ///         up</i>, not slowing the film down. Note <see cref="SimulationApp" /> takes the third position again
    ///         for its own heartbeat, which must not drift. Three timing types in one library with three different
    ///         rules, all on purpose; do not unify them.
    ///     </para>
    ///     <para>
    ///         <b><see cref="Position" /> mutates nothing</b>, the same naming discipline as
    ///         <see cref="IntervalTimer.IsDue" /> and <see cref="HeldAxis.Direction" />: every caller of this also
    ///         has an <c>OnRenderForm</c> the scene graph runs about a thousand times a second, and anything that
    ///         quietly advanced state when merely asked would be read from there.
    ///     </para>
    ///     <para>
    ///         It knows nothing about frames, files, decoders or sound. Feed it a clock and it tells you a time,
    ///         which is what makes it testable without waiting for anything to play.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     private readonly PlaybackClock _clock = new();
    ///
    ///     // On every tick: catch up to where the media should be, dropping whatever is late.
    ///     var wanted = _clock.FrameAt(fps);
    ///     while (_shown &lt; wanted &amp;&amp; _pipe.TryRead(out var frame))
    ///     {
    ///         _shown++;
    ///         _latest = frame;
    ///     }
    ///     </code>
    /// </example>
    public sealed class PlaybackClock
    {
        /// <summary>
        ///     Reads a monotonically increasing time. A <see cref="Stopwatch" /> in production; a test hands in
        ///     something it can wind forward, so nothing in the suite has to play anything to measure this.
        /// </summary>
        private readonly Func<TimeSpan> _clock;

        /// <summary>The media time the current run started from.</summary>
        private TimeSpan _origin;

        /// <summary>The clock reading the current run started at; meaningless while paused.</summary>
        private TimeSpan _startedAt;

        /// <summary>Initializes a clock, stopped at the beginning.</summary>
        public PlaybackClock() : this(null)
        {
        }

        /// <summary>
        ///     Initializes a clock against a supplied time source. The seam the tests drive; production always takes
        ///     the <see cref="Stopwatch" />.
        /// </summary>
        /// <param name="clock">The clock to read, or null for a fresh <see cref="Stopwatch" />.</param>
        internal PlaybackClock(Func<TimeSpan> clock)
        {
            if (clock == null)
            {
                var stopwatch = Stopwatch.StartNew();
                clock = () => stopwatch.Elapsed;
            }

            _clock = clock;
        }

        /// <summary>
        ///     How long the media runs for, or <see cref="TimeSpan.Zero" /> when that is not known.
        ///     <para>
        ///         <b>Unknown is a real answer and is treated as one.</b> A live stream and a pipe have no length,
        ///         and everything that would divide by it says so instead of guessing: <see cref="Progress" /> stays
        ///         at zero, <see cref="HasEnded" /> is never true, and a seek is not clamped at the top. A bar that
        ///         jumps to full because the length was assumed is worse than one that never moves.
        ///     </para>
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Whether the clock is running. Paused and stopped both answer false.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        ///     What time it is in the media. Never runs past <see cref="Duration" /> when one is known, so a caller
        ///     drawing a position does not have to clamp what it is given.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                var at = IsRunning ? _origin + (_clock() - _startedAt) : _origin;

                if (at < TimeSpan.Zero)
                    return TimeSpan.Zero;

                return Duration > TimeSpan.Zero && at > Duration ? Duration : at;
            }
        }

        /// <summary>How far through, from zero to one, or zero when there is no known length to be a fraction of.</summary>
        public double Progress =>
            Duration > TimeSpan.Zero
                ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0d, 1d)
                : 0d;

        /// <summary>Whether the end has been reached. Never true without a known length, since nothing else could be.</summary>
        public bool HasEnded => Duration > TimeSpan.Zero && Position >= Duration;

        /// <summary>Starts from the beginning.</summary>
        public void Start()
        {
            _origin = TimeSpan.Zero;
            _startedAt = _clock();
            IsRunning = true;
        }

        /// <summary>
        ///     Stops the clock where it is. Asking again does nothing, rather than banking the time twice, which is
        ///     what a pause key held down would otherwise do.
        /// </summary>
        public void Pause()
        {
            if (!IsRunning)
                return;

            _origin = Position;
            IsRunning = false;
        }

        /// <summary>Starts again from wherever it was paused. Asking while it is already running does nothing.</summary>
        public void Resume()
        {
            if (IsRunning)
                return;

            _startedAt = _clock();
            IsRunning = true;
        }

        /// <summary>Pauses and rewinds to the beginning, which is what closing a file leaves behind.</summary>
        public void Stop()
        {
            IsRunning = false;
            _origin = TimeSpan.Zero;
        }

        /// <summary>
        ///     Moves to a position, clamped into the media.
        ///     <para>
        ///         <b>Seeking does not start or stop anything</b>, and that half is what makes a scrub bar work: a
        ///         paused player being dragged along must stay paused, and a playing one must not stutter into a
        ///         pause because somebody touched the bar.
        ///     </para>
        /// </summary>
        /// <param name="position">Where to go.</param>
        public void SeekTo(TimeSpan position)
        {
            if (position < TimeSpan.Zero)
                position = TimeSpan.Zero;

            if (Duration > TimeSpan.Zero && position > Duration)
                position = Duration;

            _origin = position;
            _startedAt = _clock();
        }

        /// <summary>Moves by an amount, forwards or back, which is what a skip key does.</summary>
        /// <param name="delta">How far; negative goes back.</param>
        public void Seek(TimeSpan delta)
        {
            SeekTo(Position + delta);
        }

        /// <summary>
        ///     Which frame belongs on screen now, counting from zero.
        ///     <para>
        ///         A frame <i>number</i> rather than a time, because catching up is written against it and cannot be
        ///         written against a time: keep pulling frames while the one you have shown is behind this, and what
        ///         falls out is dropping exactly the frames that are late. It floors rather than rounds, so a frame
        ///         is never shown before its own moment.
        ///     </para>
        /// </summary>
        /// <param name="framesPerSecond">The media's frame rate. Zero or less answers zero rather than dividing.</param>
        /// <returns>The frame index that belongs on screen.</returns>
        public long FrameAt(double framesPerSecond)
        {
            if (framesPerSecond <= 0d)
                return 0L;

            var at = Position.TotalSeconds * framesPerSecond;

            return at <= 0d ? 0L : (long) Math.Floor(at);
        }

        /// <summary>
        ///     When a frame of a constant-rate stream is due, which is the inverse of <see cref="FrameAt" />: seek
        ///     here and that frame is the one <see cref="FrameAt" /> asks for.
        ///     <para>
        ///         <b>Built from ticks and rounded up, and both halves are load-bearing.</b>
        ///         <c>TimeSpan.FromSeconds</c> is only accurate to the nearest <i>millisecond</i>, which is far too
        ///         coarse here: at 23.976 frames a second, frame 37 is due at 1.5432098 seconds, that rounds to
        ///         1.543, and asking which frame belongs at 1.543 gives back <b>36</b>. A caller catching up would
        ///         then show the same frame twice and never make progress. Rounding up rather than to nearest is the
        ///         other half: a frame's moment must land at or after the true one, or the frame is due a hair
        ///         before its own time and the inverse fails from the opposite side.
        ///     </para>
        /// </summary>
        /// <param name="frame">The frame index, counting from zero.</param>
        /// <param name="framesPerSecond">The media's frame rate. Zero or less answers zero.</param>
        /// <returns>The frame's own moment.</returns>
        public static TimeSpan FrameTime(long frame, double framesPerSecond)
        {
            if (framesPerSecond <= 0d || frame <= 0L)
                return TimeSpan.Zero;

            return TimeSpan.FromTicks((long) Math.Ceiling(frame / framesPerSecond * TimeSpan.TicksPerSecond));
        }
    }
}
