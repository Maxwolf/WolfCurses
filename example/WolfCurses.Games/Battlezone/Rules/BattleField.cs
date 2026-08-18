// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     The plain, everything standing on it, and the tank the player is sitting in.
    ///     <para>
    ///         <b>The world is two-dimensional. Only the picture is three-dimensional.</b> This is the finding worth
    ///         keeping out of the whole game: a Battlezone tank drives on a flat plain and nothing ever leaves it,
    ///         so every position here is an east and a north and every facing is one angle. There is no vector type
    ///         in this file, no matrix, and no height on anything that moves — the third dimension is added by
    ///         <see cref="WireCamera" /> at the moment of drawing and exists nowhere else. That is what keeps
    ///         collision down to comparing two distances, keeps the rules testable with no console anywhere near
    ///         them, and means the hard part of a first-person game turns out to be about a hundred lines of
    ///         arithmetic in one class that draws.
    ///     </para>
    ///     <para>
    ///         <b>The plain is endless because the scenery is recycled behind the player</b>, not because it is
    ///         large. An obstacle further away than anything can be drawn is picked up and put down again somewhere
    ///         ahead, which is invisible <i>only</i> because <see cref="RecycleRange" /> is comfortably beyond
    ///         <see cref="DrawRange" /> — bring those two together and blocks start appearing out of nothing in
    ///         plain sight. The alternative, a boundary, means an invisible wall in the middle of an open desert.
    ///     </para>
    /// </summary>
    public sealed class BattleField
    {
        /// <summary>
        ///     How fast the player turns, in radians a second. Every enemy turns slower than this on purpose — see
        ///     <see cref="Enemy.TurnRate" />, which is where the game actually lives.
        /// </summary>
        public const double PlayerTurnRate = 0.90;

        /// <summary>How fast the player drives forward, in units a second.</summary>
        public const double PlayerSpeed = 30.0;

        /// <summary>How fast the player reverses. Slower, so backing out of trouble is a decision rather than a reflex.</summary>
        public const double PlayerReverse = 18.0;

        /// <summary>How wide the player's tank is.</summary>
        public const double PlayerRadius = 5.5;

        /// <summary>How fast a shell flies, in units a second.</summary>
        public const double ShellSpeed = 155.0;

        /// <summary>How far a shell gets before it falls short.</summary>
        public const double ShellRange = 420.0;

        /// <summary>How far anything can be seen. Past this the plain is empty and there is only the horizon.</summary>
        public const double DrawRange = 420.0;

        /// <summary>How far away a piece of scenery has to be before it is quietly moved somewhere useful.</summary>
        public const double RecycleRange = 620.0;

        /// <summary>How long the screen stays broken before the next tank is issued.</summary>
        public const double CrackSeconds = 1.7;

        /// <summary>How many tanks the player gets.</summary>
        public const int StartingLives = 3;

        /// <summary>Every this many points is another tank.</summary>
        public const int BonusLifeEvery = 15000;

        /// <summary>How many blocks and pyramids are on the plain at once.</summary>
        public const int ObstacleCount = 16;

        /// <summary>How fast the radar sweep goes round, in radians a second.</summary>
        private const double RadarSweepRate = 2.4;

        private readonly Randomizer _random;
        private readonly List<Obstacle> _obstacles = new();
        private readonly List<Enemy> _enemies = new();
        private readonly List<Shell> _shells = new();
        private readonly List<Explosion> _explosions = new();

        private double _saucerDue;
        private double _crackFor;
        private int _explosionSeed;
        private int _nextBonusAt = BonusLifeEvery;

        /// <summary>Initializes a new instance of the <see cref="BattleField" /> class.</summary>
        /// <param name="random">Where the scenery and the reinforcements come from.</param>
        public BattleField(Randomizer random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));

            Lives = StartingLives;
            Message = "Find it before it finds you.";

            for (var i = 0; i < ObstacleCount; i++)
                _obstacles.Add(new Obstacle(0, 0, ObstacleKindEnum.Cube));

            foreach (var obstacle in _obstacles)
                Scatter(obstacle, 60.0, 380.0);

            _saucerDue = 22.0 + _random.NextDouble()*18.0;
            SpawnHostile();
        }

        /// <summary>Where the player is, east.</summary>
        public double PlayerX { get; private set; }

        /// <summary>Where the player is, north.</summary>
        public double PlayerZ { get; private set; }

        /// <summary>Which way the player faces, in radians clockwise from north.</summary>
        public double PlayerHeading { get; private set; }

        /// <summary>How many tanks are left, this one included.</summary>
        public int Lives { get; private set; }

        /// <summary>What the player has scored.</summary>
        public int Score { get; private set; }

        /// <summary>How many enemies the player has destroyed.</summary>
        public int Kills { get; private set; }

        /// <summary>Whether the last tank has been lost.</summary>
        public bool IsOver { get; private set; }

        /// <summary>
        ///     Whether the viewport is broken — which is what being hit looks like, and the only feedback the arcade
        ///     cabinet ever gave.
        /// </summary>
        public bool IsCracked { get; private set; }

        /// <summary>Which way the shot came from, relative to the player's facing, so the break can start there.</summary>
        public double CrackBearing { get; private set; }

        /// <summary>Where the radar sweep has got to, in radians clockwise from the player's nose.</summary>
        public double RadarSweep { get; private set; }

        /// <summary>How long the game has been running, in seconds.</summary>
        public double Elapsed { get; private set; }

        /// <summary>What to tell the player.</summary>
        public string Message { get; private set; }

        /// <summary>The scenery.</summary>
        public IReadOnlyList<Obstacle> Obstacles => _obstacles;

        /// <summary>What is out there.</summary>
        public IReadOnlyList<Enemy> Enemies => _enemies;

        /// <summary>What is in the air.</summary>
        public IReadOnlyList<Shell> Shells => _shells;

        /// <summary>What is coming apart.</summary>
        public IReadOnlyList<Explosion> Explosions => _explosions;

        /// <summary>Whether the player has a shell of their own in flight.</summary>
        public bool PlayerShellInFlight
        {
            get
            {
                foreach (var shell in _shells)
                {
                    if (shell.FromPlayer && shell.Alive)
                        return true;
                }

                return false;
            }
        }

        /// <summary>Whether the player is between tanks, watching a broken screen.</summary>
        public bool IsRespawning { get; private set; }

        /// <summary>
        ///     Smallest signed angle equal to the one given: the way round that is actually shorter.
        ///     <para>
        ///         Everything that turns goes through here. Without it a tank facing just west of north and asked to
        ///         face just east of north turns the <i>long</i> way, all the way round the compass, which looks
        ///         exactly like the steering being broken rather than like arithmetic.
        ///     </para>
        /// </summary>
        /// <param name="radians">Any angle.</param>
        /// <returns>The same angle, between -π and π.</returns>
        public static double WrapAngle(double radians)
        {
            var wrapped = (radians + Math.PI)%(2.0*Math.PI);
            if (wrapped < 0.0)
                wrapped += 2.0*Math.PI;

            return wrapped - Math.PI;
        }

        /// <summary>
        ///     Advances everything by however long really passed.
        /// </summary>
        /// <param name="elapsed">How long the frame lasted.</param>
        /// <param name="turn">-1 to swing left, 1 to swing right, 0 to hold.</param>
        /// <param name="throttle">1 forward, -1 back, 0 to stand.</param>
        public void Advance(TimeSpan elapsed, int turn, int throttle)
        {
            // Clamped, for the same reason Missile Command clamps its own: one long frame - a breakpoint, a garbage
            // collection, a window being dragged - must not teleport a shell through a tank. Better to run slow for
            // a frame than to run wrong.
            var seconds = Math.Min(Math.Max(elapsed.TotalSeconds, 0.0), 0.1);
            if (seconds <= 0.0)
                return;

            Elapsed += seconds;
            RadarSweep = (RadarSweep + RadarSweepRate*seconds)%(2.0*Math.PI);

            AdvanceExplosions(seconds);

            // A broken screen is the end of the story, so nothing moves behind it. This is deliberately unlike
            // Missile Command, where the field keeps advancing after the last city falls so the warheads already in
            // the air finish their arcs - there the last frame is the best one, here the last frame is the point.
            if (IsOver)
                return;

            // Between tanks nothing moves either: the player is looking at a broken screen, and letting the enemy
            // drive on behind it means being shot the instant the glass clears.
            if (IsRespawning)
            {
                Respawn(seconds);
                return;
            }

            MovePlayer(seconds, turn, throttle);

            foreach (var enemy in _enemies)
            {
                if (enemy.Alive)
                    enemy.Think(this, seconds);
            }

            AdvanceShells(seconds);
            Recycle();
            Reinforce(seconds);
        }

        /// <summary>
        ///     Fires, if the player has a shell to fire with.
        ///     <para>
        ///         <b>One at a time, which is the arcade's rule and worth keeping.</b> It turns every shot into a
        ///         decision — miss, and the tank bearing down on you is unopposed for the three seconds the shell
        ///         takes to reach the horizon and expire. Allowing a stream of them makes the game about holding a
        ///         key down.
        ///     </para>
        /// </summary>
        /// <returns>True when a shell actually left the barrel.</returns>
        public bool Fire()
        {
            if (IsOver || IsRespawning || PlayerShellInFlight)
                return false;

            _shells.Add(new Shell(PlayerX + Math.Sin(PlayerHeading)*7.0, PlayerZ + Math.Cos(PlayerHeading)*7.0,
                PlayerHeading, true));

            return true;
        }

        /// <summary>Puts an enemy shell in the air. Called by the enemy that decided to fire.</summary>
        /// <param name="enemy">Who fired.</param>
        public void EnemyFire(Enemy enemy)
        {
            if (enemy == null)
                throw new ArgumentNullException(nameof(enemy));

            // Gunnery, not aim: the shot comes out somewhere inside the tank's error rather than exactly where it
            // was pointing. See Enemy.FireSpread - an enemy without this never misses at any range.
            var aim = enemy.Heading + (_random.NextDouble() - 0.5)*2.0*enemy.FireSpread;

            _shells.Add(new Shell(enemy.X + Math.Sin(aim)*8.0, enemy.Z + Math.Cos(aim)*8.0, aim, false));
        }

        /// <summary>Whether a circle at a position would be standing in something.</summary>
        /// <param name="x">Where, east.</param>
        /// <param name="z">Where, north.</param>
        /// <param name="radius">How wide the thing is.</param>
        /// <returns>True when it would not fit.</returns>
        public bool IsBlocked(double x, double z, double radius)
        {
            foreach (var obstacle in _obstacles)
            {
                var dx = obstacle.X - x;
                var dz = obstacle.Z - z;
                var reach = obstacle.Radius + radius;
                if (dx*dx + dz*dz < reach*reach)
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Whether one point can see another, which is to say whether a shell fired between them would arrive.
        /// </summary>
        /// <param name="fromX">Looking from, east.</param>
        /// <param name="fromZ">Looking from, north.</param>
        /// <param name="toX">Looking at, east.</param>
        /// <param name="toZ">Looking at, north.</param>
        /// <returns>True when nothing is in the way.</returns>
        public bool HasLineOfSight(double fromX, double fromZ, double toX, double toZ)
        {
            foreach (var obstacle in _obstacles)
            {
                if (DistanceToSegment(obstacle.X, obstacle.Z, fromX, fromZ, toX, toZ) < obstacle.Radius)
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Finds the nearest piece of scenery a tank is about to drive into, if any.
        /// </summary>
        /// <param name="x">Where it is, east.</param>
        /// <param name="z">Where it is, north.</param>
        /// <param name="heading">Which way it wants to go.</param>
        /// <param name="distance">How far ahead to bother looking.</param>
        /// <param name="radius">How wide the tank is.</param>
        /// <param name="blocker">What is in the way, or null.</param>
        /// <returns>True when something is in the way.</returns>
        public bool TryFindBlocker(double x, double z, double heading, double distance, double radius,
            out Obstacle blocker)
        {
            blocker = null;

            var toX = x + Math.Sin(heading)*distance;
            var toZ = z + Math.Cos(heading)*distance;
            var nearest = double.MaxValue;

            foreach (var obstacle in _obstacles)
            {
                if (DistanceToSegment(obstacle.X, obstacle.Z, x, z, toX, toZ) >= obstacle.Radius + radius)
                    continue;

                var dx = obstacle.X - x;
                var dz = obstacle.Z - z;
                var range = dx*dx + dz*dz;
                if (range >= nearest)
                    continue;

                nearest = range;
                blocker = obstacle;
            }

            return blocker != null;
        }

        /// <summary>
        ///     How far a point lies from a line segment — the one piece of geometry this game could not do without.
        /// </summary>
        /// <param name="px">The point, east.</param>
        /// <param name="pz">The point, north.</param>
        /// <param name="ax">One end, east.</param>
        /// <param name="az">One end, north.</param>
        /// <param name="bx">The other end, east.</param>
        /// <param name="bz">The other end, north.</param>
        /// <returns>The distance.</returns>
        public static double DistanceToSegment(double px, double pz, double ax, double az, double bx, double bz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var lengthSquared = dx*dx + dz*dz;

            // A segment of no length is a point, and the projection below would divide by zero working that out.
            if (lengthSquared <= double.Epsilon)
                return Math.Sqrt((px - ax)*(px - ax) + (pz - az)*(pz - az));

            // Clamped, which is what makes this a segment rather than an infinite line - without it a shell that
            // stopped short still counts as having passed through everything further along its bearing.
            var t = Math.Clamp(((px - ax)*dx + (pz - az)*dz)/lengthSquared, 0.0, 1.0);
            var nx = ax + t*dx;
            var nz = az + t*dz;

            return Math.Sqrt((px - nx)*(px - nx) + (pz - nz)*(pz - nz));
        }

        /// <summary>Drives the player, refusing any move that would end up inside something.</summary>
        /// <param name="seconds">How long the frame lasted.</param>
        /// <param name="turn">Which way to swing.</param>
        /// <param name="throttle">Forward, back or neither.</param>
        private void MovePlayer(double seconds, int turn, int throttle)
        {
            if (turn != 0)
                PlayerHeading = WrapAngle(PlayerHeading + Math.Sign(turn)*PlayerTurnRate*seconds);

            if (throttle == 0)
                return;

            var speed = throttle > 0 ? PlayerSpeed : -PlayerReverse;
            var step = speed*seconds;
            var nx = PlayerX + Math.Sin(PlayerHeading)*step;
            var nz = PlayerZ + Math.Cos(PlayerHeading)*step;

            if (IsBlocked(nx, nz, PlayerRadius) || StandsOnEnemy(nx, nz))
                return;

            PlayerX = nx;
            PlayerZ = nz;
        }

        /// <summary>Whether a position would put the player inside an enemy, which nothing may do.</summary>
        /// <param name="x">Where, east.</param>
        /// <param name="z">Where, north.</param>
        /// <returns>True when an enemy is standing there.</returns>
        private bool StandsOnEnemy(double x, double z)
        {
            foreach (var enemy in _enemies)
            {
                if (!enemy.Alive || enemy.Kind == EnemyKindEnum.Saucer)
                    continue;

                var dx = enemy.X - x;
                var dz = enemy.Z - z;
                var reach = enemy.Radius + PlayerRadius;
                if (dx*dx + dz*dz < reach*reach)
                    return true;
            }

            return false;
        }

        /// <summary>Flies every shell and works out what it hit on the way.</summary>
        /// <param name="seconds">How long the frame lasted.</param>
        private void AdvanceShells(double seconds)
        {
            for (var i = _shells.Count - 1; i >= 0; i--)
            {
                var shell = _shells[i];
                shell.Advance(seconds, ShellSpeed);

                if (shell.Travelled > ShellRange)
                    shell.Alive = false;
                else if (HitsScenery(shell))
                    shell.Alive = false;
                else if (shell.FromPlayer)
                    CheckPlayerShell(shell);
                else
                    CheckEnemyShell(shell);

                if (!shell.Alive)
                    _shells.RemoveAt(i);
            }
        }

        /// <summary>Whether a shell ran into a block on the way through.</summary>
        /// <param name="shell">The shell.</param>
        /// <returns>True when it did.</returns>
        private bool HitsScenery(Shell shell)
        {
            foreach (var obstacle in _obstacles)
            {
                if (DistanceToSegment(obstacle.X, obstacle.Z, shell.FromX, shell.FromZ, shell.X, shell.Z) <
                    obstacle.Radius)
                    return true;
            }

            return false;
        }

        /// <summary>Works out whether one of the player's shells caught anything.</summary>
        /// <param name="shell">The shell.</param>
        private void CheckPlayerShell(Shell shell)
        {
            foreach (var enemy in _enemies)
            {
                if (!enemy.Alive)
                    continue;

                // The SEGMENT, not the shell's current position. See the remarks on Shell: at this speed a point
                // test misses about a third of the hits a player is certain they made, which reads as the gun being
                // broken rather than as a rounding problem.
                if (DistanceToSegment(enemy.X, enemy.Z, shell.FromX, shell.FromZ, shell.X, shell.Z) > enemy.Radius)
                    continue;

                enemy.Alive = false;
                shell.Alive = false;

                Score += enemy.Value;
                Kills++;
                _explosions.Add(new Explosion(enemy.X, enemy.Z, enemy.Altitude, enemy.Radius*2.4, _explosionSeed++));

                Message = enemy.Kind switch
                {
                    EnemyKindEnum.Saucer => "Saucer down. 5000.",
                    EnemyKindEnum.SuperTank => "Super tank destroyed.",
                    _ => "Tank destroyed."
                };

                AwardBonusLife();
                return;
            }
        }

        /// <summary>Works out whether an enemy shell caught the player.</summary>
        /// <param name="shell">The shell.</param>
        private void CheckEnemyShell(Shell shell)
        {
            if (DistanceToSegment(PlayerX, PlayerZ, shell.FromX, shell.FromZ, shell.X, shell.Z) > PlayerRadius)
                return;

            shell.Alive = false;
            Break(shell.Heading + Math.PI);
        }

        /// <summary>Loses a tank and breaks the viewport.</summary>
        /// <param name="fromHeading">Which way the shot came from, so the break can start on that side.</param>
        private void Break(double fromHeading)
        {
            IsCracked = true;
            CrackBearing = WrapAngle(fromHeading - PlayerHeading);
            Lives--;

            if (Lives <= 0)
            {
                Lives = 0;
                IsOver = true;
                Message = "Destroyed. ENTER for another game, ESC to leave.";
                return;
            }

            IsRespawning = true;
            _crackFor = CrackSeconds;
            Message = "Hit. " + Lives + (Lives == 1 ? " tank left." : " tanks left.");
        }

        /// <summary>Counts down the broken screen and then issues the next tank.</summary>
        /// <param name="seconds">How long the frame lasted.</param>
        private void Respawn(double seconds)
        {
            _crackFor -= seconds;
            if (_crackFor > 0.0)
                return;

            IsRespawning = false;
            IsCracked = false;
            _shells.Clear();

            // The player comes back where they fell rather than at the origin, so the landmarks they had learned are
            // still the landmarks - but everything hostile is pushed back out to arm's length, since reappearing
            // under the guns of the tank that just killed you is not a game.
            foreach (var enemy in _enemies)
            {
                if (!enemy.Alive)
                    continue;

                var bearing = Math.Atan2(enemy.X - PlayerX, enemy.Z - PlayerZ);
                enemy.X = PlayerX + Math.Sin(bearing)*230.0;
                enemy.Z = PlayerZ + Math.Cos(bearing)*230.0;
            }

            Message = "Back in the fight.";
        }

        /// <summary>Ages every explosion and forgets the ones that are done.</summary>
        /// <param name="seconds">How long the frame lasted.</param>
        private void AdvanceExplosions(double seconds)
        {
            for (var i = _explosions.Count - 1; i >= 0; i--)
            {
                _explosions[i].Age += seconds;
                if (!_explosions[i].Alive)
                    _explosions.RemoveAt(i);
            }
        }

        /// <summary>Hands out a tank every so many points.</summary>
        private void AwardBonusLife()
        {
            if (Score < _nextBonusAt)
                return;

            Lives++;
            _nextBonusAt += BonusLifeEvery;
            Message = "Bonus tank.";
        }

        /// <summary>Keeps exactly one hostile on the plain, and lets a saucer wander through now and then.</summary>
        /// <param name="seconds">How long the frame lasted.</param>
        private void Reinforce(double seconds)
        {
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                var dx = enemy.X - PlayerX;
                var dz = enemy.Z - PlayerZ;

                // A saucer that has crossed the plain is gone rather than turned round - it never had any interest
                // in the player and pretending otherwise would make it another tank.
                if (!enemy.Alive || (enemy.Kind == EnemyKindEnum.Saucer && dx*dx + dz*dz > RecycleRange*RecycleRange))
                    _enemies.RemoveAt(i);
            }

            var hostiles = 0;
            foreach (var enemy in _enemies)
            {
                if (enemy.IsHostile)
                    hostiles++;
            }

            if (hostiles == 0)
                SpawnHostile();

            _saucerDue -= seconds;
            if (_saucerDue > 0.0)
                return;

            _saucerDue = 26.0 + _random.NextDouble()*22.0;
            SpawnSaucer();
        }

        /// <summary>
        ///     Puts a tank on the plain, out of sight if it can manage it.
        ///     <para>
        ///         Internal rather than private so a test can build an exact situation instead of advancing a
        ///         randomised game until something like it turns up — the same bargain <c>MissileField.Spawn</c>
        ///         struck. It is real factoring either way: <see cref="Reinforce" /> is its only other caller.
        ///     </para>
        /// </summary>
        internal void SpawnHostile()
        {
            // Super tanks arrive once the player has shown they can handle the ordinary kind, and get more common
            // from there. Difficulty is the only thing the kill count is used for.
            var superChance = Kills switch
            {
                < 2 => 0.0,
                < 5 => 0.25,
                < 9 => 0.45,
                _ => 0.65
            };

            var kind = _random.NextDouble() < superChance ? EnemyKindEnum.SuperTank : EnemyKindEnum.Tank;
            // Somewhere it can actually stand. Dropping a tank inside a block leaves it there for the rest of the
            // game: every direction it might drive is refused, so it turns on the spot for ever and the player is
            // never attacked at all. That is not a hypothetical either - it is one seed in twenty-four, and it is
            // why this loop exists rather than a single unchecked bearing.
            FindClearGround(210.0, 300.0, new Enemy(kind, 0, 0, 0).Radius, out var x, out var z);

            _enemies.Add(new Enemy(kind, x, z, Math.Atan2(PlayerX - x, PlayerZ - z)));
            Message = kind == EnemyKindEnum.SuperTank ? "Super tank approaching." : "Enemy on the radar.";
        }

        /// <summary>Sends a saucer drifting past on some errand of its own.</summary>
        internal void SpawnSaucer()
        {
            var bearing = _random.NextDouble()*2.0*Math.PI;
            var x = PlayerX + Math.Sin(bearing)*300.0;
            var z = PlayerZ + Math.Cos(bearing)*300.0;

            // It flies, so it needs no clear ground - which is the one place the check above genuinely does not
            // apply rather than having been forgotten.
            var across = bearing + Math.PI + (_random.NextBool() ? 0.45 : -0.45);
            _enemies.Add(new Enemy(EnemyKindEnum.Saucer, x, z, across));
        }

        /// <summary>
        ///     Picks somewhere in a ring around the player that nothing is already standing in.
        /// </summary>
        /// <param name="minimum">How close to the player it may be.</param>
        /// <param name="maximum">How far away it may be.</param>
        /// <param name="radius">How wide the thing being placed is.</param>
        /// <param name="x">Where it landed, east.</param>
        /// <param name="z">Where it landed, north.</param>
        private void FindClearGround(double minimum, double maximum, double radius, out double x, out double z)
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var bearing = _random.NextDouble()*2.0*Math.PI;
                var range = minimum + _random.NextDouble()*(maximum - minimum);

                x = PlayerX + Math.Sin(bearing)*range;
                z = PlayerZ + Math.Cos(bearing)*range;

                if (!IsBlocked(x, z, radius))
                    return;
            }

            // Sixteen blocks over a ring this size cannot fill it, so this is unreachable in practice - but a
            // fallback that puts the tank somewhere legal beats one that gives up and returns nothing.
            x = PlayerX;
            z = PlayerZ + maximum;
        }

        /// <summary>Picks up scenery nobody can see any more and puts it down somewhere useful.</summary>
        private void Recycle()
        {
            foreach (var obstacle in _obstacles)
            {
                var dx = obstacle.X - PlayerX;
                var dz = obstacle.Z - PlayerZ;
                if (dx*dx + dz*dz < RecycleRange*RecycleRange)
                    continue;

                Scatter(obstacle, 140.0, 400.0);
            }
        }

        /// <summary>Puts one piece of scenery somewhere it will not be standing on anything.</summary>
        /// <param name="obstacle">What to move.</param>
        /// <param name="minimum">How close to the player it may land.</param>
        /// <param name="maximum">How far away it may land.</param>
        private void Scatter(Obstacle obstacle, double minimum, double maximum)
        {
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var bearing = _random.NextDouble()*2.0*Math.PI;
                var range = minimum + _random.NextDouble()*(maximum - minimum);
                var x = PlayerX + Math.Sin(bearing)*range;
                var z = PlayerZ + Math.Cos(bearing)*range;

                if (StandsClear(obstacle, x, z))
                {
                    obstacle.X = x;
                    obstacle.Z = z;
                    obstacle.Kind = _random.NextBool() ? ObstacleKindEnum.Cube : ObstacleKindEnum.Pyramid;
                    return;
                }
            }
        }

        /// <summary>Whether a place is free of everything else that is standing about.</summary>
        /// <param name="moving">The obstacle being placed, which cannot collide with itself.</param>
        /// <param name="x">Where, east.</param>
        /// <param name="z">Where, north.</param>
        /// <returns>True when it fits.</returns>
        private bool StandsClear(Obstacle moving, double x, double z)
        {
            foreach (var other in _obstacles)
            {
                if (ReferenceEquals(other, moving))
                    continue;

                var dx = other.X - x;
                var dz = other.Z - z;
                var reach = other.Radius + moving.Radius + 14.0;
                if (dx*dx + dz*dz < reach*reach)
                    return false;
            }

            // Dropping a block on a tank would be a hard thing to explain, and dropping one on the player would be
            // worse - they would be standing inside it and unable to move in any direction.
            foreach (var enemy in _enemies)
            {
                var dx = enemy.X - x;
                var dz = enemy.Z - z;
                var reach = enemy.Radius + moving.Radius + 10.0;
                if (dx*dx + dz*dz < reach*reach)
                    return false;
            }

            var px = PlayerX - x;
            var pz = PlayerZ - z;
            var clearance = PlayerRadius + moving.Radius + 18.0;
            return px*px + pz*pz >= clearance*clearance;
        }
    }
}
