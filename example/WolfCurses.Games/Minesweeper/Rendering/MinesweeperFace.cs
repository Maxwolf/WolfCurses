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
    ///     between them, and a sunken field of tiles underneath.
    ///     <para>
    ///         <b>The whole look rests on one accident of history: the classic console palette <i>is</i> the Windows
    ///         95 palette.</b> <see cref="ConsoleColor.Gray" /> is <c>#C0C0C0</c>, which is the silver every dialog
    ///         and every button in that era was painted with; <see cref="ConsoleColor.White" /> is the highlight and
    ///         <see cref="ConsoleColor.DarkGray" /> is <c>#808080</c>, the shadow. So the bevels are not an
    ///         impression of the thing, they are the same three colours in the same three places — and because they
    ///         are named colours rather than exact RGB they still follow whatever theme the terminal is wearing
    ///         instead of fighting it.
    ///     </para>
    ///     <para>
    ///         <b>A tile is three columns wide and one row tall</b>: a light left edge (<c>▌</c>), its face, and a
    ///         dark right edge (<c>▐</c>) — which is the smallest thing a terminal can draw that the eye reads as
    ///         raised, and the dark edge of one tile against the light edge of the next is the seam a row of buttons
    ///         has always had. A revealed square drops the bevel and shows a flat face with a thin grid line, which
    ///         is the whole visual difference between "not yet touched" and "opened", exactly as it was. See
    ///         <see cref="TileWidth" /> for why it is not the two columns that would make a tile square.
    ///     </para>
    ///     <para>
    ///         <b>Nothing here needs colour to be playable.</b> Raised, flat, flagged and mined are four different
    ///         <i>glyphs</i> before they are four different colours, so a terminal resolved to
    ///         <see cref="AnsiColorModeEnum.None" /> loses the era and keeps the game — the rule this arcade keeps
    ///         arriving at, most recently in Battlezone.
    ///     </para>
    /// </summary>
    public sealed class MinesweeperFace
    {
        /// <summary>
        ///     How many columns one square is drawn across.
        ///     <para>
        ///         <b>Three, and the third one is the tile's face.</b> Two columns is the square-looking answer — a
        ///         cell is about twice as tall as it is wide — but it leaves a tile that is nothing but its own two
        ///         bevels, and a row of those reads as a picket fence rather than as a row of buttons. It is worst
        ///         on a terminal with no colour at all, where the highlight and the shadow are the same glyph and
        ///         the whole field turns into vertical stripes. A blank middle costs nine columns on a nine-wide
        ///         board and buys a tile that is unmistakably a button, lit or not.
        ///     </para>
        /// </summary>
        public const int TileWidth = 3;

        /// <summary>How many columns the panel's own raised edge and the field's sunken edge take, each side.</summary>
        private const int SideChrome = 2;

        /// <summary>Rows above the field: the raised top edge, the counter row, a gap, and the field's top edge.</summary>
        private const int RowsAboveField = 4;

        /// <summary>Rows below the field: the field's bottom edge and the panel's raised bottom edge.</summary>
        private const int RowsBelowField = 2;

        /// <summary>The most a Windows 95 counter could show, and it is three digits for the same reason.</summary>
        private const int CounterCap = 999;

        private static readonly TextStyle _face = new(ConsoleColor.Gray, ConsoleColor.Gray);
        private static readonly TextStyle _highlight = new(ConsoleColor.White, ConsoleColor.Gray);
        private static readonly TextStyle _shadow = new(ConsoleColor.DarkGray, ConsoleColor.Gray);
        private static readonly TextStyle _gridLine = new(ConsoleColor.DarkGray, ConsoleColor.Gray);
        private static readonly TextStyle _label = new(ConsoleColor.Black, ConsoleColor.Gray);
        private static readonly TextStyle _led = new(ConsoleColor.Red, ConsoleColor.Black, true);
        private static readonly TextStyle _smiley = new(ConsoleColor.Black, ConsoleColor.Yellow, true);
        private static readonly TextStyle _flag = new(ConsoleColor.Red, ConsoleColor.Gray, true);
        private static readonly TextStyle _mine = new(ConsoleColor.Black, ConsoleColor.Gray, true);
        private static readonly TextStyle _detonated = new(ConsoleColor.Black, ConsoleColor.Red, true);

        /// <summary>
        ///     The numbers, in the palette every version of this game has used since 1990. Index is the count, so
        ///     the first two entries are never read.
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
        public MinesweeperFace(int width, int height)
        {
            BoardWidth = Math.Max(1, width);
            BoardHeight = Math.Max(1, height);

            Columns = BoardWidth*TileWidth + SideChrome*2;
            Rows = BoardHeight + RowsAboveField + RowsBelowField;

            _grid = new TextGrid(Columns, Rows);
        }

        /// <summary>How many squares across.</summary>
        public int BoardWidth { get; }

        /// <summary>How many squares down.</summary>
        public int BoardHeight { get; }

        /// <summary>How many columns the whole panel takes.</summary>
        public int Columns { get; }

        /// <summary>How many rows the whole panel takes.</summary>
        public int Rows { get; }

        /// <summary>Which column of the panel the left-hand edge of square zero sits on.</summary>
        public int BoardOriginColumn => SideChrome;

        /// <summary>Which row of the panel square zero sits on.</summary>
        public int BoardOriginRow => RowsAboveField;

        /// <summary>Which row the counters and the face are drawn on.</summary>
        public int SmileyRow => 1;

        /// <summary>How many columns the face takes.</summary>
        public int SmileyWidth => 2;

        /// <summary>Which column the face starts at. Centred, so it moves with the board width.</summary>
        public int SmileyOriginColumn => (Columns - SmileyWidth)/2;

        /// <summary>
        ///     Draws the whole panel.
        /// </summary>
        /// <param name="field">The board to draw.</param>
        /// <param name="seconds">How long the player has been at it, for the right-hand counter.</param>
        /// <param name="showLabels">
        ///     Whether to put column letters and row numbers on the field's sunken edge. They are only there for
        ///     the sake of typing a square, so a terminal with a working mouse gets the clean panel instead — and
        ///     because they ride on chrome that is drawn either way, <b>turning them on moves nothing</b>, which is
        ///     what keeps a click landing on the same square in both.
        /// </param>
        /// <returns>The panel, one line per row.</returns>
        public string Render(Minefield field, int seconds, bool showLabels)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            _grid.Fill(' ', _face);

            DrawPanelEdge();
            DrawCounters(field, seconds);
            DrawFieldEdge(showLabels);
            DrawSquares(field);

            return _grid.Render();
        }

        /// <summary>The raised outer edge: light along the top and left, dark along the bottom and right.</summary>
        private void DrawPanelEdge()
        {
            for (var x = 0; x < Columns; x++)
            {
                _grid.Set(x, 0, '▀', _highlight);
                _grid.Set(x, Rows - 1, '▄', _shadow);
            }

            for (var y = 1; y < Rows - 1; y++)
            {
                _grid.Set(0, y, '▌', _highlight);
                _grid.Set(Columns - 1, y, '▐', _shadow);
            }
        }

        /// <summary>The two counters and the face between them.</summary>
        /// <param name="field">The board, for the mines-remaining count.</param>
        /// <param name="seconds">How long the player has been at it.</param>
        private void DrawCounters(Minefield field, int seconds)
        {
            const int row = 1;

            // Counted DOWN from the mine total as flags are planted, which is what the left counter has always
            // shown: it is "how many do you still think are out there", not "how many are left", and it happily
            // goes negative if the player over-flags.
            DrawLed(SideChrome, row, field.MineCount - field.FlagsPlaced);
            DrawLed(Columns - SideChrome - 3, row, seconds);

            // Alive, dead, or wearing the sunglasses it always wore for a cleared board.
            var mood = !field.IsOver ? ":)" : field.Won ? "B)" : ":(";

            for (var i = 0; i < mood.Length && i < SmileyWidth; i++)
                _grid.Set(SmileyOriginColumn + i, row, mood[i], _smiley);
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

        /// <summary>
        ///     The field's sunken edge — dark on the top and left, light on the bottom and right, which is the
        ///     outer bevel turned inside out and the whole of why the field reads as set into the panel.
        /// </summary>
        /// <param name="showLabels">Whether to spend that edge on coordinates instead.</param>
        private void DrawFieldEdge(bool showLabels)
        {
            var top = BoardOriginRow - 1;
            var bottom = BoardOriginRow + BoardHeight;
            var left = BoardOriginColumn - 1;
            var right = BoardOriginColumn + BoardWidth*TileWidth;

            for (var x = left; x <= right; x++)
            {
                _grid.Set(x, top, '▄', _shadow);
                _grid.Set(x, bottom, '▀', _highlight);
            }

            for (var y = BoardOriginRow; y < bottom; y++)
            {
                _grid.Set(left, y, '▐', _shadow);
                _grid.Set(right, y, '▌', _highlight);
            }

            if (!showLabels)
                return;

            // The coordinates ride ON the sunken edge rather than beside it, so switching them on does not move a
            // single square. Anything that changed the geometry would have to be undone in the click map as well,
            // and the two would drift apart the first time one of them was edited.
            for (var x = 0; x < BoardWidth; x++)
                _grid.Set(BoardOriginColumn + x*TileWidth + TileWidth/2, top, (char) ('A' + x), _label);

            for (var y = 0; y < BoardHeight && y < 9; y++)
                _grid.Set(left, BoardOriginRow + y, (char) ('1' + y), _label);
        }

        /// <summary>Every square, raised or opened.</summary>
        /// <param name="field">The board.</param>
        private void DrawSquares(Minefield field)
        {
            for (var y = 0; y < BoardHeight; y++)
            for (var x = 0; x < BoardWidth; x++)
                DrawSquare(field, x, y);
        }

        /// <summary>One square, in two columns.</summary>
        /// <param name="field">The board.</param>
        /// <param name="x">Which column of squares.</param>
        /// <param name="y">Which row of squares.</param>
        private void DrawSquare(Minefield field, int x, int y)
        {
            var left = BoardOriginColumn + x*TileWidth;
            var row = BoardOriginRow + y;

            var middle = left + TileWidth/2;

            if (!field.IsRevealed(x, y))
            {
                // Raised: a light edge down the left of the tile, a dark one down the right, and the face between
                // them. The dark edge of one tile sitting against the light edge of the next is the seam a row of
                // buttons has always had.
                _grid.Set(left, row, '▌', _highlight);
                _grid.Set(middle, row, ' ', _face);
                _grid.Set(left + TileWidth - 1, row, '▐', _shadow);

                if (field.IsFlagged(x, y))
                    _grid.Set(middle, row, '¶', _flag);

                return;
            }

            // Opened: flat, with a thin grid line where the tile's raised left edge used to be. Losing the bevel is
            // the whole visual difference between a square nobody has touched and one that is done with.
            _grid.Set(left, row, '▏', _gridLine);
            _grid.Set(middle, row, ' ', _face);
            _grid.Set(left + TileWidth - 1, row, ' ', _face);

            if (field.IsMine(x, y))
            {
                // A flag that turned out to be right keeps its flag, the one that was actually stepped on burns,
                // and everything else is just a mine. Losing opens EVERY mine at once, so a square being face up is
                // no help in telling them apart - which is why the board remembers where the last one went off.
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
