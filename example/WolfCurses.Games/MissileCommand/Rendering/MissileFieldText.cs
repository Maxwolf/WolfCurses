// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     Draws the field as characters, for a terminal that cannot show a picture — or cannot show one worth
    ///     looking at.
    ///     <para>
    ///         <b>This is not a consolation prize.</b> It is the only thing on screen when virtual-terminal
    ///         processing cannot be turned on, which is not a rare case and is not a graceful one: the presenter
    ///         blanks a true-pixel payload row on that path rather than printing its escape bytes as garbage, so a
    ///         game that only knew how to draw pixels would be an entirely empty screen with a prompt underneath it.
    ///     </para>
    ///     <para>
    ///         <b>Every kind of thing has its own glyph as well as its own colour</b>, which is the lesson
    ///         <see cref="Chess.ChessTextBoard" /> learned the same way: at <c>NO_COLOR</c>, or under a forced
    ///         grayscale, colour stops distinguishing anything and a board that leaned on it becomes unreadable
    ///         while still looking fine to whoever wrote it.
    ///     </para>
    ///     <para>
    ///         <b>It is drawn into a <see cref="TextGrid" />, and this screen is one of the three that type was
    ///         written to delete.</b> The grid's own class documentation names them: Snake, this board, and the
    ///         chess text board had each written the same <c>char[,]</c> plus a per-row loop breaking each row into
    ///         runs of like cells and styling every run once. Snake gave its copy up when the type shipped; this
    ///         one was simply missed.
    ///     </para>
    ///     <para>
    ///         <b>The output is byte-identical, measured rather than assumed</b> - sixty frames across six seeds,
    ///         escapes and all - so this is a deletion and not a change of picture. What it also closes is a latent
    ///         fault the local loop had and could not easily be given: it broke a run when the <i>glyph</i> changed,
    ///         where a run really belongs to the escape sequence. Ground, a MIRV and the edge of a fireball are all
    ///         dark yellow, so any two of them meeting would have cost a reset and a re-open for nothing. It
    ///         happens not to arise in the frames measured, which is exactly what makes it the kind of fault worth
    ///         removing by construction rather than by noticing.
    ///     </para>
    /// </summary>
    public static class MissileFieldText
    {
        private const char Sky = ' ';
        private const char GroundGlyph = '▄';
        private const char CityGlyph = '█';
        private const char RuinGlyph = '▖';
        private const char SiloGlyph = '▲';
        private const char SiloRuinGlyph = '▵';
        private const char TrailGlyph = '·';
        private const char IcbmGlyph = '*';
        private const char MirvGlyph = '◆';
        private const char SmartGlyph = '%';
        private const char CounterGlyph = '^';
        private const char FireEdgeGlyph = 'o';
        private const char FireCoreGlyph = '@';
        private const char CrosshairGlyph = '+';

        /// <summary>
        ///     What each thing is drawn in. Named here beside its glyph rather than worked back out of the glyph
        ///     afterwards, which is what the old run-coalescing loop had to do and is why two things sharing a
        ///     colour used to cost an escape sequence between them.
        /// </summary>
        private static readonly TextStyle _groundStyle = new(ConsoleColor.DarkYellow);

        private static readonly TextStyle _cityStyle = new(ConsoleColor.Cyan);
        private static readonly TextStyle _ruinStyle = new(ConsoleColor.DarkGray);
        private static readonly TextStyle _siloStyle = new(ConsoleColor.White);
        private static readonly TextStyle _icbmStyle = new(ConsoleColor.Red);
        private static readonly TextStyle _mirvStyle = new(ConsoleColor.DarkYellow);
        private static readonly TextStyle _smartStyle = new(ConsoleColor.Magenta);
        private static readonly TextStyle _counterStyle = new(ConsoleColor.Cyan);
        private static readonly TextStyle _trailStyle = new(ConsoleColor.DarkRed);
        private static readonly TextStyle _fireCoreStyle = new(ConsoleColor.Yellow);
        private static readonly TextStyle _fireEdgeStyle = new(ConsoleColor.DarkYellow);
        private static readonly TextStyle _crosshairStyle = new(ConsoleColor.White);

        /// <summary>
        ///     Draws the field into a grid of characters, coloured a run at a time.
        /// </summary>
        /// <param name="field">What to draw.</param>
        /// <param name="aimX">Where the player is aiming, in world units.</param>
        /// <param name="aimY">Where the player is aiming, in world units.</param>
        /// <param name="columns">How wide the grid is.</param>
        /// <param name="rows">How tall the grid is.</param>
        /// <returns>The field, one line per row, with no trailing newline.</returns>
        public static string Render(MissileField field, double aimX, double aimY, int columns, int rows)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            columns = Math.Max(20, columns);
            rows = Math.Max(6, rows);

            // A fresh grid is already all sky: TextGrid's blank is a space and an unset cell carries no style at
            // all, which is also what keeps the output byte-identical to the plain loop for a frame nobody
            // coloured. Built per call rather than kept, because this is static and the size follows the terminal.
            var grid = new TextGrid(columns, rows);

            // Painted back to front, so the things the player has to react to end up on top of the scenery.
            PaintGround(grid, field, columns, rows);
            PaintTrails(grid, field, columns, rows);
            PaintFireballs(grid, field, columns, rows);

            if (!field.IsOver)
                Plot(grid, columns, rows, aimX, aimY, CrosshairGlyph, _crosshairStyle);

            return grid.Render();
        }

        /// <summary>Turns a world position into a grid cell, or leaves it off the grid.</summary>
        private static (int X, int Y) ToCell(double worldX, double worldY, int columns, int rows)
        {
            return ((int) Math.Round(worldX/MissileField.Aspect*(columns - 1)),
                (int) Math.Round((1.0 - worldY)*(rows - 1)));
        }

        /// <summary>
        ///     Writes one glyph at a world position. The clipping the four-way bounds test used to do is the grid's
        ///     own contract now - a write that does not land is dropped.
        /// </summary>
        private static void Plot(TextGrid grid, int columns, int rows, double worldX, double worldY, char glyph,
            TextStyle style)
        {
            var (x, y) = ToCell(worldX, worldY, columns, rows);
            grid.Set(x, y, glyph, style);
        }

        private static void PaintGround(TextGrid grid, MissileField field, int columns, int rows)
        {
            var (_, groundRow) = ToCell(0, MissileField.GroundY, columns, rows);
            var top = Math.Max(0, groundRow);

            grid.Fill(0, top, columns, rows - top, GroundGlyph, _groundStyle);

            for (var i = 0; i < MissileField.CityPositions.Count; i++)
            {
                var standing = field.CitiesStanding[i];
                Plot(grid, columns, rows, MissileField.CityPositions[i], MissileField.GroundY,
                    standing ? CityGlyph : RuinGlyph, standing ? _cityStyle : _ruinStyle);
            }

            for (var i = 0; i < MissileField.SiloPositions.Count; i++)
            {
                var standing = field.SilosStanding[i];
                Plot(grid, columns, rows, MissileField.SiloPositions[i], MissileField.GroundY,
                    standing ? SiloGlyph : SiloRuinGlyph, standing ? _siloStyle : _ruinStyle);
            }
        }

        private static void PaintTrails(TextGrid grid, MissileField field, int columns, int rows)
        {
            foreach (var missile in field.Missiles)
            {
                if (!missile.Alive)
                    continue;

                // Walked in cell steps rather than drawn with a rasteriser: the grid is a few dozen cells across, so
                // sampling the flight at every whole cell of its length is both exact enough and shorter.
                var (fromX, fromY) = ToCell(missile.OriginX, missile.OriginY, columns, rows);
                var (toX, toY) = ToCell(missile.X, missile.Y, columns, rows);
                var steps = Math.Max(Math.Abs(toX - fromX), Math.Abs(toY - fromY));

                for (var step = 0; step <= steps; step++)
                {
                    var x = steps == 0 ? toX : fromX + (toX - fromX)*step/steps;
                    var y = steps == 0 ? toY : fromY + (toY - fromY)*step/steps;

                    // Only over sky, so a trail never paints out the ground, a city or an earlier warhead. Asked of
                    // the grid, which answers Blank for anywhere off it, so the bounds test comes for free.
                    if (grid.GlyphAt(x, y) == Sky)
                        grid.Set(x, y, TrailGlyph, _trailStyle);
                }

                var (glyph, style) = missile.Kind switch
                {
                    MissileKindEnum.Mirv => (MirvGlyph, _mirvStyle),
                    MissileKindEnum.SmartBomb => (SmartGlyph, _smartStyle),
                    MissileKindEnum.Counter => (CounterGlyph, _counterStyle),
                    _ => (IcbmGlyph, _icbmStyle)
                };

                grid.Set(toX, toY, glyph, style);
            }
        }

        private static void PaintFireballs(TextGrid grid, MissileField field, int columns, int rows)
        {
            foreach (var blast in field.Blasts)
            {
                if (blast.Radius <= 0.0)
                    continue;

                // Tested in WORLD units, not in cells. A character cell is nowhere near square, so a radius measured
                // in cells would draw an oval where the rules see a circle - and the player would learn to aim at
                // something that is not what the game is testing.
                var (left, top) = ToCell(blast.X - blast.Radius, blast.Y + blast.Radius, columns, rows);
                var (right, bottom) = ToCell(blast.X + blast.Radius, blast.Y - blast.Radius, columns, rows);

                for (var y = Math.Max(0, top); y <= Math.Min(rows - 1, bottom); y++)
                for (var x = Math.Max(0, left); x <= Math.Min(columns - 1, right); x++)
                {
                    var worldX = (double) x/(columns - 1)*MissileField.Aspect;
                    var worldY = 1.0 - (double) y/(rows - 1);
                    var dx = worldX - blast.X;
                    var dy = worldY - blast.Y;
                    var distance = Math.Sqrt(dx*dx + dy*dy);

                    if (distance > blast.Radius)
                        continue;

                    var core = distance < blast.Radius*0.55;
                    grid.Set(x, y, core ? FireCoreGlyph : FireEdgeGlyph, core ? _fireCoreStyle : _fireEdgeStyle);
                }
            }
        }
    }
}
