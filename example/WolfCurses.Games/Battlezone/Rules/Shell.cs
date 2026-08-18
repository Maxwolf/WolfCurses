// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     A shell in flight, travelling in a straight line at a constant speed until it hits something or runs out
    ///     of range.
    ///     <para>
    ///         <b>It remembers where it was a moment ago, and that is the whole reason this is a class rather than a
    ///         pair of numbers.</b> A shell crosses about five units in a frame and a tank is six across, so at
    ///         thirty frames a second a hit is a near thing and at twenty it is a miss — the shell is simply on one
    ///         side of the target in one frame and the other side in the next, having never been <i>on</i> it. So
    ///         nothing here asks whether the shell is inside anything; everything asks whether the <i>segment it
    ///         just travelled</i> passed through. Missile Command learned this about its fireballs and the lesson
    ///         did not stay learned, which is why it is written down twice.
    ///     </para>
    /// </summary>
    public sealed class Shell
    {
        /// <summary>Initializes a new instance of the <see cref="Shell" /> class.</summary>
        /// <param name="x">Where it starts, east.</param>
        /// <param name="z">Where it starts, north.</param>
        /// <param name="heading">Which way it flies, in radians clockwise from north.</param>
        /// <param name="fromPlayer">True when the player fired it, which decides what it can hurt.</param>
        public Shell(double x, double z, double heading, bool fromPlayer)
        {
            X = x;
            Z = z;
            FromX = x;
            FromZ = z;
            Heading = heading;
            FromPlayer = fromPlayer;
            Alive = true;
        }

        /// <summary>Where it is now, east.</summary>
        public double X { get; private set; }

        /// <summary>Where it is now, north.</summary>
        public double Z { get; private set; }

        /// <summary>Where it was at the start of this frame, east.</summary>
        public double FromX { get; private set; }

        /// <summary>Where it was at the start of this frame, north.</summary>
        public double FromZ { get; private set; }

        /// <summary>Which way it flies, in radians clockwise from north.</summary>
        public double Heading { get; }

        /// <summary>Whether the player fired it.</summary>
        public bool FromPlayer { get; }

        /// <summary>How far it has flown altogether.</summary>
        public double Travelled { get; private set; }

        /// <summary>Whether it is still in the air.</summary>
        public bool Alive { get; internal set; }

        /// <summary>Moves it, leaving the segment it crossed readable through <see cref="FromX" />.</summary>
        /// <param name="seconds">How long the frame lasted.</param>
        /// <param name="speed">How fast it flies, in units a second.</param>
        public void Advance(double seconds, double speed)
        {
            FromX = X;
            FromZ = Z;

            var step = speed*seconds;
            X += Math.Sin(Heading)*step;
            Z += Math.Cos(Heading)*step;
            Travelled += step;
        }
    }
}
