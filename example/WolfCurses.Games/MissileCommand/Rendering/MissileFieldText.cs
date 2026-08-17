// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Text;
using WolfCurses.Graphics;

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

            var cells = new char[rows, columns];
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
                cells[y, x] = Sky;

            // Painted back to front, so the things the player has to react to end up on top of the scenery.
            PaintGround(cells, field, columns, rows);
            PaintTrails(cells, field, columns, rows);
            PaintFireballs(cells, field, columns, rows);

            if (!field.IsOver)
                Plot(cells, columns, rows, aimX, aimY, CrosshairGlyph);

            return Compose(cells, columns, rows);
        }

        /// <summary>Turns a world position into a grid cell, or leaves it off the grid.</summary>
        private static (int X, int Y) ToCell(double worldX, double worldY, int columns, int rows)
        {
            return ((int) Math.Round(worldX/MissileField.Aspect*(columns - 1)),
                (int) Math.Round((1.0 - worldY)*(rows - 1)));
        }

        /// <summary>Writes one glyph at a world position, clipped.</summary>
        private static void Plot(char[,] cells, int columns, int rows, double worldX, double worldY, char glyph)
        {
            var (x, y) = ToCell(worldX, worldY, columns, rows);
            if (x >= 0 && x < columns && y >= 0 && y < rows)
                cells[y, x] = glyph;
        }

        private static void PaintGround(char[,] cells, MissileField field, int columns, int rows)
        {
            var (_, groundRow) = ToCell(0, MissileField.GroundY, columns, rows);
            for (var y = Math.Max(0, groundRow); y < rows; y++)
            for (var x = 0; x < columns; x++)
                cells[y, x] = GroundGlyph;

            for (var i = 0; i < MissileField.CityPositions.Count; i++)
            {
                Plot(cells, columns, rows, MissileField.CityPositions[i], MissileField.GroundY,
                    field.CitiesStanding[i] ? CityGlyph : RuinGlyph);
            }

            for (var i = 0; i < MissileField.SiloPositions.Count; i++)
            {
                Plot(cells, columns, rows, MissileField.SiloPositions[i], MissileField.GroundY,
                    field.SilosStanding[i] ? SiloGlyph : SiloRuinGlyph);
            }
        }

        private static void PaintTrails(char[,] cells, MissileField field, int columns, int rows)
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

                    if (x >= 0 && x < columns && y >= 0 && y < rows && cells[y, x] == Sky)
                        cells[y, x] = TrailGlyph;
                }

                if (toX >= 0 && toX < columns && toY >= 0 && toY < rows)
                {
                    cells[toY, toX] = missile.Kind switch
                    {
                        MissileKindEnum.Mirv => MirvGlyph,
                        MissileKindEnum.SmartBomb => SmartGlyph,
                        MissileKindEnum.Counter => CounterGlyph,
                        _ => IcbmGlyph
                    };
                }
            }
        }

        private static void PaintFireballs(char[,] cells, MissileField field, int columns, int rows)
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

                    cells[y, x] = distance < blast.Radius*0.55 ? FireCoreGlyph : FireEdgeGlyph;
                }
            }
        }

        /// <summary>
        ///     Turns the grid into text, styling a run of like cells at a time rather than a cell at a time — the
        ///     same discipline the library's own widgets follow, and the reason a row of sixty ground characters is
        ///     one escape sequence instead of sixty.
        /// </summary>
        private static string Compose(char[,] cells, int columns, int rows)
        {
            var sb = new StringBuilder();

            for (var y = 0; y < rows; y++)
            {
                if (y > 0)
                    sb.AppendLine();

                var runStart = 0;
                for (var x = 1; x <= columns; x++)
                {
                    if (x < columns && cells[y, x] == cells[y, runStart])
                        continue;

                    AppendRun(sb, cells[y, runStart], x - runStart);
                    runStart = x;
                }
            }

            return sb.ToString();
        }

        /// <summary>Writes one run of identical cells, styled once.</summary>
        private static void AppendRun(StringBuilder sb, char glyph, int count)
        {
            var text = new string(glyph, count);
            var style = glyph switch
            {
                GroundGlyph => new TextStyle(ConsoleColor.DarkYellow),
                CityGlyph => new TextStyle(ConsoleColor.Cyan),
                RuinGlyph or SiloRuinGlyph => new TextStyle(ConsoleColor.DarkGray),
                SiloGlyph => new TextStyle(ConsoleColor.White),
                IcbmGlyph => new TextStyle(ConsoleColor.Red),
                MirvGlyph => new TextStyle(ConsoleColor.DarkYellow),
                SmartGlyph => new TextStyle(ConsoleColor.Magenta),
                CounterGlyph => new TextStyle(ConsoleColor.Cyan),
                TrailGlyph => new TextStyle(ConsoleColor.DarkRed),
                FireCoreGlyph => new TextStyle(ConsoleColor.Yellow),
                FireEdgeGlyph => new TextStyle(ConsoleColor.DarkYellow),
                CrosshairGlyph => new TextStyle(ConsoleColor.White),
                _ => TextStyle.None
            };

            sb.Append(style.Apply(text));
        }
    }
}
