// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Diagnostics;

namespace WolfCurses
{
    /// <summary>
    ///     Paces something that happens on real elapsed time rather than on ticks: a sprite step, an animation frame,
    ///     a piece falling. Ask it whether the interval has come round, and if it has it takes the period and says so.
    ///     <para>
    ///         <b>Why not just count ticks?</b> Because a tick is not a unit of time. The host loop calls
    ///         <see cref="SimulationApp.OnTick" /> as fast as it likes, and a typical host sleeps a millisecond
    ///         between turns — which on Windows means about fifteen, because that is the default timer granularity.
    ///         Anything paced by counting ticks runs at whatever speed the machine happens to produce, and the
    ///         simulation tick, the only fixed one, fires once a second.
    ///     </para>
    ///     <para>
    ///         <b>This type has exactly one opinion, and it is that a late period is dropped rather than owed.</b>
    ///         When <see cref="TryConsume()" /> succeeds it starts the next period from <i>now</i>, not from when the
    ///         last one was due. Carrying the overshoot forward instead would be more accurate and is the wrong
    ///         behaviour here: after a slow frame the debt is repaid as a burst of instant steps, which is a sprite
    ///         teleporting or a tetromino falling two rows for one gravity tick. Note that
    ///         <see cref="SimulationApp" /> deliberately does the opposite for its own simulation tick, because a
    ///         once-a-second heartbeat must not drift; a frame must not sprint. Same repository, opposite rules, both
    ///         on purpose.
    ///     </para>
    ///     <para>
    ///         <b><see cref="TryConsume()" /> mutates and <see cref="IsDue" /> does not</b>, which is why the first
    ///         is not called <c>Due()</c>. Every caller of this type also has an <c>OnRenderForm</c> that the scene
    ///         graph invokes on every system tick — roughly a thousand times a second — and a predicate-shaped name
    ///         is exactly what gets written inside a render method, where it would silently swallow periods and halve
    ///         the pace with nothing thrown and nothing to see. Ask <see cref="IsDue" /> when you only want to look.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     private readonly IntervalTimer _step = new(TimeSpan.FromMilliseconds(130));
    ///
    ///     public override void OnTick(bool systemTick, bool skipDay)
    ///     {
    ///         base.OnTick(systemTick, skipDay);
    ///         if (!_step.TryConsume())
    ///             return;
    ///
    ///         Advance();
    ///     }
    ///     </code>
    /// </example>
    public sealed class IntervalTimer
    {
        /// <summary>
        ///     Reads a monotonically increasing time. A <see cref="Stopwatch" /> in production; a test hands in
        ///     something it can wind forward, so nothing in the suite has to sleep to measure a timer.
        /// </summary>
        private readonly Func<TimeSpan> _clock;

        /// <summary>The clock reading this timer was created at; <see cref="TotalElapsed" /> counts from here and nothing moves it.</summary>
        private readonly TimeSpan _created;

        /// <summary>The clock reading the current period started at.</summary>
        private TimeSpan _periodStart;

        /// <summary>
        ///     Initializes a new instance of the <see cref="IntervalTimer" /> class, running from now.
        /// </summary>
        /// <param name="interval">
        ///     How long a period lasts. Required rather than defaulted, because a zero interval makes
        ///     <see cref="TryConsume()" /> true on every call — a silent thousand-steps-a-second that reads as a bug
        ///     in the game rather than a mistake at the constructor.
        /// </param>
        public IntervalTimer(TimeSpan interval) : this(interval, null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="IntervalTimer" /> class against a supplied clock. The
        ///     seam the tests drive; production always takes the <see cref="Stopwatch" />.
        /// </summary>
        /// <param name="interval">How long a period lasts.</param>
        /// <param name="clock">The clock to read, or null for a fresh <see cref="Stopwatch" />.</param>
        internal IntervalTimer(TimeSpan interval, Func<TimeSpan> clock)
        {
            if (interval < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "An interval cannot run backwards.");

            if (clock == null)
            {
                var stopwatch = Stopwatch.StartNew();
                clock = () => stopwatch.Elapsed;
            }

            _clock = clock;
            Interval = interval;
            _created = _clock();
            _periodStart = _created;
        }

        /// <summary>
        ///     How long a period lasts. Settable, so a pace that changes with difficulty can be changed in one place;
        ///     a pace that changes every step is better passed to <see cref="TryConsume(TimeSpan)" /> instead.
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>How long the current period has been running. Reset by a successful consume and by <see cref="Restart" />.</summary>
        public TimeSpan Elapsed => _clock() - _periodStart;

        /// <summary>
        ///     How long since this timer was created. Never reset by anything — not by consuming a period and not by
        ///     <see cref="Restart" /> — so it is the clock to phase an animation against, where restarting would make
        ///     the animation jump.
        /// </summary>
        public TimeSpan TotalElapsed => _clock() - _created;

        /// <summary>
        ///     The length of the period the last successful <see cref="TryConsume()" /> closed, overshoot included —
        ///     so a caller that needs the real delta between steps can have it, rather than assuming
        ///     <see cref="Interval" /> and being wrong by however late the tick was.
        /// </summary>
        public TimeSpan LastElapsed { get; private set; }

        /// <summary>
        ///     Whether the current period has run out, <b>without</b> taking it. Safe to call from a render method or
        ///     to show on screen; <see cref="TryConsume()" /> is the one that advances.
        /// </summary>
        public bool IsDue => Elapsed >= Interval;

        /// <summary>
        ///     Takes the current period if it has run out, and starts the next one from now.
        /// </summary>
        /// <returns>True when the interval had elapsed and the period was taken; false when it is not time yet.</returns>
        public bool TryConsume()
        {
            return TryConsume(Interval);
        }

        /// <summary>
        ///     Takes the current period if it has lasted at least <paramref name="interval" />, ignoring
        ///     <see cref="Interval" />. For a pace that is recomputed every step — gravity that speeds up with the
        ///     level, an animation whose every frame declares its own delay.
        /// </summary>
        /// <param name="interval">How long this particular period should last.</param>
        /// <returns>True when the period was taken.</returns>
        public bool TryConsume(TimeSpan interval)
        {
            var elapsed = Elapsed;
            if (elapsed < interval)
                return false;

            LastElapsed = elapsed;

            // From now, not from when the period was due. See the class documentation - this is the whole opinion.
            _periodStart = _clock();
            return true;
        }

        /// <summary>
        ///     Starts the current period again from now, without taking it.
        ///     <para>
        ///         What this is for: an input that has just done the timer's job (a soft drop is gravity, arriving
        ///         early), and coming back from a modal dialog, where the form owes back however long the dialog was
        ///         up as one free step. The second case is common enough to be automatic — see
        ///         <see cref="Window.Form.Form{TData}.RestartOnActivate" />.
        ///     </para>
        /// </summary>
        public void Restart()
        {
            _periodStart = _clock();
        }
    }
}
