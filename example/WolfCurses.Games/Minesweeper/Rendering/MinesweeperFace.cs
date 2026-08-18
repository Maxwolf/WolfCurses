// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Games.Minesweeper
{
    /// <summary>
    ///     The board drawn the way Windows 95 drew it: a raised silver panel, two red LED counters and a smiley
    ///     between them, and a field of tiles underneath — each one <b>a box with four sides</b>.
    ///     <para>
    ///         <b>The whole look rests on one accident of history: the classic console palette <i>is</i> the Windows
    ///         95 palette.</b> <see cref="ConsoleColor.Gray" /> is <c>#C0C0C0</c>, the silver every dialog and every
    ///         button of that era was painted with; <see cref="ConsoleColor.White" /> is the highlight and
    ///         <see cref="ConsoleColor.DarkGray" /> is <c>#808080</c>, the shadow.
    ///     </para>
    ///     <para>
    ///         <b>A tile takes two rows, and it took four wrong answers to accept that.</b> Every one of them tried
    ///         to fit a box into a single row by drawing its edges at the edges of cells — half-cell bars, which
    ///         stacked into rails; quadrants, which read as bands; hairlines in white, which vanish against silver;
    ///         hairlines in shadow, which finally looked like a grid but left one corner of every tile open. That
    ///         last one is not a bug to be found, it is arithmetic: <b>a box needs a line above its content and a
    ///         line below it, which is three vertical positions, and a character cell offers one.</b> Two rows per
    ///         tile is the floor, and it is the price of the thing actually being a box.
    ///     </para>
    ///     <para>
    ///         With two rows it becomes an ordinary lattice with <i>shared</i> edges — one line between neighbours
    ///         rather than two — drawn with <see cref="BoxDrawing.Junction" />, which is in the library for exactly
    ///         this and picks each corner, tee and cross from the four directions that meet there. That is what
    ///         closes every corner: a junction glyph is a thing a font has, where a corner made of two hairlines is
    ///         a thing a cell cannot hold.
    ///     </para>
    ///     <para>
    ///         <b>A line is drawn only where a tile beside it is still closed</b>, which is what makes a cleared
    ///         region read as one flat expanse rather than as more squares — the same as the original, and the same
    ///         rule Pac-Man's maze walls follow. It also removes the one ambiguity that would otherwise matter: a
    ///         blank opened tile is never next to a closed one (a square with no neighbouring mines opens all of its
    ///         neighbours), so "boxed" and "closed" mean the same thing on screen.
    ///     </para>
    /// </summary>
    public sealed class MinesweeperFace
    {
        /// <summary>How many columns a tile's interior takes, between its own two side lines.</summary>
        public const int InteriorWidth = 3;

        /// <summary>
        ///     How many columns one tile advances: its interior plus the line it shares with the next one.
        ///     <para>
        ///         Four across and two down makes a box that is square on screen, a character cell being about twice
        ///         as tall as it is wide.
        ///     </para>
        /// </summary>
        public const int TileWidth = InteriorWidth + 1;

        /// <summary>How many rows one tile advances: its interior row plus the line it shares with the next one.</summary>
        public const int TileHeight = 2;

        /// <summary>How many columns the coordinate gutter takes when it is drawn: two digits and a space.</summary>
        public const int LabelWidth = 3;

        /// <summary>The widest board that can still name each of its columns with a single letter.</summary>
        public const int WidestLabelledBoard = 26;

        /// <summary>How many columns the panel's own raised edge takes, each side.</summary>
        private const int SideChrome = 1;

        /// <summary>Rows above the field: the panel's raised top edge, the counter row, and a gap.</summary>
        private const int RowsAboveField = 3;

        /// <summary>Rows below the field: the panel's raised bottom edge.</summary>
        private const int RowsBelowField = 1;

        /// <summary>The most a Windows 95 counter could show, and it is three digits for the same reason.</summary>
        private const int CounterCap = 999;

        private static readonly TextStyle _face = new(ConsoleColor.Gray, ConsoleColor.Gray);
        private static readonly TextStyle _highlight = new(ConsoleColor.White, ConsoleColor.Gray);
        private static readonly TextStyle _shadow = new(ConsoleColor.DarkGray, ConsoleColor.Gray);
        private static readonly TextStyle _line = new(ConsoleColor.DarkGray, ConsoleColor.Gray);
        private static readonly TextStyle _label = new(ConsoleColor.Gray);
        private static readonly TextStyle _led = new(ConsoleColor.Red, ConsoleColor.Black, true);
        private static readonly TextStyle _smiley = new(ConsoleColor.Black, ConsoleColor.Yellow, true);
        private static readonly TextStyle _flag = new(ConsoleColor.Red, ConsoleColor.Gray, true);
        private static readonly TextStyle _mine = new(ConsoleColor.Black, ConsoleColor.Gray, true);
        private static readonly TextStyle _detonated = new(ConsoleColor.Black, ConsoleColor.Red, true);

        /// <summary>
        ///     The numbers, in the palette every version of this game has used since 1990. Index is the count, so
        ///     the first entry is never read.
        /// </summary>
        private static readonly ConsoleColor[] _numbers =
        {
            ConsoleColor.Gray, ConsoleColor.Blue, ConsoleColor.DarkGreen, ConsoleColor.Red, ConsoleColor.DarkBlue,
            ConsoleColor.DarkRed, ConsoleColor.DarkCyan, ConsoleColor.Black, ConsoleColor.DarkGray
        };

        private readonly TextGrid _grid;

        /// <summary>Initializes a new instance of the <see cref="MinesweeperFace" /> class for a board size.</summary>
        /// <param name="width">How many squares across.</param>
        /// <param name="height">How many squares down.</param>
        /// <param name="showLabels">
        ///     Whether to draw column letters above the panel and row numbers down its left. They exist only so a
        ///     square can be typed, so a terminal with a working mouse gets the panel without them. <b>It is a
        ///     construction-time choice</b>, because the gutter changes where the field sits — and since the drawing
        ///     and the click map both take that from <see cref="BoardOriginColumn" />, they move together.
        /// </param>
        public MinesweeperFace(int width, int height, bool showLabels = false)
        {
            BoardWidth = Math.Max(1, width);
            BoardHeight = Math.Max(1, height);
            ShowLabels = showLabels;

            Columns = ColumnsFor(BoardWidth, showLabels);
            Rows = RowsFor(BoardHeight, showLabels);

            _grid = new TextGrid(Columns, Rows);
        }

        /// <summary>How many squares across.</summary>
        public int BoardWidth { get; }

        /// <summary>How many squares down.</summary>
        public int BoardHeight { get; }

        /// <summary>Whether the coordinate gutter is drawn.</summary>
        public bool ShowLabels { get; }

        /// <summary>How many columns the whole thing takes, gutter included.</summary>
        public int Columns { get; }

        /// <summary>How many rows the whole thing takes, gutter included.</summary>
        public int Rows { get; }

        /// <summary>Which column the field's own left-hand line is drawn on.</summary>
        public int BoardOriginColumn => Gutter + SideChrome;

        /// <summary>Which row the field's own top line is drawn on.</summary>
        public int BoardOriginRow => LabelRow + RowsAboveField;

        /// <summary>Which row the counters and the face are drawn on.</summary>
        public int SmileyRow => LabelRow + 1;

        /// <summary>How many columns the face takes.</summary>
        public int SmileyWidth => 2;

        /// <summary>Which column the face starts at. Centred on the panel, so it moves with the board width.</summary>
        public int SmileyOriginColumn => Gutter + (Columns - Gutter - SmileyWidth)/2;

        /// <summary>Which column the left-hand counter's first digit sits on.</summary>
        public int CounterOriginColumn => Gutter + SideChrome;

        /// <summary>How many columns the coordinate gutter actually takes.</summary>
        private int Gutter => ShowLabels ? LabelWidth : 0;

        /// <summary>How many rows the column letters actually take.</summary>
        private int LabelRow => ShowLabels ? 1 : 0;

        /// <summary>How wide a panel comes out for a board of a given size.</summary>
        /// <param name="width">How many squares across.</param>
        /// <param name="showLabels">Whether the coordinate gutter is drawn.</param>
        /// <returns>The width in columns.</returns>
        public static int ColumnsFor(int width, bool showLabels)
        {
            return (showLabels ? LabelWidth : 0) + SideChrome*2 + Math.Max(1, width)*TileWidth + 1;
        }

        /// <summary>How tall a panel comes out for a board of a given size.</summary>
        /// <param name="height">How many squares down.</param>
        /// <param name="showLabels">Whether the column letters are drawn.</param>
        /// <returns>The height in rows.</returns>
        public static int RowsFor(int height, bool showLabels)
        {
            return (showLabels ? 1 : 0) + RowsAboveField + Math.Max(1, height)*TileHeight + 1 + RowsBelowField;
        }

        /// <summary>
        ///     Draws the whole panel.
        /// </summary>
        /// <param name="field">The board to draw.</param>
        /// <param name="seconds">How long the player has been at it, for the right-hand counter.</param>
        /// <returns>The panel, one line per row.</returns>
        public string Render(Minefield field, int seconds)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            // Unstyled, so the gutter outside the panel keeps the terminal's own background rather than putting a
            // silver rectangle around nothing.
            _grid.Fill(' ', TextStyle.None);

            DrawPanelEdge();
            DrawCounters(field, seconds);
            DrawLabels();
            DrawLattice(field);
            DrawTiles(field);

            return _grid.Render();
        }

        /// <summary>The raised outer edge: light along the top and left, dark along the bottom and right.</summary>
        private void DrawPanelEdge()
        {
            var left = Gutter;
            var top = LabelRow;

            _grid.Fill(left, top, Columns - left, Rows - top, ' ', _face);

            for (var x = left; x < Columns; x++)
            {
                _grid.Set(x, top, '▀', _highlight);
                _grid.Set(x, Rows - 1, '▄', _shadow);
            }

            for (var y = top + 1; y < Rows - 1; y++)
            {
                _grid.Set(left, y, '▌', _highlight);
                _grid.Set(Columns - 1, y, '▐', _shadow);
            }
        }

        /// <summary>The two counters and the face between them.</summary>
        /// <param name="field">The board, for the mines-remaining count.</param>
        /// <param name="seconds">How long the player has been at it.</param>
        private void DrawCounters(Minefield field, int seconds)
        {
            // Counted DOWN from the mine total as flags are planted, which is what the left counter has always
            // shown: it is "how many do you still think are out there", and it happily goes negative.
            DrawLed(CounterOriginColumn, SmileyRow, field.MineCount - field.FlagsPlaced);
            DrawLed(Columns - SideChrome - 3, SmileyRow, seconds);

            // Alive, dead, or wearing the sunglasses it always wore for a cleared board.
            var mood = !field.IsOver ? ":)" : field.Won ? "B)" : ":(";

            for (var i = 0; i < mood.Length && i < SmileyWidth; i++)
                _grid.Set(SmileyOriginColumn + i, SmileyRow, mood[i], _smiley);
        }

        /// <summary>One three-digit red readout on black, clamped the way the originals were.</summary>
        /// <param name="x">Where the first digit goes.</param>
        /// <param name="y">Which row.</param>
        /// <param name="value">What to show.</param>
        private void DrawLed(int x, int y, int value)
        {
            var shown = Math.Clamp(value, -99, CounterCap);
            var text = shown < 0
                ? "-" + Math.Abs(shown).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')
                : shown.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');

            for (var i = 0; i < 3; i++)
                _grid.Set(x + i, y, text[i], _led);
        }

        /// <summary>The column letters and row numbers, outside the panel so they are plainly not part of it.</summary>
        private void DrawLabels()
        {
            if (!ShowLabels)
                return;

            for (var x = 0; x < BoardWidth && x < WidestLabelledBoard; x++)
                _grid.Set(InteriorColumn(x), 0, (char) ('A' + x), _label);

            for (var y = 0; y < BoardHeight; y++)
            {
                var number = (y + 1).ToString(CultureInfo.InvariantCulture).PadLeft(2);

                for (var i = 0; i < number.Length && i < LabelWidth; i++)
                    _grid.Set(i, InteriorRow(y), number[i], _label);
            }
        }

        /// <summary>
        ///     Every line of the lattice, with each junction asked for rather than guessed.
        /// </summary>
        /// <param name="field">The board.</param>
        private void DrawLattice(Minefield field)
        {
            for (var lineY = 0; lineY <= BoardHeight; lineY++)
            {
                var row = BoardOriginRow + lineY*TileHeight;

                for (var lineX = 0; lineX <= BoardWidth; lineX++)
                {
                    var column = BoardOriginColumn + lineX*TileWidth;

                    // Which of the four directions actually have a line running into this point. That is the whole
                    // question BoxDrawing.Junction exists to answer, and the reason a network of lines cannot reuse
                    // Box: a rectangle knows its six glyphs from position, this needs sixteen chosen per cell.
                    var up = lineY > 0 && SideDrawn(field, lineX, lineY - 1);
                    var down = lineY < BoardHeight && SideDrawn(field, lineX, lineY);
                    var left = lineX > 0 && TopDrawn(field, lineX - 1, lineY);
                    var right = lineX < BoardWidth && TopDrawn(field, lineX, lineY);

                    if (up || down || left || right)
                        _grid.Set(column, row, BoxDrawing.Junction(up, down, left, right), _line);

                    if (right)
                    {
                        for (var i = 1; i <= InteriorWidth; i++)
                            _grid.Set(column + i, row, '─', _line);
                    }

                    if (down)
                        _grid.Set(column, row + 1, '│', _line);
                }
            }
        }

        /// <summary>
        ///     Whether the line above tile row <paramref name="lineY" /> is drawn over tile column
        ///     <paramref name="x" />.
        /// </summary>
        /// <param name="field">The board.</param>
        /// <param name="x">Which column of tiles.</param>
        /// <param name="lineY">Which horizontal line, counting from the top of the field.</param>
        /// <returns>True when it is drawn.</returns>
        private bool TopDrawn(Minefield field, int x, int lineY)
        {
            return Closed(field, x, lineY - 1) || Closed(field, x, lineY);
        }

        /// <summary>
        ///     Whether the line left of tile column <paramref name="lineX" /> is drawn beside tile row
        ///     <paramref name="y" />.
        /// </summary>
        /// <param name="field">The board.</param>
        /// <param name="lineX">Which vertical line, counting from the left of the field.</param>
        /// <param name="y">Which row of tiles.</param>
        /// <returns>True when it is drawn.</returns>
        private bool SideDrawn(Minefield field, int lineX, int y)
        {
            return Closed(field, lineX - 1, y) || Closed(field, lineX, y);
        }

        /// <summary>
        ///     Whether a square is still closed. <b>Anything off the board counts as closed</b>, which is what draws
        ///     the field's own outer frame without a special case for it.
        /// </summary>
        /// <param name="field">The board.</param>
        /// <param name="x">Which column of tiles.</param>
        /// <param name="y">Which row of tiles.</param>
        /// <returns>True when a line beside it should be drawn.</returns>
        private bool Closed(Minefield field, int x, int y)
        {
            if (x < 0 || y < 0 || x >= BoardWidth || y >= BoardHeight)
                return true;

            return !field.IsRevealed(x, y);
        }

        /// <summary>Which column a tile's content sits in.</summary>
        /// <param name="x">Which column of tiles.</param>
        /// <returns>The column.</returns>
        public int InteriorColumn(int x)
        {
            return BoardOriginColumn + x*TileWidth + InteriorWidth/2 + 1;
        }

        /// <summary>Which row a tile's content sits in.</summary>
        /// <param name="y">Which row of tiles.</param>
        /// <returns>The row.</returns>
        public int InteriorRow(int y)
        {
            return BoardOriginRow + y*TileHeight + 1;
        }

        /// <summary>Whatever each tile has to show inside its box.</summary>
        /// <param name="field">The board.</param>
        private void DrawTiles(Minefield field)
        {
            for (var y = 0; y < BoardHeight; y++)
            for (var x = 0; x < BoardWidth; x++)
                DrawTile(field, x, y);
        }

        /// <summary>One tile's interior.</summary>
        /// <param name="field">The board.</param>
        /// <param name="x">Which column of tiles.</param>
        /// <param name="y">Which row of tiles.</param>
        private void DrawTile(Minefield field, int x, int y)
        {
            var row = InteriorRow(y);
            var middle = InteriorColumn(x);

            if (!field.IsRevealed(x, y))
            {
                // A hairline highlight just inside the box's left edge, which is where Windows put it. It is also
                // the one mark that says "closed" without depending on the lines around it, so the board stays
                // readable for anything reading it a cell at a time.
                _grid.Set(middle - 1, row, '▏', _highlight);

                if (field.IsFlagged(x, y))
                    _grid.Set(middle, row, '¶', _flag);

                return;
            }

            if (field.IsMine(x, y))
            {
                // A flag that turned out to be right keeps its flag, the one that was actually stepped on burns, and
                // everything else is just a mine. Losing opens EVERY mine at once, so a square being face up is no
                // help in telling them apart — which is why the board remembers where the last one went off.
                if (field.IsFlagged(x, y))
                    _grid.Set(middle, row, '¶', _flag);
                else if (x == field.HitX && y == field.HitY)
                    _grid.Set(middle, row, '*', _detonated);
                else
                    _grid.Set(middle, row, '*', _mine);

                return;
            }

            var adjacent = field.AdjacentMines(x, y);
            if (adjacent <= 0)
                return;

            var color = _numbers[Math.Clamp(adjacent, 0, _numbers.Length - 1)];
            _grid.Set(middle, row, (char) ('0' + adjacent), new TextStyle(color, ConsoleColor.Gray, true));
        }
    }
}
