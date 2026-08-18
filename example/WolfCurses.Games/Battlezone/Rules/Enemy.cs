// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     Something hostile on the plain, and the only thinking this game does.
    ///     <para>
    ///         <b>The tactic the whole game is built on is one number: <see cref="TurnRate" />, which is slower than
    ///         the player's.</b> An enemy tank drives at you and fires when it is lined up, so head-on it wins the
    ///         exchange — but it cannot follow a target that keeps moving sideways, so the answer is to circle, and
    ///         a player works that out in about ten seconds without being told. Give the enemy the player's turn
    ///         rate and every one of those decisions disappears: the game still compiles, still shoots, still keeps
    ///         score, and is no longer about anything. It is the same shape of finding as Missile Command's middle
    ///         battery being faster than the outer two, and it is the reason this file has almost no code in it.
    ///     </para>
    ///     <para>
    ///         There is no path-finding here either, for the same reason there is none in Pac-Man: aim at the
    ///         player, and if a block is in the way lean around it. What that produces — a tank that noses out from
    ///         behind a pyramid, loses its line, and swings wide to try again — is a consequence of the scenery
    ///         rather than of anything clever.
    ///     </para>
    /// </summary>
    public sealed class Enemy
    {
        /// <summary>
        ///     How far ahead a tank looks for something to steer around.
        ///     <para>
        ///         <b>Deliberately shorter than the gap between two blocks, and that is load-bearing.</b> Look
        ///         further and a tank starts leaning away from scenery it is nowhere near; the lean is applied to
        ///         the bearing to the player, which rotates as the tank moves, so a constant offset held for a long
        ///         time is not a detour but a <i>circle</i>. See <see cref="Think" /> for the fix, which is to lean
        ///         by the angle that just clears the block rather than by a constant — after which the look-ahead
        ///         distance stops being delicate and is only about how early a tank starts to turn.
        ///     </para>
        /// </summary>
        private const double LookAhead = 40.0;

        /// <summary>How near it tries to get before it stops closing and just shoots.</summary>
        private const double PreferredRange = 95.0;

        /// <summary>Initializes a new instance of the <see cref="Enemy" /> class.</summary>
        /// <param name="kind">What it is.</param>
        /// <param name="x">Where it starts, east.</param>
        /// <param name="z">Where it starts, north.</param>
        /// <param name="heading">Which way it faces, in radians clockwise from north.</param>
        public Enemy(EnemyKindEnum kind, double x, double z, double heading)
        {
            Kind = kind;
            X = x;
            Z = z;
            Heading = heading;
            Alive = true;
            Reload = kind == EnemyKindEnum.SuperTank ? 1.9 : 2.6;
        }

        /// <summary>What it is.</summary>
        public EnemyKindEnum Kind { get; }

        /// <summary>Where it is, east.</summary>
        public double X { get; internal set; }

        /// <summary>Where it is, north.</summary>
        public double Z { get; internal set; }

        /// <summary>Which way it faces, in radians clockwise from north.</summary>
        public double Heading { get; internal set; }

        /// <summary>Whether it is still on the plain.</summary>
        public bool Alive { get; internal set; }

        /// <summary>How long until it can fire again.</summary>
        public double Reload { get; internal set; }

        /// <summary>How wide it is, for being shot at and for bumping into things.</summary>
        public double Radius => Kind == EnemyKindEnum.Saucer ? 7.0 : 6.5;

        /// <summary>How far off the ground it floats. Only the saucer does.</summary>
        public double Altitude => Kind == EnemyKindEnum.Saucer ? 20.0 : 0.0;

        /// <summary>What killing it is worth.</summary>
        public int Value
        {
            get
            {
                return Kind switch
                {
                    EnemyKindEnum.SuperTank => 3000,
                    EnemyKindEnum.Saucer => 5000,
                    _ => 1000
                };
            }
        }

        /// <summary>How fast it drives, in units a second.</summary>
        public double Speed
        {
            get
            {
                return Kind switch
                {
                    EnemyKindEnum.SuperTank => 34.0,
                    EnemyKindEnum.Saucer => 26.0,
                    _ => 22.0
                };
            }
        }

        /// <summary>
        ///     How fast it can turn, in radians a second — and deliberately less than
        ///     <see cref="BattleField.PlayerTurnRate" />. See the remarks on this class: this single number is the
        ///     game.
        /// </summary>
        public double TurnRate => Kind == EnemyKindEnum.SuperTank ? 0.60 : 0.40;

        /// <summary>How nearly lined up it has to be before it will fire, in radians either side.</summary>
        public double FireCone => Kind == EnemyKindEnum.SuperTank ? 0.12 : 0.09;

        /// <summary>
        ///     How far off its aim a shot can come out, in radians either side.
        ///     <para>
        ///         <b>Without this an enemy never misses, and <see cref="FireCone" /> does not save you.</b> The cone
        ///         is only a gate on when it is allowed to shoot; a tank turns toward the player every frame, so by
        ///         the time the reload runs out it is aimed dead on and every single shell is a bullseye. Measured,
        ///         that made a stationary player die to exactly three shots at a hundred and seventy units, which is
        ///         not the game — the cabinet's tanks miss, and missing is what gives the player time to line one up.
        ///     </para>
        ///     <para>
        ///         Because the error is angular, it grows into a bigger miss the further away the shot is taken
        ///         from. That is the whole difficulty curve and it needed nothing else: enemies are frightening up
        ///         close and survivable at range, so closing the distance is a real decision in both directions.
        ///     </para>
        /// </summary>
        public double FireSpread => Kind == EnemyKindEnum.SuperTank ? 0.050 : 0.075;

        /// <summary>Whether it shoots at all.</summary>
        public bool IsHostile => Kind != EnemyKindEnum.Saucer;

        /// <summary>
        ///     Drives, aims and decides whether to shoot. Everything it knows it asks the field for.
        /// </summary>
        /// <param name="field">The world it is standing in.</param>
        /// <param name="seconds">How long the frame lasted.</param>
        public void Think(BattleField field, double seconds)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            Reload -= seconds;

            if (Kind == EnemyKindEnum.Saucer)
            {
                // It has no opinion about anything. It flies the way it was pointed and leaves.
                X += Math.Sin(Heading)*Speed*seconds;
                Z += Math.Cos(Heading)*Speed*seconds;
                return;
            }

            var dx = field.PlayerX - X;
            var dz = field.PlayerZ - Z;
            var range = Math.Sqrt(dx*dx + dz*dz);
            var wanted = Math.Atan2(dx, dz);

            var sees = field.HasLineOfSight(X, Z, field.PlayerX, field.PlayerZ);

            // Whether it has any reason to drive at all: too far away, or near enough but with something in the way.
            // A tank that has arrived at its favourite range with a clear view has nothing left to do but shoot.
            var closing = range > PreferredRange || !sees;

            // Lean around whatever is in the way, by exactly enough to clear its edge and no more. Deliberately a
            // lean and not a route: it steers to one side of the block rather than plotting a way past it, so it can
            // be wrong, be stuck for a moment and try again, which is what makes an enemy look like it is driving
            // rather than following a path.
            //
            // THE ANGLE HAS TO SHRINK WITH DISTANCE, and a fixed one is the bug. A lean is applied to the bearing to
            // the player, and a bearing that is being steered away from by a constant amount does not trace a detour
            // — it traces a CIRCLE. Measured with a flat 0.85 radians, four seeds in twenty-four had their tank
            // orbiting a block for ever, never closing, never firing, while a player who stood perfectly still went
            // three minutes without being shot at. Steering to the tangent instead is self-limiting: the further off
            // the block, the smaller the correction, and once it is behind there is no correction at all.
            // Only while it is actually going somewhere. A lean is a STEERING correction, and a tank that has
            // stopped is not steering — leaving it on means a stationary tank sits at its range aiming politely past
            // the player at a block it is never going to hit, for ever. That cost one seed in twenty-four too.
            if (closing && field.TryFindBlocker(X, Z, wanted, Math.Min(range, LookAhead), Radius, out var blocker))
            {
                var away = Math.Sqrt((blocker.X - X)*(blocker.X - X) + (blocker.Z - Z)*(blocker.Z - Z));
                var clearance = blocker.Radius + Radius + 3.0;
                var span = Math.Asin(Math.Clamp(clearance/Math.Max(away, clearance), -1.0, 1.0));

                var toBlocker = Math.Atan2(blocker.X - X, blocker.Z - Z);
                wanted += BattleField.WrapAngle(wanted - toBlocker) >= 0 ? span : -span;
            }

            var error = BattleField.WrapAngle(wanted - Heading);
            var turn = Math.Min(Math.Abs(error), TurnRate*seconds);
            Heading = BattleField.WrapAngle(Heading + Math.Sign(error)*turn);

            // WHERE THE PLAYER IS, not where the tank is steering. These are the same number until the tank leans
            // around a block, and then they are not - and firing on the steering error means a tank that is dodging
            // scenery empties its magazine into the scenery. Measured, one seed in twenty-four spent three minutes
            // shooting seventy-four shells at a pyramid.
            var aimError = BattleField.WrapAngle(Math.Atan2(dx, dz) - Heading);

            if (closing && Math.Abs(error) < 1.2)
            {
                var step = Speed*seconds;
                var nx = X + Math.Sin(Heading)*step;
                var nz = Z + Math.Cos(Heading)*step;

                if (!field.IsBlocked(nx, nz, Radius))
                {
                    X = nx;
                    Z = nz;
                }
            }

            if (Reload <= 0.0 && Math.Abs(aimError) < FireCone && range < 340.0 && sees)
            {
                field.EnemyFire(this);
                Reload = Kind == EnemyKindEnum.SuperTank ? 1.9 : 2.6;
            }
        }
    }
}
