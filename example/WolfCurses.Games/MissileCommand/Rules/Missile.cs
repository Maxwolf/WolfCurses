// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     One thing in flight, travelling in a straight line from where it was launched to where it was aimed.
    ///     <para>
    ///         <b>Position is a fraction of the way along that line, not an accumulated pair of coordinates.</b> That
    ///         is the whole design of this class. Adding a velocity to an <c>X</c> and a <c>Y</c> every frame works,
    ///         and then "has it arrived?" becomes a question about floating-point distance that answers slightly
    ///         differently depending on the frame rate — so a missile overshoots its city by a pixel on a fast machine
    ///         and stops a pixel short on a slow one. Advancing a single <see cref="Progress" /> makes arrival exactly
    ///         <c>Progress &gt;= 1</c> on every machine, and drops the drift entirely because the endpoints are never
    ///         re-derived from the current position.
    ///     </para>
    /// </summary>
    public sealed class Missile
    {
        /// <summary>Initializes a new instance of the <see cref="Missile" /> class aimed from one point at another.</summary>
        /// <param name="kind">What sort of missile this is.</param>
        /// <param name="originX">Where it was launched from, in world units.</param>
        /// <param name="originY">Where it was launched from, in world units.</param>
        /// <param name="targetX">Where it is going, in world units.</param>
        /// <param name="targetY">Where it is going, in world units.</param>
        /// <param name="speed">How fast it travels, in world units per second.</param>
        /// <param name="silo">Which battery fired it, or -1 for anything the player did not launch.</param>
        public Missile(MissileKindEnum kind, double originX, double originY,
            double targetX, double targetY, double speed, int silo = -1)
        {
            Kind = kind;
            OriginX = originX;
            OriginY = originY;
            TargetX = targetX;
            TargetY = targetY;
            Speed = speed;
            Silo = silo;
            Alive = true;

            var dx = targetX - originX;
            var dy = targetY - originY;
            Length = Math.Sqrt(dx*dx + dy*dy);
        }

        /// <summary>What sort of missile this is.</summary>
        public MissileKindEnum Kind { get; }

        /// <summary>Where it was launched from. A split warhead is launched from wherever its parent let it go.</summary>
        public double OriginX { get; private set; }

        /// <summary>Where it was launched from.</summary>
        public double OriginY { get; private set; }

        /// <summary>Where it is aimed.</summary>
        public double TargetX { get; private set; }

        /// <summary>Where it is aimed.</summary>
        public double TargetY { get; private set; }

        /// <summary>How far it has to travel in total, in world units.</summary>
        public double Length { get; private set; }

        /// <summary>How fast it travels, in world units per second.</summary>
        public double Speed { get; }

        /// <summary>Which battery fired it, or -1 for anything the player did not launch.</summary>
        public int Silo { get; }

        /// <summary>How far along its flight it is, from 0 at the origin to 1 at the target.</summary>
        public double Progress { get; private set; }

        /// <summary>Where it was at the start of the current step, which is the tail of the segment it swept.</summary>
        public double PreviousProgress { get; private set; }

        /// <summary>False once it has been destroyed or has arrived; the field sweeps these out at the end of a step.</summary>
        public bool Alive { get; internal set; }

        /// <summary>Whether this warhead has already let go of its extra heads. Only ever true for a <see cref="MissileKindEnum.Mirv" />.</summary>
        public bool HasSplit { get; internal set; }

        /// <summary>How many times a <see cref="MissileKindEnum.SmartBomb" /> has swerved. Bounded, so it can be cornered.</summary>
        public int Dodges { get; internal set; }

        /// <summary>True once it has reached what it was aimed at.</summary>
        public bool HasArrived => Progress >= 1.0;

        /// <summary>Current horizontal position in world units.</summary>
        public double X => OriginX + (TargetX - OriginX)*Progress;

        /// <summary>Current altitude in world units.</summary>
        public double Y => OriginY + (TargetY - OriginY)*Progress;

        /// <summary>Horizontal position at the start of the current step.</summary>
        public double PreviousX => OriginX + (TargetX - OriginX)*PreviousProgress;

        /// <summary>Altitude at the start of the current step.</summary>
        public double PreviousY => OriginY + (TargetY - OriginY)*PreviousProgress;

        /// <summary>
        ///     Moves it along its line for one step of the given length, remembering where it started so the field can
        ///     test the segment it swept rather than only the point it landed on.
        /// </summary>
        /// <param name="seconds">How long the step lasted.</param>
        internal void Advance(double seconds)
        {
            PreviousProgress = Progress;

            // A zero-length flight would divide by zero, and it is reachable: a player can aim a counter-missile at
            // the silo firing it.
            Progress = Length <= double.Epsilon
                ? 1.0
                : Math.Min(1.0, Progress + Speed*seconds/Length);
        }

        /// <summary>
        ///     Points it somewhere else from wherever it currently is — how a smart bomb dodges.
        ///     <para>
        ///         The origin is moved to the current position and the progress reset, rather than the target simply
        ///         being changed. Leaving the origin where it was would mean the fraction already travelled now
        ///         refers to a different line, and the missile would jump backwards along the new one. Moving the
        ///         origin is also what makes the drawn trail bend at the point it dodged instead of snapping to a new
        ///         straight line from a launch point it left ten seconds ago.
        ///     </para>
        /// </summary>
        /// <param name="targetX">The new aim point.</param>
        /// <param name="targetY">The new aim point.</param>
        internal void Redirect(double targetX, double targetY)
        {
            OriginX = X;
            OriginY = Y;
            TargetX = targetX;
            TargetY = targetY;

            var dx = targetX - OriginX;
            var dy = targetY - OriginY;
            Length = Math.Sqrt(dx*dx + dy*dy);

            Progress = 0.0;
            PreviousProgress = 0.0;
        }

        /// <summary>
        ///     A freshly-split warhead, launched from wherever its parent has got to rather than from the sky, which
        ///     is what makes the drawn trail fork at the split point.
        /// </summary>
        /// <param name="parent">The warhead this one came out of.</param>
        /// <param name="targetX">Where the new head is aimed.</param>
        /// <param name="targetY">Where the new head is aimed.</param>
        /// <returns>A missile starting where the parent currently is.</returns>
        internal static Missile SplitFrom(Missile parent, double targetX, double targetY)
        {
            return new Missile(MissileKindEnum.Icbm, parent.X, parent.Y, targetX, targetY, parent.Speed);
        }
    }
}
