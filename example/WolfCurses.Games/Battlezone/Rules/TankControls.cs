// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     How a tank is driven from a keyboard that can only report one key at a time.
    ///     <para>
    ///         <b>A terminal cannot tell you that two keys are held down, and this is the game where that stops
    ///         being a curiosity.</b> The operating system repeats only the <i>most recently pressed</i> key, so
    ///         holding forward and then pressing left replaces the forward repeats entirely — the forward axis goes
    ///         quiet, is correctly inferred to have been released, and the tank stops dead the moment the player
    ///         tries to steer. Missile Command never noticed because a crosshair moving diagonally is a luxury;
    ///         here, driving and turning at the same time is the whole of tank combat.
    ///     </para>
    ///     <para>
    ///         So <b>the throttle is a gear, not a key that is held</b>: forward, stopped or reverse, changed one
    ///         notch at a time and staying where it was put. That is also what the cabinet did — it had two levers
    ///         you pushed and let go of, and the tank kept going. Only one key then has to be held at once, and it
    ///         is the steering, which is what a player is actually holding.
    ///     </para>
    ///     <para>
    ///         <b>Steering is quantised rather than timed</b>, which is the other half. An operating system waits
    ///         about half a second before it starts repeating a held key, so a steering axis built on
    ///         <see cref="HeldAxis" /> turns for its release window, stops, waits out the delay and then runs
    ///         smoothly — a lurch, a pause, and then motion, which is exactly the "clunky" a player reports without
    ///         being able to say what is wrong. Instead each press buys a fixed <see cref="TurnPerPress" /> of
    ///         turning, paid out smoothly over the frames that follow, so a tap is a precise nudge (which is what
    ///         aiming at four hundred units needs) and a held key is a stream of nudges that overlap into a
    ///         continuous turn. The debt is capped at barely more than one press, so letting go coasts by a few
    ///         degrees rather than sailing past the target.
    ///     </para>
    /// </summary>
    public sealed class TankControls
    {
        /// <summary>How far one press of a steering key is worth, in radians.</summary>
        public const double TurnPerPress = 0.10;

        /// <summary>
        ///     The most turning that can be owed at once, in radians.
        ///     <para>
        ///         This is the coast-after-release, so it is kept to barely more than a single press. Any larger and
        ///         a held turn builds up a debt that keeps being paid out after the key is let go, which overshoots
        ///         the target; any smaller and a held key cannot keep the debt topped up between repeats.
        ///     </para>
        /// </summary>
        public const double MaxTurnDebt = 0.13;

        private double _debt;
        private int _sign;

        /// <summary>Which way the tank is set to drive: -1 reverse, 0 stopped, 1 forward.</summary>
        public int Gear { get; private set; }

        /// <summary>How much turning is still owed, in radians. For tests and for nothing else.</summary>
        public double TurnDebt => _debt;

        /// <summary>
        ///     Moves the gear one notch, clamped: reverse, stopped, forward and no further either way.
        /// </summary>
        /// <param name="notches">Positive to speed up, negative to slow down.</param>
        public void Shift(int notches)
        {
            Gear = Math.Clamp(Gear + Math.Sign(notches), -1, 1);
        }

        /// <summary>Stops the tank without stepping through the gears.</summary>
        public void Halt()
        {
            Gear = 0;
        }

        /// <summary>
        ///     Says a steering key was pressed, buying <see cref="TurnPerPress" /> of turning in that direction.
        /// </summary>
        /// <param name="direction">-1 to swing left, 1 to swing right.</param>
        public void PressTurn(int direction)
        {
            if (direction == 0)
                return;

            // Reversing spends whatever was owed the other way rather than adding to it, so a correction takes
            // effect on the next frame instead of waiting out the turn it is correcting.
            if (Math.Sign(direction) != _sign)
                _debt = 0.0;

            _sign = Math.Sign(direction);
            _debt = Math.Min(_debt + TurnPerPress, MaxTurnDebt);
        }

        /// <summary>
        ///     Which way to turn this frame, spending the debt as it goes.
        ///     <para>
        ///         <b>This mutates</b>, which is why it is not a property — the same naming rule
        ///         <see cref="IntervalTimer.TryConsume" /> follows, and for the same reason: every caller also owns a
        ///         render method that runs a thousand times a second, and a property-shaped consumer is exactly what
        ///         ends up being called from one.
        ///     </para>
        /// </summary>
        /// <param name="elapsed">How long the frame lasted.</param>
        /// <param name="turnRate">How fast the tank turns, in radians a second.</param>
        /// <returns>-1, 0 or 1, ready to hand to <see cref="BattleField.Advance" />.</returns>
        public int TurnFor(TimeSpan elapsed, double turnRate)
        {
            if (_debt <= 0.0 || _sign == 0)
                return 0;

            _debt -= turnRate*Math.Max(elapsed.TotalSeconds, 0.0);

            if (_debt <= 0.0)
            {
                _debt = 0.0;
                var last = _sign;
                _sign = 0;
                return last;
            }

            return _sign;
        }

        /// <summary>Puts everything back to a standstill, for a fresh game.</summary>
        public void Reset()
        {
            Gear = 0;
            _debt = 0.0;
            _sign = 0;
        }
    }
}
