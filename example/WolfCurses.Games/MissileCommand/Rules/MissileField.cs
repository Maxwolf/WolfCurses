// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Core;

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     The whole of Missile Command, and nothing that knows what a terminal is. Hand it a random source, call
    ///     <see cref="Advance" /> with however long has really passed, and ask it where everything is.
    ///     <para>
    ///         <b>The world is measured in square units.</b> It runs from 0 to <see cref="Aspect" /> across and 0 to 1
    ///         up, with the ground at the bottom, so one unit means the same distance on both axes. That is worth
    ///         stating because the obvious alternative — normalising both axes to 0..1 — is wrong in a way that takes
    ///         a long evening to find: on a field half again as wide as it is tall, <c>dx*dx + dy*dy &lt;= r*r</c>
    ///         stops describing a circle and starts describing an ellipse, so blasts destroy warheads they visibly
    ///         miss and miss warheads they visibly touch. Nothing is inconsistent when that happens — the physics is
    ///         self-consistent and so is the drawing, and only their agreement with each other is broken, which is
    ///         precisely why it is hard to see.
    ///     </para>
    ///     <para>
    ///         Altitude increases upwards, which is the opposite of a screen row. Both renderers flip it once, on the
    ///         way out, and nothing in here ever thinks about rows.
    ///     </para>
    /// </summary>
    public sealed class MissileField
    {
        /// <summary>How much wider the field is than it is tall. Everything horizontal runs from 0 to this.</summary>
        public const double Aspect = 1.6;

        /// <summary>The altitude the cities and batteries stand at.</summary>
        public const double GroundY = 0.06;

        /// <summary>
        ///     The lowest the player may aim. Without it the quickest way to lose is to defend a city by blowing it
        ///     up, and a player who does that once has no way of knowing they were allowed to.
        /// </summary>
        public const double MinAimY = 0.20;

        /// <summary>How many counter-missiles may be in the air at once, across all three batteries.</summary>
        public const int MaxCountersInFlight = 8;

        /// <summary>How many enemy warheads may be in the air at once, which is what stops a late wave being unplayable.</summary>
        public const int MaxEnemiesInFlight = 8;

        /// <summary>Shells each battery starts every wave with.</summary>
        public const int AmmoPerSilo = 10;

        /// <summary>
        ///     The most cities a single wave is allowed to take. This is the rule that makes the game fair rather
        ///     than merely hard: without it a bad wave can end a run outright, and the player never learns anything
        ///     from a defeat that was decided by the random number generator before they touched a key.
        /// </summary>
        public const int MaxCitiesLostPerWave = 3;

        /// <summary>Points needed for a spare city, banked and rebuilt between waves.</summary>
        private const int PointsPerBonusCity = 10_000;

        /// <summary>How close an impact has to be to a structure to flatten it.</summary>
        private const double StructureHalfWidth = 0.05;

        /// <summary>How long the wave tally stays on screen before the next wave starts.</summary>
        private const double IntermissionSeconds = 2.5;

        /// <summary>The altitude a MIRV lets go at, high enough that its heads have room to spread.</summary>
        private const double SplitAltitude = 0.62;

        /// <summary>
        ///     How far ahead of itself a smart bomb looks for something to dodge, in world units. Short on purpose:
        ///     tested against its whole remaining flight it would swerve around a fireball ten seconds away, and
        ///     since a swerve restarts its flight it would swerve again on the next frame and hover indefinitely
        ///     without ever coming down.
        /// </summary>
        private const double EvadeLookAhead = 0.18;

        /// <summary>
        ///     How many times one smart bomb will dodge before giving up and flying straight. A bound rather than a
        ///     rule of the original, and it is what makes them beatable: three fireballs in its way and it stops
        ///     being clever, so a player who boxes one in is rewarded rather than made to wait it out.
        /// </summary>
        private const int MaxDodges = 3;

        /// <summary>
        ///     The most any single step may advance. See <see cref="Advance" /> — this is the rules class's own
        ///     opinion about how far anything may move at once, and it belongs here rather than in the caller.
        /// </summary>
        private const double MaxStepSeconds = 0.1;

        /// <summary>Where the six cities stand: three either side of the middle battery.</summary>
        private static readonly double[] _cityPositions = {0.28, 0.44, 0.60, 1.00, 1.16, 1.32};

        /// <summary>Where the three batteries stand: one at each end and one in the middle.</summary>
        private static readonly double[] _siloPositions = {0.12, 0.80, 1.48};

        /// <summary>
        ///     How fast a warhead falls, by wave, in world units a second. Waves past the end of the table repeat the
        ///     last row, which is roughly where the arcade stopped getting harder too.
        /// </summary>
        private static readonly double[] _waveSpeeds =
            {0.055, 0.070, 0.085, 0.100, 0.118, 0.135, 0.150, 0.168, 0.185, 0.200, 0.215, 0.230, 0.245, 0.260};

        /// <summary>
        ///     How many warheads each wave sends. The dips at waves 4, 8 and 12 are deliberate rather than a typo:
        ///     the arcade eases off every fourth wave so the difficulty curve has somewhere to breathe, and a run of
        ///     strictly increasing numbers feels worse to play despite reading better in a table.
        /// </summary>
        private static readonly int[] _waveCounts = {12, 15, 18, 12, 16, 14, 17, 10, 13, 16, 19, 12, 15, 18};

        /// <summary>How many smart bombs come with each wave; they start turning up around the sixth.</summary>
        private static readonly int[] _waveSmartBombs = {0, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5};

        private readonly Randomizer _random;
        private readonly List<Missile> _missiles = new();
        private readonly List<Blast> _blasts = new();
        private readonly bool[] _cities = new bool[6];
        private readonly bool[] _silos = new bool[3];
        private readonly int[] _ammo = new int[3];

        /// <summary>Reused between steps, so a wave splitting overhead does not allocate a list per frame.</summary>
        private readonly List<Missile> _spawned = new();

        private int _remainingIcbms;
        private int _remainingSmartBombs;
        private int _citiesLostThisWave;
        private int _bankedCities;
        private int _nextBonusAt = PointsPerBonusCity;
        private double _launchCountdown;
        private double _intermission;

        /// <summary>Initializes a new instance of the <see cref="MissileField" /> class on its first wave.</summary>
        /// <param name="random">The random source; seed it in a test and the whole game is reproducible.</param>
        public MissileField(Randomizer random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));

            for (var i = 0; i < _cities.Length; i++)
                _cities[i] = true;

            Wave = 0;
            BeginWave();
        }

        /// <summary>Where the six cities stand, in world units.</summary>
        public static IReadOnlyList<double> CityPositions => _cityPositions;

        /// <summary>Where the three batteries stand, in world units.</summary>
        public static IReadOnlyList<double> SiloPositions => _siloPositions;

        /// <summary>Everything in the air, the player's counter-missiles included.</summary>
        public IReadOnlyList<Missile> Missiles => _missiles;

        /// <summary>Every fireball currently burning.</summary>
        public IReadOnlyList<Blast> Blasts => _blasts;

        /// <summary>Which cities are still standing.</summary>
        public IReadOnlyList<bool> CitiesStanding => _cities;

        /// <summary>Which batteries are still standing. A flattened one is rebuilt with the next wave.</summary>
        public IReadOnlyList<bool> SilosStanding => _silos;

        /// <summary>How many shells each battery has left.</summary>
        public IReadOnlyList<int> SiloAmmo => _ammo;

        /// <summary>Which wave is being played, counting from one.</summary>
        public int Wave { get; private set; }

        /// <summary>The score so far.</summary>
        public int Score { get; private set; }

        /// <summary>What every kill is worth this wave, from 1 up to 6.</summary>
        public int Multiplier => Math.Min(6, (Wave + 1)/2);

        /// <summary>How many spare cities are waiting to be rebuilt at the end of the wave.</summary>
        public int BankedCities => _bankedCities;

        /// <summary>True once the last city has gone. The simulation deliberately keeps running afterwards.</summary>
        public bool IsOver { get; private set; }

        /// <summary>True when nothing is moving and nothing is burning, which is when the game is finally finished.</summary>
        public bool IsQuiet => _missiles.Count == 0 && _blasts.Count == 0;

        /// <summary>One line for the screen: the wave tally, a warning about ammunition, or the ending.</summary>
        public string Message { get; private set; }

        /// <summary>How many cities are still standing.</summary>
        public int CitiesRemaining
        {
            get
            {
                var standing = 0;
                foreach (var city in _cities)
                {
                    if (city)
                        standing++;
                }

                return standing;
            }
        }

        /// <summary>How many shells are left across all three batteries.</summary>
        public int AmmoRemaining
        {
            get
            {
                var total = 0;
                foreach (var shells in _ammo)
                    total += shells;

                return total;
            }
        }

        /// <summary>
        ///     Advances everything by however long really passed.
        ///     <para>
        ///         <b>The step is clamped here, in the rules, and not by the caller.</b> Only this class knows how far
        ///         anything is allowed to move at once, and the number matters: a garbage collection, a window resize
        ///         or a laptop lid closing hands the next call several hundred milliseconds, which at a late wave's
        ///         speed is most of the height of the field in one jump. Clamping in the form would leave a test that
        ///         calls <c>Advance(TimeSpan.FromHours(1))</c> teleporting every warhead through the ground.
        ///     </para>
        /// </summary>
        /// <param name="delta">How long has passed since the last call.</param>
        public void Advance(TimeSpan delta)
        {
            var seconds = Math.Clamp(delta.TotalSeconds, 0.0, MaxStepSeconds);
            if (seconds <= 0.0)
                return;

            AdvanceBlasts(seconds);
            AdvanceMissiles(seconds);
            SweepForKills();
            LandArrivals();

            _missiles.RemoveAll(missile => !missile.Alive);

            if (IsOver)
                return;

            AdvanceWave(seconds);
        }

        /// <summary>
        ///     Launches a counter-missile from a battery at a point. Refused rather than thrown when it cannot be
        ///     done, because "the player pressed fire at a bad moment" is not an exceptional circumstance.
        /// </summary>
        /// <param name="silo">Which battery, 0 to 2.</param>
        /// <param name="x">Where to detonate, in world units.</param>
        /// <param name="y">Where to detonate, in world units.</param>
        /// <returns>True when a shell was actually spent.</returns>
        public bool Fire(int silo, double x, double y)
        {
            if (IsOver || silo < 0 || silo >= _silos.Length)
                return false;

            if (!_silos[silo] || _ammo[silo] <= 0 || y < MinAimY)
                return false;

            if (CountInFlight(true) >= MaxCountersInFlight)
                return false;

            _ammo[silo]--;
            _missiles.Add(new Missile(MissileKindEnum.Counter, _siloPositions[silo], GroundY, x, y,
                CounterSpeed(silo), silo));

            if (AmmoRemaining == 0)
                Message = "Out of shells.";
            else if (AmmoRemaining <= 5)
                Message = $"{AmmoRemaining} shells left.";

            return true;
        }

        /// <summary>
        ///     Which battery can put a shell on a point soonest, or -1 when none of them can.
        ///     <para>
        ///         <b>Ranked by flight time, not by distance</b>, and the difference is the entire reason the cabinet
        ///         had three buttons instead of one. The middle battery's shells travel more than twice as fast as
        ///         the flanks', so it is the quicker answer across most of the sky even where it is not the nearer
        ///         one. Ranking by distance compiles, looks sensible, and quietly deletes the only interesting
        ///         decision in the game: the player never discovers that the middle battery is the one worth
        ///         spending, the flanks never feel like the fallback they are, and the whole thing plays harder than
        ///         it should for no visible reason.
        ///     </para>
        /// </summary>
        /// <param name="x">Where the shell has to reach.</param>
        /// <param name="y">Where the shell has to reach.</param>
        /// <returns>The battery index, or -1 when every battery is dry or flattened.</returns>
        public int BestSilo(double x, double y)
        {
            var best = -1;
            var bestTime = double.MaxValue;

            for (var silo = 0; silo < _silos.Length; silo++)
            {
                if (!_silos[silo] || _ammo[silo] <= 0)
                    continue;

                var dx = x - _siloPositions[silo];
                var dy = y - GroundY;
                var time = Math.Sqrt(dx*dx + dy*dy)/CounterSpeed(silo);

                if (time >= bestTime)
                    continue;

                bestTime = time;
                best = silo;
            }

            return best;
        }

        /// <summary>How fast a battery's shells fly. The middle one is the fast one, which is the whole point of it.</summary>
        /// <param name="silo">Which battery.</param>
        /// <returns>Speed in world units a second.</returns>
        public static double CounterSpeed(int silo) => silo == 1 ? 1.85 : 0.80;

        /// <summary>
        ///     Puts a warhead in the sky exactly where asked — which is all <see cref="Launch" /> does once it has
        ///     finished deciding. Internal because the game itself never wants to choose: it is here so that anything
        ///     scripting an exact scenario, a test above all, can build the position it means instead of advancing a
        ///     random wave until something like it happens to appear.
        /// </summary>
        /// <param name="kind">What sort of warhead.</param>
        /// <param name="originX">Where it starts.</param>
        /// <param name="originY">Where it starts.</param>
        /// <param name="targetX">Where it is aimed.</param>
        /// <param name="targetY">Where it is aimed.</param>
        /// <param name="speed">How fast it travels, in world units a second.</param>
        /// <returns>The warhead, so a caller can watch it.</returns>
        internal Missile Spawn(MissileKindEnum kind, double originX, double originY,
            double targetX, double targetY, double speed)
        {
            var missile = new Missile(kind, originX, originY, targetX, targetY, speed);
            _missiles.Add(missile);
            return missile;
        }

        /// <summary>Sets a fireball going at a point, which is what an arriving counter-missile and a dying warhead both do.</summary>
        /// <param name="x">Where it goes off.</param>
        /// <param name="y">Where it goes off.</param>
        /// <param name="fromPlayer">True when the player's own shell made it.</param>
        /// <returns>The fireball, so a caller can watch it.</returns>
        internal Blast Detonate(double x, double y, bool fromPlayer)
        {
            var blast = new Blast(x, y, fromPlayer);
            _blasts.Add(blast);
            return blast;
        }

        /// <summary>Burns every fireball for a step and clears away the ones that have gone out.</summary>
        private void AdvanceBlasts(double seconds)
        {
            foreach (var blast in _blasts)
                blast.Advance(seconds);

            _blasts.RemoveAll(blast => !blast.Alive);
        }

        /// <summary>Moves everything in the air, splitting and dodging on the way past.</summary>
        private void AdvanceMissiles(double seconds)
        {
            _spawned.Clear();

            foreach (var missile in _missiles)
            {
                if (!missile.Alive)
                    continue;

                if (missile.Kind == MissileKindEnum.SmartBomb)
                    Evade(missile);

                missile.Advance(seconds);

                if (missile.Kind == MissileKindEnum.Mirv && !missile.HasSplit && missile.Y <= SplitAltitude)
                    Split(missile);
            }

            // Added after the walk rather than during it. Adding to a List while a foreach is walking it throws, and
            // the index-loop version that does not throw is worse: the new heads are then advanced in the same step
            // they were born in, so a MIRV appears to split downwards.
            _missiles.AddRange(_spawned);
        }

        /// <summary>Lets a warhead's extra heads go, aimed at fresh targets.</summary>
        private void Split(Missile parent)
        {
            parent.HasSplit = true;

            var heads = 2 + _random.Next(2);
            for (var i = 0; i < heads; i++)
            {
                var (x, y) = PickTarget();
                _spawned.Add(Missile.SplitFrom(parent, x, y));
            }
        }

        /// <summary>
        ///     Steers a smart bomb around a fireball it is about to fly into. Crude on purpose — it aims at a point
        ///     off to one side of whatever is in the way, which produces the visible behaviour (it will not be shot
        ///     down by firing where it is going) without any of the arcade's direction tables.
        /// </summary>
        private void Evade(Missile bomb)
        {
            if (bomb.Dodges >= MaxDodges || bomb.Length <= double.Epsilon)
                return;

            // Only the stretch of sky immediately in front of it - see EvadeLookAhead for why the whole remaining
            // flight is the wrong segment to test.
            var scale = Math.Min(EvadeLookAhead, (1.0 - bomb.Progress)*bomb.Length)/bomb.Length;
            var aheadX = bomb.X + (bomb.TargetX - bomb.OriginX)*scale;
            var aheadY = bomb.Y + (bomb.TargetY - bomb.OriginY)*scale;

            foreach (var blast in _blasts)
            {
                if (!blast.Catches(bomb.X, bomb.Y, aheadX, aheadY))
                    continue;

                // Aimed at the ground past the far side of what is in the way, so a dodge is still a descent. A
                // sideways-only dodge would leave it drifting along at the same altitude looking ridiculous.
                var away = bomb.X < blast.X ? -1.0 : 1.0;
                var dodgeX = Math.Clamp(blast.X + away*(blast.Radius*3.0 + 0.05), 0.0, Aspect);

                bomb.Redirect(dodgeX, GroundY);
                bomb.Dodges++;
                return;
            }
        }

        /// <summary>
        ///     Destroys everything that flew into a fireball this step, setting off a fireball of its own where it
        ///     died — which is the chain reaction, and the best thing in this game.
        ///     <para>
        ///         <b>That <c>break</c> is load-bearing and is not merely an optimisation.</b> Killing a warhead adds
        ///         a fireball to the very list this loop is walking, and leaving the loop immediately is what stops
        ///         the enumerator ever being advanced over a collection that has changed underneath it. Take the
        ///         <c>break</c> out — to let one warhead be scored against several fireballs, say — and the first
        ///         chain of two throws <see cref="InvalidOperationException" />, which is not on any frame anybody
        ///         tests and very much on the first frame anybody enjoys.
        ///     </para>
        ///     <para>
        ///         The <i>other</i> thing making it safe to detonate in place is that a new fireball is born at zero
        ///         radius, so a warhead later in this list cannot be caught by one created moments ago in the same
        ///         step. That is also the whole reason a cascade reads as a cascade instead of as one lucky shot: it
        ///         spreads over frames because fireballs grow, not because anything here defers the work. Both facts
        ///         live in <see cref="Blast.Radius" />, so a change to how a blast starts its life lands here.
        ///     </para>
        ///     <para>
        ///         Counter-missiles are skipped, which is the arcade's rule that you may fire straight through your
        ///         own cloud. It is also the only thing stopping a wall of blasts from being a wall to the player too.
        ///     </para>
        /// </summary>
        private void SweepForKills()
        {
            foreach (var missile in _missiles)
            {
                if (!missile.Alive || missile.Kind == MissileKindEnum.Counter)
                    continue;

                foreach (var blast in _blasts)
                {
                    if (!blast.Catches(missile.PreviousX, missile.PreviousY, missile.X, missile.Y))
                        continue;

                    missile.Alive = false;
                    Award(missile.Kind == MissileKindEnum.SmartBomb ? 125 : 25);
                    Detonate(missile.X, missile.Y, false);
                    break;
                }
            }
        }

        /// <summary>Detonates arriving counter-missiles and flattens whatever an arriving warhead was aimed at.</summary>
        private void LandArrivals()
        {
            foreach (var missile in _missiles)
            {
                if (!missile.Alive || !missile.HasArrived)
                    continue;

                missile.Alive = false;

                if (missile.Kind == MissileKindEnum.Counter)
                {
                    Detonate(missile.X, missile.Y, true);
                    continue;
                }

                // A warhead that gets through is worth nothing to anybody and leaves no fireball to chain from - it
                // is simply damage.
                Flatten(missile.X);
            }
        }

        /// <summary>Knocks down any city or battery standing where a warhead came down.</summary>
        private void Flatten(double x)
        {
            for (var i = 0; i < _cities.Length; i++)
            {
                if (_cities[i] && Math.Abs(_cityPositions[i] - x) <= StructureHalfWidth)
                {
                    _cities[i] = false;
                    _citiesLostThisWave++;
                }
            }

            for (var i = 0; i < _silos.Length; i++)
            {
                if (_silos[i] && Math.Abs(_siloPositions[i] - x) <= StructureHalfWidth)
                    _silos[i] = false;
            }

            if (CitiesRemaining > 0)
                return;

            IsOver = true;
            Message = "GAME OVER - press ENTER for the menu";
        }

        /// <summary>Launches this wave's warheads on a schedule, then tallies the wave up and starts the next one.</summary>
        private void AdvanceWave(double seconds)
        {
            if (_intermission > 0.0)
            {
                _intermission -= seconds;
                if (_intermission <= 0.0)
                    BeginWave();

                return;
            }

            if (_remainingIcbms + _remainingSmartBombs > 0)
            {
                _launchCountdown -= seconds;
                if (_launchCountdown <= 0.0)
                    Launch();

                return;
            }

            // The wave is only over once the sky is clear as well as the launcher: a warhead still falling can still
            // take a city, and ending the wave under it would rebuild the city it was about to destroy.
            if (CountInFlight(false) > 0)
                return;

            TallyWave();
        }

        /// <summary>Sends one warhead, if there is room in the sky for it.</summary>
        private void Launch()
        {
            if (CountInFlight(false) >= MaxEnemiesInFlight)
                return;

            var gap = Math.Max(0.35, 1.6 - Wave*0.06);
            _launchCountdown = gap*(0.5 + _random.NextDouble());

            var smart = _remainingSmartBombs > 0 &&
                        (_remainingIcbms == 0 || _random.Next(_remainingIcbms + _remainingSmartBombs) < _remainingSmartBombs);

            var (targetX, targetY) = PickTarget();
            var speed = _waveSpeeds[Math.Min(Wave - 1, _waveSpeeds.Length - 1)];
            var originX = _random.NextDouble()*Aspect;

            if (smart)
            {
                _remainingSmartBombs--;
                _missiles.Add(new Missile(MissileKindEnum.SmartBomb, originX, 1.0, targetX, targetY, speed*0.7));
                return;
            }

            _remainingIcbms--;

            // A quarter of the warheads carry more, from the third wave on. It is a flag rather than a separate
            // launch path because once a MIRV has split there is nothing left that distinguishes it.
            var kind = Wave >= 3 && _random.Next(4) == 0 ? MissileKindEnum.Mirv : MissileKindEnum.Icbm;
            _missiles.Add(new Missile(kind, originX, 1.0, targetX, targetY, speed));
        }

        /// <summary>
        ///     Picks what the next warhead is aimed at, refusing to aim at a city once this wave has already claimed
        ///     or committed to <see cref="MaxCitiesLostPerWave" /> of them.
        /// </summary>
        /// <returns>A ground point.</returns>
        private (double X, double Y) PickTarget()
        {
            var standing = new List<double>();
            for (var i = 0; i < _cities.Length; i++)
            {
                if (_cities[i])
                    standing.Add(_cityPositions[i]);
            }

            var committed = _citiesLostThisWave + CountCitiesUnderFire();
            if (standing.Count > 0 && committed < MaxCitiesLostPerWave)
                return (standing[_random.Next(standing.Count)], GroundY);

            var silos = new List<double>();
            for (var i = 0; i < _silos.Length; i++)
            {
                if (_silos[i])
                    silos.Add(_siloPositions[i]);
            }

            if (silos.Count > 0)
                return (silos[_random.Next(silos.Count)], GroundY);

            // Everything worth hitting is already flat, so it comes down somewhere harmless rather than nowhere.
            return (_random.NextDouble()*Aspect, GroundY);
        }

        /// <summary>How many standing cities already have a warhead on the way to them.</summary>
        private int CountCitiesUnderFire()
        {
            var counted = 0;
            for (var i = 0; i < _cities.Length; i++)
            {
                if (!_cities[i])
                    continue;

                foreach (var missile in _missiles)
                {
                    if (!missile.Alive || missile.Kind == MissileKindEnum.Counter)
                        continue;

                    if (Math.Abs(missile.TargetX - _cityPositions[i]) > StructureHalfWidth)
                        continue;

                    counted++;
                    break;
                }
            }

            return counted;
        }

        /// <summary>How many of one side's missiles are in the air.</summary>
        /// <param name="counters">True to count the player's, false to count the enemy's.</param>
        /// <returns>The count.</returns>
        private int CountInFlight(bool counters)
        {
            var counted = 0;
            foreach (var missile in _missiles)
            {
                if (!missile.Alive)
                    continue;

                var isCounter = missile.Kind == MissileKindEnum.Counter;
                if (isCounter == counters)
                    counted++;
            }

            return counted;
        }

        /// <summary>Pays the end-of-wave bonus and puts the tally on screen.</summary>
        private void TallyWave()
        {
            var shells = AmmoRemaining;
            var cities = CitiesRemaining;
            var shellBonus = shells*5*Multiplier;
            var cityBonus = cities*100*Multiplier;

            Award(shellBonus + cityBonus);

            Message = string.Format(CultureInfo.InvariantCulture,
                "Wave {0} cleared - {1} shells x {2} = {3}, {4} cities x {5} = {6}",
                Wave, shells, 5*Multiplier, shellBonus, cities, 100*Multiplier, cityBonus);

            _intermission = IntermissionSeconds;
        }

        /// <summary>Adds points, and banks a spare city each time the score passes another ten thousand.</summary>
        private void Award(int points)
        {
            Score += points;

            while (Score >= _nextBonusAt)
            {
                _bankedCities++;
                _nextBonusAt += PointsPerBonusCity;
            }
        }

        /// <summary>Rebuilds the batteries, rearms them, spends any banked cities and deals the next wave.</summary>
        private void BeginWave()
        {
            Wave++;
            _citiesLostThisWave = 0;

            for (var i = 0; i < _silos.Length; i++)
            {
                _silos[i] = true;
                _ammo[i] = AmmoPerSilo;
            }

            // Spare cities are spent on craters, oldest first, and quietly forgotten once every city stands.
            for (var i = 0; i < _cities.Length && _bankedCities > 0; i++)
            {
                if (_cities[i])
                    continue;

                _cities[i] = true;
                _bankedCities--;
            }

            var row = Math.Min(Wave - 1, _waveCounts.Length - 1);
            _remainingIcbms = _waveCounts[row];
            _remainingSmartBombs = _waveSmartBombs[row];
            _launchCountdown = 1.0;

            if (Wave > 1)
                Message = $"Wave {Wave}. Everything rebuilt and rearmed.";
            else
                Message = "Defend the cities. Arrows aim, SPACE fires.";
        }
    }
}
