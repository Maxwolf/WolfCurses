// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     Draws the same scene as characters, on a terminal that has no pixels to give.
    ///     <para>
    ///         <b>This is not a consolation prize; a vector game is the one thing a character grid is genuinely good
    ///         at.</b> Chess has to give up a piece of artwork and cards have to give up a rank in a corner, but a
    ///         wireframe was never a picture — it is a list of lines, and a line drawn in slashes and pipes is the
    ///         same line. What is lost is resolution, not information, which is why this view is playable rather
    ///         than merely legible.
    ///     </para>
    ///     <para>
    ///         It exists because of <see cref="TextGrid.DrawLine" />, which arrived in the library with this game:
    ///         the scene emits a few hundred segments a frame and something has to turn each into cells. The glyph
    ///         is chosen by slope here rather than in the library, because which character best suggests a diagonal
    ///         is a decision about a picture and not about a grid.
    ///     </para>
    ///     <para>
    ///         <b>Every kind of thing gets its own character as well as its own colour</b> — the rule this arcade
    ///         keeps arriving at — so a terminal resolved to no colour at all loses nothing but the mood. A tank is
    ///         a <c>#</c> whether or not it is allowed to be green.
    ///     </para>
    /// </summary>
    public sealed class BattlezoneText
    {
        private static readonly TextStyle _horizonStyle = new(ConsoleColor.DarkGreen);
        private static readonly TextStyle _sceneryStyle = new(ConsoleColor.Green);
        private static readonly TextStyle _enemyStyle = new(ConsoleColor.Green, bold: true);
        private static readonly TextStyle _saucerStyle = new(ConsoleColor.Cyan, bold: true);
        private static readonly TextStyle _shellStyle = new(ConsoleColor.Yellow, bold: true);
        private static readonly TextStyle _explosionStyle = new(ConsoleColor.White, bold: true);
        private static readonly TextStyle _reticleStyle = new(ConsoleColor.Green);
        private static readonly TextStyle _radarStyle = new(ConsoleColor.DarkGreen);
        private static readonly TextStyle _blipStyle = new(ConsoleColor.Red, bold: true);
        private static readonly TextStyle _crackStyle = new(ConsoleColor.White, bold: true);

        private readonly TextGrid _grid;
        private readonly BattleScene _scene;
        private readonly Action<int, int, int, int, BattleInkEnum> _draw;

        /// <summary>Initializes a new instance of the <see cref="BattlezoneText" /> class at a character size.</summary>
        /// <param name="columns">How many columns the view covers.</param>
        /// <param name="rows">How many rows the view covers.</param>
        public BattlezoneText(int columns, int rows)
        {
            Columns = Math.Max(20, columns);
            Rows = Math.Max(6, rows);

            _grid = new TextGrid(Columns, Rows);

            // Half the horizontal focal length vertically, because a character cell is about twice as tall as it is
            // wide. Leave them equal and every tank on the plain comes out squashed into an egg while every position
            // stays exactly right, which is a hard thing to see and a harder one to name.
            var focal = Columns*0.714;
            _scene = new BattleScene(new WireCamera(Columns, Rows, focal, focal/2.0));
            _draw = Draw;
        }

        /// <summary>How many columns the view covers.</summary>
        public int Columns { get; }

        /// <summary>How many rows the view covers.</summary>
        public int Rows { get; }

        /// <summary>The eye this view is drawn through, whose two focal lengths differ. See the constructor.</summary>
        public WireCamera Camera => _scene.Camera;

        /// <summary>Draws the whole scene as one block of text.</summary>
        /// <param name="field">What to draw.</param>
        /// <returns>One line per row, ready to drop into a rendered form.</returns>
        public string Render(BattleField field)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            // Load-bearing, unlike in the maze: a cell nobody repaints keeps what it had, and everything here moves
            // every single frame. Without this the plain fills up with the ghost of every line ever drawn.
            _grid.Clear();

            _scene.Draw(field, _draw);
            return _grid.Render();
        }

        /// <summary>
        ///     Which character best suggests a line of a given slope.
        /// </summary>
        /// <param name="x0">One end, across.</param>
        /// <param name="y0">One end, down.</param>
        /// <param name="x1">The other end, across.</param>
        /// <param name="y1">The other end, down.</param>
        /// <returns>The character to draw the line with.</returns>
        public static char SlopeGlyph(int x0, int y0, int x1, int y1)
        {
            var dx = Math.Abs((long) x1 - x0);
            var dy = Math.Abs((long) y1 - y0);

            if (dx >= dy*2)
                return '-';
            if (dy >= dx*2)
                return '|';

            // Rows count downward, so a line going right and down is a backslash. Getting this the wrong way round
            // produces a picture that is subtly, consistently wrong and still looks like a picture.
            return ((long) x1 - x0)*((long) y1 - y0) > 0 ? '\\' : '/';
        }

        /// <summary>Puts one line down as whatever characters its meaning calls for.</summary>
        /// <param name="x0">One end, across.</param>
        /// <param name="y0">One end, down.</param>
        /// <param name="x1">The other end, across.</param>
        /// <param name="y1">The other end, down.</param>
        /// <param name="ink">What the line is.</param>
        private void Draw(int x0, int y0, int x1, int y1, BattleInkEnum ink)
        {
            var style = ink switch
            {
                BattleInkEnum.Horizon => _horizonStyle,
                BattleInkEnum.Scenery => _sceneryStyle,
                BattleInkEnum.Enemy => _enemyStyle,
                BattleInkEnum.Saucer => _saucerStyle,
                BattleInkEnum.Shell => _shellStyle,
                BattleInkEnum.Explosion => _explosionStyle,
                BattleInkEnum.Reticle => _reticleStyle,
                BattleInkEnum.Radar => _radarStyle,
                BattleInkEnum.Blip => _blipStyle,
                _ => _crackStyle
            };

            // Solid marks for the things a player needs to pick out of a field of lines, slope glyphs for the things
            // whose SHAPE is the information. A tank drawn in slashes reads as more scenery.
            var glyph = ink switch
            {
                BattleInkEnum.Enemy => '#',
                BattleInkEnum.Saucer => 'o',
                BattleInkEnum.Shell => '*',
                BattleInkEnum.Explosion => '+',
                BattleInkEnum.Blip => '@',
                BattleInkEnum.Crack => 'X',

                // The gunsight gets its own horizontal glyph so that the part of it lying nearest the horizon is
                // still telling the player something rather than merging into it.
                BattleInkEnum.Reticle => SlopeGlyph(x0, y0, x1, y1) == '-' ? '=' : '|',
                _ => SlopeGlyph(x0, y0, x1, y1)
            };

            _grid.DrawLine(x0, y0, x1, y1, glyph, style);
        }
    }
}
