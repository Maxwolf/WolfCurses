// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Diagnostics;

namespace WolfCurses
{
    /// <summary>
    ///     One direction the user is holding down — left/right, forward/back — recovered from a stream of key
    ///     presses, because a terminal never says when a key was let go.
    ///     <para>
    ///         <b>This exists because the obvious code is wrong in a way that only shows up on somebody else's
    ///         machine.</b> <see cref="Core.InputManager" /> drains the console's key buffer with a <c>while</c> loop
    ///         and dispatches everything it found inside a single tick, which is correct and is what stops a held
    ///         arrow feeling like a hockey puck. The consequence is that a key being <i>held</i> arrives as a burst
    ///         of eight or ten presses with no time at all between them. For a game on a grid that is exactly right
    ///         — one press is one step — but for anything moving continuously, "advance a bit per press" makes
    ///         speed a function of the player's key-repeat setting and of how far behind the tick loop has fallen,
    ///         so the harder the machine works the faster things fly. The fix is always the same: record a
    ///         <i>direction</i> and <i>when it was last asserted</i>, integrate real elapsed time against it, and
    ///         treat silence longer than <see cref="ReleaseAfter" /> as the key-up event that is never coming.
    ///     </para>
    ///     <para>
    ///         <b><see cref="HeldFor" /> is the part that gets written wrong</b>, and this type exists as much for
    ///         that as for the release inference. A caller wanting to ramp a speed up while a key is held needs to
    ///         know when the axis last started moving <i>from a standstill</i>, and the natural way to write it —
    ///         set the stamp when nothing is being held — is checked after the direction has already been assigned,
    ///         by which point something always is. The condition is then never true, the stamp stays at zero
    ///         forever, and the ramp silently pins itself to full speed a fraction of a second into the program and
    ///         stays there. That is not hypothetical: it is precisely the bug this type was extracted from.
    ///     </para>
    ///     <para>
    ///         Nothing here reads the console or knows what a key is: a caller pushes -1, 0 or +1 from wherever it
    ///         likes, so the same type serves a keyboard, a held mouse button or a replayed script. It keeps its own
    ///         clock, started at construction and never reset — which is what makes a modal dialog behave properly,
    ///         since the axis comes back from it having heard nothing for however long the dialog was up and so
    ///         reads as released rather than lurching off in whatever direction was last held.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     private readonly HeldAxis _turn = new();
    ///
    ///     public override void OnKeyPressed(ConsoleKey key)
    ///     {
    ///         if (key == ConsoleKey.LeftArrow) _turn.Press(-1);
    ///         else if (key == ConsoleKey.RightArrow) _turn.Press(1);
    ///     }
    ///
    ///     public override void OnTick(bool systemTick, bool skipDay)
    ///     {
    ///         if (!_frame.TryConsume()) return;
    ///         _heading += _turn.Direction * TurnRate * _frame.LastElapsed.TotalSeconds;
    ///     }
    ///     </code>
    /// </example>
    public sealed class HeldAxis
    {
        /// <summary>
        ///     How long an axis may go unasserted before it is treated as released.
        ///     <para>
        ///         Comfortably longer than a key-repeat interval — operating systems repeat at roughly thirty a
        ///         second once the initial delay has passed — and comfortably shorter than a human tapping a key on
        ///         purpose. Too short and a held key stutters as the repeats arrive; too long and the axis coasts
        ///         after release, which reads as sticky controls.
        ///     </para>
        /// </summary>
        public static readonly TimeSpan DefaultReleaseAfter = TimeSpan.FromMilliseconds(180);

        private readonly Func<TimeSpan> _clock;

        /// <summary>Which way was last asked for, before the release inference is applied to it.</summary>
        private int _direction;

        /// <summary>When that direction was last asserted.</summary>
        private TimeSpan _pressedAt;

        /// <summary>When the axis last started moving after being at rest.</summary>
        private TimeSpan _movingSince;

        /// <summary>Initializes a new instance of the <see cref="HeldAxis" /> class with the default release delay.</summary>
        public HeldAxis() : this(DefaultReleaseAfter, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HeldAxis" /> class.</summary>
        /// <param name="releaseAfter">How long silence must last before the axis reads as released.</param>
        public HeldAxis(TimeSpan releaseAfter) : this(releaseAfter, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HeldAxis" /> class against a supplied clock.</summary>
        /// <param name="releaseAfter">How long silence must last before the axis reads as released.</param>
        /// <param name="clock">Where time comes from; null starts a stopwatch of its own.</param>
        internal HeldAxis(TimeSpan releaseAfter, Func<TimeSpan> clock)
        {
            if (releaseAfter <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(releaseAfter),
                    "An axis that is released immediately can never be held.");

            if (clock == null)
            {
                var stopwatch = Stopwatch.StartNew();
                clock = () => stopwatch.Elapsed;
            }

            _clock = clock;
            ReleaseAfter = releaseAfter;
        }

        /// <summary>How long silence lasts before the axis reads as released.</summary>
        public TimeSpan ReleaseAfter { get; }

        /// <summary>
        ///     Which way the axis is being held: -1, 0 or +1. Zero once nothing has been asserted for longer than
        ///     <see cref="ReleaseAfter" />.
        ///     <para>
        ///         <b>Reading this changes nothing</b>, so it is safe in a render path that runs a thousand times a
        ///         second — the same rule <see cref="IntervalTimer.IsDue" /> follows, and for the same reason.
        ///     </para>
        /// </summary>
        public int Direction
        {
            get
            {
                if (_direction == 0)
                    return 0;

                return _clock() - _pressedAt > ReleaseAfter ? 0 : _direction;
            }
        }

        /// <summary>Whether the axis is being held at all.</summary>
        public bool IsHeld => Direction != 0;

        /// <summary>
        ///     How long the axis has been moving, measured from the last time it started from a standstill —
        ///     <see cref="TimeSpan.Zero" /> when it is not held.
        ///     <para>
        ///         What a speed ramp needs. Reversing direction does <i>not</i> restart it: the axis never came to
        ///         rest, so a player sweeping left and then right keeps the speed they had built up rather than
        ///         being dropped back to a crawl in the middle of a movement.
        ///     </para>
        /// </summary>
        public TimeSpan HeldFor => Direction == 0 ? TimeSpan.Zero : _clock() - _movingSince;

        /// <summary>
        ///     Says the axis is being held a given way, right now.
        /// </summary>
        /// <param name="direction">
        ///     Which way: anything negative is -1, anything positive is +1, and zero is the same as
        ///     <see cref="Release" />.
        /// </param>
        public void Press(int direction)
        {
            if (direction == 0)
            {
                Release();
                return;
            }

            var now = _clock();

            // Asked BEFORE the direction is assigned, and of the time-aware reading rather than the raw field. Both
            // halves matter: assigning first makes the test never true, and testing the raw field makes an axis that
            // was silently released ten seconds ago look like it never stopped.
            if (Direction == 0)
                _movingSince = now;

            _direction = Math.Sign(direction);
            _pressedAt = now;
        }

        /// <summary>
        ///     Lets go of the axis at once, without waiting out <see cref="ReleaseAfter" />.
        ///     <para>
        ///         For handing control to something else — a mouse click that takes over aiming, a form being reset
        ///         for a new game — where waiting for the inference would leave the old direction running.
        ///     </para>
        /// </summary>
        public void Release()
        {
            _direction = 0;
            _movingSince = TimeSpan.Zero;
        }
    }
}
