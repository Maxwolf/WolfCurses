// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     Walks the world once and hands out every line that should be on the screen, tagged with what it is.
    ///     <para>
    ///         <b>One scene walk, two rasterizers.</b> The picture and the character view are drawn by classes that
    ///         know how to put down a coloured line and a glyph respectively, and nothing else — where the horizon
    ///         sits, how big the radar is, which way a tank is facing and what the broken glass looks like are all
    ///         decided exactly once, here. Writing it twice would be two chances to draw a different game, and the
    ///         difference would show up only on whichever terminal the author was not using.
    ///     </para>
    ///     <para>
    ///         Everything is emitted in the order it should be laid down, back to front: horizon, then the world,
    ///         then the instruments, then the broken glass over all of it. That matters for the character view,
    ///         where a later glyph replaces an earlier one in the same cell — the picture composites and would look
    ///         much the same either way, which is exactly how a draw-order bug would hide.
    ///     </para>
    /// </summary>
    public sealed class BattleScene
    {
        /// <summary>How far the radar sees. Beyond this a tank is a rumour.</summary>
        public const double RadarRange = 360.0;

        /// <summary>How many columns the mountain profile is sampled at. One per column is cheap and exact.</summary>
        private const int HorizonStep = 1;

        /// <summary>How far round the compass the volcano stands.</summary>
        private const double VolcanoBearing = 2.10;

        /// <summary>
        ///     Where the broken glass runs, as bearings from the impact. Fixed rather than random, so the break does
        ///     not crawl about while the player is looking at it.
        /// </summary>
        private static readonly double[] _crackSpokes =
        {
            0.15, 0.72, 1.35, 1.98, 2.55, 3.05, 3.60, 4.20, 4.85, 5.50
        };

        /// <summary>How far out each ring of the break sits, as a fraction of the view. Jittered per spoke below.</summary>
        private static readonly double[] _crackRings = {0.06, 0.14, 0.24};

        private readonly WireCamera _camera;
        private readonly Action<int, int, int, int> _emit;

        private Action<int, int, int, int, BattleInkEnum> _sink;
        private BattleInkEnum _ink;

        /// <summary>Initializes a new instance of the <see cref="BattleScene" /> class.</summary>
        /// <param name="camera">The eye to draw through.</param>
        public BattleScene(WireCamera camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));

            // Built once and kept. The camera takes a plain four-integer sink because it knows nothing about this
            // game; this closure is what carries the current ink across without allocating one per model per frame.
            _emit = (x0, y0, x1, y1) => _sink(x0, y0, x1, y1, _ink);
        }

        /// <summary>The eye this scene is drawn through.</summary>
        public WireCamera Camera => _camera;

        /// <summary>
        ///     Draws the whole scene from where the player is standing.
        /// </summary>
        /// <param name="field">The world.</param>
        /// <param name="sink">Where the finished screen-space segments go.</param>
        public void Draw(BattleField field, Action<int, int, int, int, BattleInkEnum> sink)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _camera.SetView(field.PlayerX, field.PlayerZ, field.PlayerHeading, 6.0);

            DrawHorizon();
            DrawWorld(field);
            DrawReticle();
            DrawRadar(field);

            if (field.IsCracked)
                DrawCrack(field.CrackBearing);
        }

        /// <summary>
        ///     How high the mountains stand at a bearing, as an angle above the horizon.
        ///     <para>
        ///         <b>Measured in elevation rather than in rows</b>, which is what lets the same range look the same
        ///         height in a two-hundred-pixel picture and an eighteen-row character grid. A few sines added
        ///         together give a ridge that never repeats visibly and needs no data, and because it is a pure
        ///         function of the absolute bearing the mountains are nailed to the compass: they swing past as the
        ///         player turns and do not move an inch as the player drives, which is what being infinitely far
        ///         away means.
        ///     </para>
        /// </summary>
        /// <param name="bearing">The compass bearing, in radians.</param>
        /// <returns>The elevation of the ridge line, in radians.</returns>
        public static double MountainElevation(double bearing)
        {
            // The base has to exceed the three amplitudes added together, or the ridge dips BELOW the horizon at
            // some bearings - a mountain range drawn under the line where the ground meets the sky, which reads as
            // the horizon being broken rather than as a range with a valley in it.
            var elevation = 0.075
                            + 0.032*Math.Sin(bearing*3.0 + 1.10)
                            + 0.020*Math.Sin(bearing*7.0 + 2.35)
                            + 0.012*Math.Sin(bearing*13.0 + 0.44);

            // The volcano, which every cabinet had and which is the only landmark on the whole plain that can be
            // used to steer by - the mountains repeat, this does not.
            var offset = BattleField.WrapAngle(bearing - VolcanoBearing);
            elevation += 0.170*Math.Exp(-(offset*offset)/0.018);

            return elevation;
        }

        /// <summary>Draws the ground line and the mountains standing on it.</summary>
        private void DrawHorizon()
        {
            _ink = BattleInkEnum.Horizon;

            var horizon = (int) Math.Round(_camera.HorizonRow);
            _sink(0, horizon, _camera.Width - 1, horizon, BattleInkEnum.Horizon);

            var previousColumn = 0;
            var previousRow = RidgeRow(0);

            for (var column = HorizonStep; column < _camera.Width; column += HorizonStep)
            {
                var row = RidgeRow(column);
                _sink(previousColumn, previousRow, column, row, BattleInkEnum.Horizon);
                previousColumn = column;
                previousRow = row;
            }
        }

        /// <summary>Where the ridge line sits at a column.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The row.</returns>
        private int RidgeRow(int column)
        {
            var bearing = _camera.Heading + _camera.BearingAtColumn(column);
            return (int) Math.Round(_camera.HorizonRow - _camera.FocalY*Math.Tan(MountainElevation(bearing)));
        }

        /// <summary>Draws everything standing on the plain.</summary>
        /// <param name="field">The world.</param>
        private void DrawWorld(BattleField field)
        {
            _ink = BattleInkEnum.Scenery;
            foreach (var obstacle in field.Obstacles)
            {
                if (!InRange(field, obstacle.X, obstacle.Z))
                    continue;

                _camera.DrawModel(WireModel.For(obstacle.Kind), obstacle.X, obstacle.Z, 0.0, 0.0, obstacle.Radius,
                    _emit);
            }

            foreach (var enemy in field.Enemies)
            {
                if (!enemy.Alive || !InRange(field, enemy.X, enemy.Z))
                    continue;

                _ink = enemy.Kind == EnemyKindEnum.Saucer ? BattleInkEnum.Saucer : BattleInkEnum.Enemy;
                _camera.DrawModel(WireModel.For(enemy.Kind), enemy.X, enemy.Z, enemy.Altitude, enemy.Heading,
                    enemy.Radius, _emit);
            }

            _ink = BattleInkEnum.Shell;
            foreach (var shell in field.Shells)
            {
                if (!InRange(field, shell.X, shell.Z))
                    continue;

                _camera.DrawModel(WireModel.Shell, shell.X, shell.Z, 3.0, 0.0, 1.4, _emit);
            }

            _ink = BattleInkEnum.Explosion;
            foreach (var explosion in field.Explosions)
                DrawExplosion(explosion);
        }

        /// <summary>
        ///     Draws one explosion as a burst of fragments flying outward.
        ///     <para>
        ///         The fragments are recomputed from the explosion's seed every frame rather than stored, so the
        ///         rules keep no opinion about what an explosion looks like and nothing is allocated per frame. The
        ///         mixing below is not a random number generator and does not need to be — it needs to give the same
        ///         scattered-looking directions for the same seed, every time.
        ///     </para>
        /// </summary>
        /// <param name="explosion">What blew up.</param>
        private void DrawExplosion(Explosion explosion)
        {
            const int fragments = 11;
            var reach = explosion.Size*(0.4 + 2.6*explosion.Progress);

            for (var i = 0; i < fragments; i++)
            {
                var mixed = explosion.Seed*7919 + i*2654435761L;
                var bearing = (mixed%1000)/1000.0*2.0*Math.PI;
                var lift = ((mixed/1000)%1000)/1000.0*1.6 - 0.35;

                var x = explosion.X + Math.Sin(bearing)*reach;
                var z = explosion.Z + Math.Cos(bearing)*reach;
                var y = explosion.Y + lift*reach;

                // Drawn from a point part of the way out rather than from the centre, so what is on screen is a
                // ring of fragments coming apart and not a star that never stops being a star.
                var innerX = explosion.X + Math.Sin(bearing)*reach*0.55;
                var innerZ = explosion.Z + Math.Cos(bearing)*reach*0.55;
                var innerY = explosion.Y + lift*reach*0.55;

                if (_camera.TryProject(innerX, innerY, innerZ, out var x0, out var y0) &&
                    _camera.TryProject(x, y, z, out var x1, out var y1))
                    _sink(x0, y0, x1, y1, BattleInkEnum.Explosion);
            }
        }

        /// <summary>Whether something is near enough to bother drawing.</summary>
        /// <param name="field">The world.</param>
        /// <param name="x">Where it is, east.</param>
        /// <param name="z">Where it is, north.</param>
        /// <returns>True when it is within the draw distance.</returns>
        private static bool InRange(BattleField field, double x, double z)
        {
            var dx = x - field.PlayerX;
            var dz = z - field.PlayerZ;
            return dx*dx + dz*dz < BattleField.DrawRange*BattleField.DrawRange;
        }

        /// <summary>Draws the gunsight, which is the one thing on screen that never moves.</summary>
        private void DrawReticle()
        {
            var cx = (int) Math.Round(_camera.CenterX);
            var cy = (int) Math.Round(_camera.HorizonRow);
            var arm = Math.Max(2, _camera.Width/18);
            var drop = Math.Max(1, (int) Math.Round(arm*_camera.FocalY/_camera.FocalX));
            var tick = Math.Max(1, arm/3);

            // Two uprights with the ticks turned inward, and deliberately NOTHING lying along the middle row: the
            // horizon runs through there, so a horizontal sight is drawn on top of a horizontal line and vanishes.
            // The target also stays uncovered, which a crosshair cannot manage.
            _sink(cx - arm, cy - drop, cx - arm, cy + drop, BattleInkEnum.Reticle);
            _sink(cx + arm, cy - drop, cx + arm, cy + drop, BattleInkEnum.Reticle);
            _sink(cx - arm, cy - drop, cx - arm + tick, cy - drop, BattleInkEnum.Reticle);
            _sink(cx - arm, cy + drop, cx - arm + tick, cy + drop, BattleInkEnum.Reticle);
            _sink(cx + arm - tick, cy - drop, cx + arm, cy - drop, BattleInkEnum.Reticle);
            _sink(cx + arm - tick, cy + drop, cx + arm, cy + drop, BattleInkEnum.Reticle);
        }

        /// <summary>
        ///     Draws the radar: a bezel, a sweep, and a mark for everything it can see.
        ///     <para>
        ///         <b>This is the other half of the game.</b> Everywhere else in this arcade the screen is a map and
        ///         the player can see the whole board; here the screen is a <i>view</i>, so a tank behind you is a
        ///         tank you do not know about. The radar is the only thing that says the world continues past the
        ///         edges of the picture, and combining the two is the skill the game is asking for.
        ///     </para>
        /// </summary>
        /// <param name="field">The world.</param>
        private void DrawRadar(BattleField field)
        {
            var radiusX = Math.Max(3.0, _camera.Width/16.0);

            // Squashed by the same ratio the projection is, so the radar is a circle on a pixel buffer and a circle
            // on a character grid rather than a circle on one and an ellipse on the other.
            var radiusY = radiusX*_camera.FocalY/_camera.FocalX;
            var centerX = _camera.CenterX;
            var centerY = radiusY + 1.0;

            const int segments = 20;
            var previousX = (int) Math.Round(centerX);
            var previousY = (int) Math.Round(centerY - radiusY);

            for (var i = 1; i <= segments; i++)
            {
                var angle = i*2.0*Math.PI/segments;
                var x = (int) Math.Round(centerX + Math.Sin(angle)*radiusX);
                var y = (int) Math.Round(centerY - Math.Cos(angle)*radiusY);
                _sink(previousX, previousY, x, y, BattleInkEnum.Radar);
                previousX = x;
                previousY = y;
            }

            _sink((int) Math.Round(centerX), (int) Math.Round(centerY),
                (int) Math.Round(centerX + Math.Sin(field.RadarSweep)*radiusX),
                (int) Math.Round(centerY - Math.Cos(field.RadarSweep)*radiusY), BattleInkEnum.Radar);

            foreach (var enemy in field.Enemies)
            {
                if (!enemy.Alive)
                    continue;

                // The camera's own rotation, so the radar cannot end up pointing somewhere the view is not.
                _camera.ToGround(enemy.X, enemy.Z, out var right, out var forward);
                if (right*right + forward*forward > RadarRange*RadarRange)
                    continue;

                // One cell, not a dash: on a radar five columns across, a three-wide blip is most of the sky.
                // The picture makes it visible by drawing it thick rather than long.
                var bx = (int) Math.Round(centerX + right/RadarRange*radiusX);
                var by = (int) Math.Round(centerY - forward/RadarRange*radiusY);
                _sink(bx, by, bx, by, BattleInkEnum.Blip);
            }
        }

        /// <summary>
        ///     Draws the broken viewport: spokes out from where the shot came in, and chords across them.
        /// </summary>
        /// <param name="bearing">Where the shot came from, relative to the player's nose.</param>
        private void DrawCrack(double bearing)
        {
            // The impact goes where the shot came from when that is on screen and is pinned near the edge when it is
            // not, so a shot in the back still breaks the glass on the correct side.
            var offset = Math.Clamp(Math.Tan(Math.Clamp(bearing, -1.3, 1.3)), -2.5, 2.5);
            var originX = _camera.CenterX + offset*_camera.FocalX*0.28;
            var originY = _camera.HorizonRow - _camera.Height*0.08;
            var squash = _camera.FocalY/_camera.FocalX;

            // A WEB, not a starburst: rings of chords joined by short radial pieces. Spokes alone, drawn long enough
            // to look like damage, reach every corner of the screen and the game disappears behind them - which is
            // what the first version of this did. Glass breaks locally, and so does this.
            for (var ring = 0; ring < _crackRings.Length; ring++)
            {
                var previousX = 0.0;
                var previousY = 0.0;

                for (var i = 0; i <= _crackSpokes.Length; i++)
                {
                    var spoke = i%_crackSpokes.Length;
                    var angle = _crackSpokes[spoke];

                    // Jittered by the spoke's own bearing so the rings are irregular but identical every frame.
                    var wobble = 1.0 + 0.28*Math.Sin(angle*5.0 + ring*2.3);
                    var reach = _crackRings[ring]*wobble*_camera.Width;
                    var x = originX + Math.Sin(angle)*reach;
                    var y = originY - Math.Cos(angle)*reach*squash;

                    var inner = ring == 0 ? 0.0 : _crackRings[ring - 1]*(1.0 + 0.28*Math.Sin(angle*5.0 +
                        (ring - 1)*2.3))*_camera.Width;
                    var ix = originX + Math.Sin(angle)*inner;
                    var iy = originY - Math.Cos(angle)*inner*squash;

                    if (i > 0)
                    {
                        _sink((int) Math.Round(previousX), (int) Math.Round(previousY), (int) Math.Round(x),
                            (int) Math.Round(y), BattleInkEnum.Crack);
                        _sink((int) Math.Round(ix), (int) Math.Round(iy), (int) Math.Round(x), (int) Math.Round(y),
                            BattleInkEnum.Crack);
                    }

                    previousX = x;
                    previousY = y;
                }
            }
        }
    }
}
