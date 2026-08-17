// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     An expanding ball of fire. It grows, holds and shrinks away, and anything that flies into it while it is
    ///     there is destroyed — including the warheads that then leave blasts of their own, which is the whole game.
    ///     <para>
    ///         <b><see cref="Radius" /> is worked out from <see cref="Age" /> every time it is asked for, and is
    ///         never accumulated.</b> Growing it by <c>radius += rate * delta</c> looks equivalent and is not: the
    ///         error compounds with the frame rate, so the same blast covers measurably different ground on a fast
    ///         machine than on a slow one, and on the shrinking half it sails past zero into negative radii that
    ///         collide with nothing while still being drawn. Deriving it means every blast of the same age is the
    ///         same size on every machine, forever.
    ///     </para>
    /// </summary>
    public sealed class Blast
    {
        /// <summary>How long a blast lasts, start to finish.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2.1);

        /// <summary>How big it gets at its widest, in world units.</summary>
        public const double MaxRadius = 0.075;

        /// <summary>
        ///     The fraction of its life spent growing. The rest is spent shrinking, and the two are deliberately not
        ///     equal — a blast that blooms faster than it fades is what makes firing ahead of a missile feel like
        ///     timing rather than like luck.
        /// </summary>
        private const double GrowFraction = 0.42;

        /// <summary>Initializes a new instance of the <see cref="Blast" /> class at a point.</summary>
        /// <param name="x">Where it went off, in world units.</param>
        /// <param name="y">Where it went off, in world units.</param>
        /// <param name="fromPlayer">True when the player's own counter-missile made it, which only scoring cares about.</param>
        public Blast(double x, double y, bool fromPlayer)
        {
            X = x;
            Y = y;
            FromPlayer = fromPlayer;
        }

        /// <summary>Where it went off, in world units.</summary>
        public double X { get; }

        /// <summary>Where it went off, in world units.</summary>
        public double Y { get; }

        /// <summary>True when the player's own counter-missile made it, rather than a warhead dying in one.</summary>
        public bool FromPlayer { get; }

        /// <summary>How long it has been burning.</summary>
        public double Age { get; private set; }

        /// <summary>How far through its life it is, 0 to 1 — what a colour ramp and a fading alpha are sampled on.</summary>
        public double Age01 => Math.Min(1.0, Age/Lifetime.TotalSeconds);

        /// <summary>False once it has burned out.</summary>
        public bool Alive => Age < Lifetime.TotalSeconds;

        /// <summary>How big it is right now, in world units. Never negative, and zero at both ends of its life.</summary>
        public double Radius
        {
            get
            {
                var t = Age01;
                var scale = t < GrowFraction
                    ? t/GrowFraction
                    : 1.0 - (t - GrowFraction)/(1.0 - GrowFraction);

                return MaxRadius*Math.Max(0.0, scale);
            }
        }

        /// <summary>Burns for one step.</summary>
        /// <param name="seconds">How long the step lasted.</param>
        internal void Advance(double seconds)
        {
            Age += seconds;
        }

        /// <summary>
        ///     Whether the straight path something swept between two points passes within this blast — the segment,
        ///     not just where it ended up.
        ///     <para>
        ///         Testing only the endpoint is the version of this that ships and then produces bug reports about
        ///         missiles "going through" explosions. A missile crosses a blast in a fraction of a second; one
        ///         slow frame — a garbage collection, an alt-tab, a debugger breakpoint — and the endpoint before is
        ///         above the blast and the endpoint after is below it, with nothing in between ever tested. The
        ///         failure is asymmetric and always in the same direction: the blast is what protects and the
        ///         missile is what kills, so tunnelling only ever costs the player a city.
        ///     </para>
        /// </summary>
        /// <param name="x0">Where the swept segment starts.</param>
        /// <param name="y0">Where the swept segment starts.</param>
        /// <param name="x1">Where the swept segment ends.</param>
        /// <param name="y1">Where the swept segment ends.</param>
        /// <param name="padding">Extra reach, for something with a body rather than a point.</param>
        /// <returns>True when any part of that segment is inside the fireball.</returns>
        public bool Catches(double x0, double y0, double x1, double y1, double padding = 0.0)
        {
            var reach = Radius + padding;
            if (reach <= 0.0)
                return false;

            var dx = x1 - x0;
            var dy = y1 - y0;
            var lengthSquared = dx*dx + dy*dy;

            // Where along the segment the closest approach happens, clamped to the segment itself so a blast behind
            // the missile is not counted as being in front of it.
            var t = lengthSquared <= double.Epsilon
                ? 0.0
                : Math.Clamp(((X - x0)*dx + (Y - y0)*dy)/lengthSquared, 0.0, 1.0);

            var nearestX = x0 + dx*t;
            var nearestY = y0 + dy*t;
            var offX = X - nearestX;
            var offY = Y - nearestY;

            return offX*offX + offY*offY <= reach*reach;
        }
    }
}
