// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     A rectangle of characters, each with its own <see cref="TextStyle" />, that draws itself as text — and can
    ///     draw a <b>window onto itself</b> when the rectangle is larger than the screen it has to fit in.
    ///     <para>
    ///         <b>Why this is in the library.</b> Every screen here that is made of cells rather than of sentences had
    ///         written the same two loops for itself: fill a <c>char[,]</c>, then walk each row breaking it into runs
    ///         of like cells so a row of sixty identical characters costs one escape sequence instead of sixty.
    ///         Snake, the Missile Command character board and the chess text board each carried a copy, and so do five
    ///         of this library's own widgets. That is the same count that got <see cref="Graphics.AnsiText" />
    ///         published and <see cref="TextColumns" /> lifted out of the games project, and the same reason: the
    ///         interesting half of a copied loop is the part everyone gets subtly wrong.
    ///     </para>
    ///     <para>
    ///         <b>The part everyone gets wrong is what counts as "the same colour".</b> Runs break on the
    ///         <i>resolved escape sequence</i> and not on the <see cref="TextStyle" /> that produced it. Quantisation
    ///         happens downstream in <see cref="TextColor" />: a grayscale terminal has twenty-six answers and a
    ///         256-colour one has 256, so dozens of distinct styles arrive as the identical <c>ESC[38;5;n m</c>.
    ///         Comparing the styles looks right, is visually indistinguishable, and spends a reset plus an open
    ///         between two cells the terminal draws exactly the same. The cheap style compare stays in front of it as
    ///         a fast path, so a true-colour terminal pays nothing for the correctness.
    ///     </para>
    ///     <para>
    ///         <b>Rectangle in, rectangle out.</b> A rendered row always carries exactly
    ///         <c>columns * <see cref="CellWidth" /></c> visible characters, blanks included, and a viewport reaching
    ///         past the edge of the grid reads as <see cref="Blank" /> rather than stopping short. That is what makes
    ///         the output safe to put in a <see cref="Box" /> or a <see cref="TextColumns" /> column: the frame does
    ///         not breathe as the contents change, and a caller scrolling around a world larger than the screen gets
    ///         the same size picture at every position. It is the opposite choice from <see cref="TextColumns" />,
    ///         which trims its trailing blanks — there the blanks are padding, here they are cells, and a cell may be
    ///         carrying a background colour that trimming would throw away.
    ///     </para>
    ///     <para>
    ///         <b>Nothing is emitted for a grid nobody coloured.</b> Not an escape, not a reset — the same invariant
    ///         every other widget here keeps, and it goes further: an unstyled grid never asks
    ///         <see cref="AnsiConsole.DetectColorMode" /> what the terminal can do, because it has nothing to ask
    ///         about.
    ///     </para>
    ///     <para>
    ///         <b>Writes off the grid are dropped, not thrown</b> — the same bargain <see cref="PixelBuffer.Fill" />
    ///         and <see cref="PixelBuffer.DrawImage" /> strike. Callers plot from world coordinates, where being off
    ///         the edge is an ordinary event and not a mistake, and a bounds check at every call site is the code this
    ///         type exists to delete. Use <see cref="Contains" /> where a write really should have landed.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     var grid = new TextGrid(mazeWidth, mazeHeight) {CellWidth = 2};
    ///     grid.Set(player.X, player.Y, '@', ConsoleColor.Yellow);
    ///
    ///     // A window onto it, following the player and stopping at the edges.
    ///     var left = TextGrid.CenterOrigin(player.X, visibleColumns, grid.Width);
    ///     var top = TextGrid.CenterOrigin(player.Y, visibleRows, grid.Height);
    ///     string screen = grid.Render(left, top, visibleColumns, visibleRows);
    ///     </code>
    /// </example>
    public sealed class TextGrid
    {
        private readonly char[] _glyphs;
        private readonly TextStyle[] _styles;

        /// <summary>
        ///     Whether any cell has ever been given a style. Latched rather than recomputed, and never cleared: it
        ///     guards the whole colour path, so the only thing that matters is that it is never <i>false</i> while a
        ///     styled cell exists. A grid that was coloured and then blanked pays a resolve it does not need, which
        ///     costs one cached lookup; the other direction would silently drop colour.
        /// </summary>
        private bool _anyStyle;

        /// <summary>Initializes a new instance of the <see cref="TextGrid" /> class, filled with <see cref="Blank" />.</summary>
        /// <param name="width">How many cells across; must be positive.</param>
        /// <param name="height">How many cells down; must be positive.</param>
        public TextGrid(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "A grid needs at least one column.");

            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "A grid needs at least one row.");

            Width = width;
            Height = height;
            _glyphs = new char[width*height];
            _styles = new TextStyle[width*height];

            _glyphs.AsSpan().Fill(Blank);
        }

        /// <summary>How many cells across.</summary>
        public int Width { get; }

        /// <summary>How many cells down.</summary>
        public int Height { get; }

        /// <summary>
        ///     The character an empty cell holds, and what a viewport reaching past the edge of the grid reads as.
        ///     Set before anything is drawn: changing it later does not repaint cells already filled.
        /// </summary>
        public char Blank { get; set; } = ' ';

        /// <summary>
        ///     How many screen columns one cell is drawn as, by repeating its glyph. Defaults to one.
        ///     <para>
        ///         This exists because a character cell is about twice as tall as it is wide, so a grid meant to look
        ///         square — a maze, a board, a map — has to draw each cell two columns across, and every game here had
        ///         its own <c>CellWidth</c> constant and its own multiplication doing it. Origins and extents passed to
        ///         <see cref="Render(int,int,int,int)" /> stay in <i>cells</i>; only the output gets wider.
        ///     </para>
        /// </summary>
        public int CellWidth { get; set; } = 1;

        /// <summary>
        ///     Which colours to emit. <see cref="AnsiColorModeEnum.Auto" /> asks the environment once per render.
        ///     <para>
        ///         Pinned per grid rather than read from the environment so a test can assert on exact bytes without
        ///         setting <c>NO_COLOR</c> for the whole process, which would race every other test in the assembly.
        ///     </para>
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     The origin of a window of <paramref name="visible" /> cells centred on <paramref name="focus" />, held
        ///     inside a run of <paramref name="total" /> cells.
        ///     <para>
        ///         The camera, in one line of arithmetic. It clamps rather than wrapping, so walking toward an edge
        ///         stops the view at that edge and the focus drifts off centre instead of the world scrolling past its
        ///         own end — which is what every player expects and what nobody writes correctly the first time.
        ///         Returns zero when the window is at least as big as what it is looking at, so a caller does not have
        ///         to special-case the terminal being large enough to show everything.
        ///     </para>
        /// </summary>
        /// <param name="focus">The cell to centre on, along one axis.</param>
        /// <param name="visible">How many cells the window shows along that axis.</param>
        /// <param name="total">How many cells exist along that axis.</param>
        /// <returns>The first cell the window should show.</returns>
        public static int CenterOrigin(int focus, int visible, int total)
        {
            if (visible >= total || visible <= 0)
                return 0;

            var origin = focus - visible/2;

            if (origin < 0)
                return 0;

            var last = total - visible;
            return origin > last ? last : origin;
        }

        /// <summary>Whether a cell is on the grid.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <returns>True when both coordinates are in range.</returns>
        public bool Contains(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>Puts a character in a cell, leaving whatever style it already had. Off-grid writes are dropped.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <param name="glyph">The character to draw.</param>
        public void Set(int x, int y, char glyph)
        {
            if (Contains(x, y))
                _glyphs[y*Width + x] = glyph;
        }

        /// <summary>Puts a styled character in a cell. Off-grid writes are dropped.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <param name="glyph">The character to draw.</param>
        /// <param name="style">How to colour it; <see cref="TextStyle.None" /> emits nothing at all.</param>
        public void Set(int x, int y, char glyph, TextStyle style)
        {
            if (!Contains(x, y))
                return;

            var index = y*Width + x;
            _glyphs[index] = glyph;
            _styles[index] = style;
            _anyStyle |= !style.IsEmpty;
        }

        /// <summary>
        ///     Writes a string across a row, one character per cell, starting at a cell and clipping at both ends.
        ///     <para>
        ///         The member every caller writes for itself the first time it wants a word on a board — a score, a
        ///         label, a <c>READY!</c> over the middle of a maze. <b>It writes cells, not columns</b>, so with a
        ///         <see cref="CellWidth" /> above one each character comes out that many columns wide and the text is
        ///         spaced out rather than doubled up; a caller wanting text at its natural width draws it beside the
        ///         grid instead of into it.
        ///     </para>
        /// </summary>
        /// <param name="x">The cell to start at; the text may begin off the left edge and be clipped.</param>
        /// <param name="y">The row to write on; a row off the grid writes nothing.</param>
        /// <param name="text">What to write. Null or empty writes nothing.</param>
        /// <param name="style">How to colour it.</param>
        public void DrawText(int x, int y, string text, TextStyle style = default)
        {
            if (string.IsNullOrEmpty(text) || y < 0 || y >= Height)
                return;

            _anyStyle |= !style.IsEmpty;

            // Clipped by the loop range rather than per character, so a caption starting a long way off the left of
            // the grid costs its own length and not the distance it starts from.
            var from = x < 0 ? -x : 0;
            var to = Math.Min(text.Length, Width - x);

            for (var i = from; i < to; i++)
            {
                var index = y*Width + x + i;
                _glyphs[index] = text[i];
                _styles[index] = style;
            }
        }

        /// <summary>
        ///     Draws a straight line of cells between two points, both ends included, clipped to the grid rather
        ///     than refused by it.
        ///     <para>
        ///         The cell counterpart of <see cref="PixelBuffer.DrawLine(int,int,int,int,Rgba32)" />, and it keeps
        ///         that method's two hard-won properties because they are worth exactly as much here. <b>The loop
        ///         range is clipped rather than each cell</b>, so a line drawn between coordinates a million cells
        ///         apart costs the width of the grid and not a million iterations — which is not an exotic case at
        ///         all for anything projecting a three-dimensional scene, where a vertex just in front of the eye
        ///         lands enormously far off the side of the screen. And <b>the position at each step is a pure
        ///         function of the step index</b>, recomputed from the original endpoints every time, which is the
        ///         only thing that makes clipping the range sound: an incremental error accumulator would draw a
        ///         different line depending on where the loop was entered, so a shape would change as it crossed an
        ///         edge.
        ///     </para>
        ///     <para>
        ///         There is no thickness and no glyph-by-slope cleverness. A cell is already a chunky thing, and
        ///         which character best suggests a diagonal is a decision about the picture rather than about the
        ///         grid — a caller wanting <c>/</c> and <c>\</c> works its slope out and passes them in.
        ///     </para>
        /// </summary>
        /// <param name="x0">Start column, which may lie outside the grid.</param>
        /// <param name="y0">Start row, which may lie outside the grid.</param>
        /// <param name="x1">End column, which may lie outside the grid.</param>
        /// <param name="y1">End row, which may lie outside the grid.</param>
        /// <param name="glyph">The character to draw at every cell along the way.</param>
        /// <param name="style">How to colour it; <see cref="TextStyle.None" /> emits nothing at all.</param>
        public void DrawLine(int x0, int y0, int x1, int y1, char glyph, TextStyle style = default)
        {
            // Rejected in constant time when nothing can land, which is most of them in a scene that is mostly
            // behind the viewer.
            if (Math.Max(x0, x1) < 0 || Math.Min(x0, x1) >= Width)
                return;
            if (Math.Max(y0, y1) < 0 || Math.Min(y0, y1) >= Height)
                return;

            var dx = (long) x1 - x0;
            var dy = (long) y1 - y0;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            if (steps == 0)
            {
                // A line with no length is still a mark rather than nothing — the far end of a scene, where a whole
                // object has shrunk into one cell, relies on it.
                Set(x0, y0, glyph, style);
                return;
            }

            _anyStyle |= !style.IsEmpty;

            var xMajor = Math.Abs(dx) >= Math.Abs(dy);
            var majorStart = xMajor ? x0 : y0;
            var majorLimit = xMajor ? Width : Height;
            var forward = xMajor ? dx > 0 : dy > 0;

            // Which values of the step counter put the major axis on the grid.
            var first = forward ? -(long) majorStart : majorStart - (majorLimit - 1L);
            var last = forward ? majorLimit - 1L - majorStart : majorStart;
            if (first < 0) first = 0;
            if (last > steps) last = steps;

            for (var step = first; step <= last; step++)
            {
                // The major axis advances exactly one cell per step, so only the minor one is interpolated — and
                // rounding away from zero keeps the line symmetric whichever way round it is drawn.
                var minor = (int) Math.Round((double) (xMajor ? dy : dx)*step/steps, MidpointRounding.AwayFromZero);
                var x = xMajor ? x0 + (forward ? step : -step) : x0 + minor;
                var y = xMajor ? y0 + minor : y0 + (forward ? step : -step);

                // The range clip bounds the major axis only; the minor one still wanders off the sides.
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    continue;

                var index = (int) (y*Width + x);
                _glyphs[index] = glyph;
                _styles[index] = style;
            }
        }

        /// <summary>The character in a cell, or <see cref="Blank" /> when the cell is off the grid.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <returns>The character that cell holds.</returns>
        public char GlyphAt(int x, int y)
        {
            return Contains(x, y) ? _glyphs[y*Width + x] : Blank;
        }

        /// <summary>The style of a cell, or <see cref="TextStyle.None" /> when the cell is off the grid.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <returns>That cell's style.</returns>
        public TextStyle StyleAt(int x, int y)
        {
            return Contains(x, y) ? _styles[y*Width + x] : TextStyle.None;
        }

        /// <summary>Fills the whole grid with one styled character.</summary>
        /// <param name="glyph">The character to draw everywhere.</param>
        /// <param name="style">How to colour it.</param>
        public void Fill(char glyph, TextStyle style = default)
        {
            _glyphs.AsSpan().Fill(glyph);
            _styles.AsSpan().Fill(style);
            _anyStyle |= !style.IsEmpty;
        }

        /// <summary>
        ///     Fills a rectangle with one styled character, clipped to the grid rather than refused — so a rectangle
        ///     that hangs off an edge paints the part that fits, exactly as <see cref="PixelBuffer.Fill" /> does.
        /// </summary>
        /// <param name="x">Left column of the rectangle.</param>
        /// <param name="y">Top row of the rectangle.</param>
        /// <param name="width">How many columns wide.</param>
        /// <param name="height">How many rows tall.</param>
        /// <param name="glyph">The character to draw.</param>
        /// <param name="style">How to colour it.</param>
        public void Fill(int x, int y, int width, int height, char glyph, TextStyle style = default)
        {
            var left = Math.Max(0, x);
            var top = Math.Max(0, y);
            var right = Math.Min(Width, x + width);
            var bottom = Math.Min(Height, y + height);

            if (left >= right || top >= bottom)
                return;

            _anyStyle |= !style.IsEmpty;

            for (var row = top; row < bottom; row++)
            {
                var start = row*Width + left;
                var span = right - left;
                _glyphs.AsSpan(start, span).Fill(glyph);
                _styles.AsSpan(start, span).Fill(style);
            }
        }

        /// <summary>Blanks every cell and drops every style.</summary>
        public void Clear()
        {
            Fill(Blank);
        }

        /// <summary>Draws the whole grid.</summary>
        /// <returns>One line per row, joined with the platform newline and none trailing.</returns>
        public string Render()
        {
            return Render(0, 0, Width, Height);
        }

        /// <summary>
        ///     Draws a window onto the grid. Cells outside the grid read as <see cref="Blank" /> with no style, so a
        ///     window may hang off any edge and still comes back the size it asked for.
        /// </summary>
        /// <param name="originX">The first column to show, in cells.</param>
        /// <param name="originY">The first row to show, in cells.</param>
        /// <param name="columns">How many cells across to show; zero or less renders nothing.</param>
        /// <param name="rows">How many cells down to show; zero or less renders nothing.</param>
        /// <returns>
        ///     One line per row, each exactly <c>columns * <see cref="CellWidth" /></c> visible characters wide,
        ///     joined with the platform newline and none trailing.
        /// </returns>
        public string Render(int originX, int originY, int columns, int rows)
        {
            if (columns <= 0 || rows <= 0)
                return string.Empty;

            var cellWidth = Math.Max(1, CellWidth);

            // Resolved once for the whole render rather than per cell, and not at all for a grid nobody coloured -
            // which is the invariant that lets an untouched grid produce byte-for-byte what a plain char[,] loop
            // produced before this type existed, without ever consulting the environment.
            var mode = ColorMode;
            if (_anyStyle && mode == AnsiColorModeEnum.Auto)
                mode = AnsiConsole.DetectColorMode();

            var colored = _anyStyle && mode != AnsiColorModeEnum.None;

            var sb = new StringBuilder(rows*(columns*cellWidth + 2));

            for (var row = 0; row < rows; row++)
            {
                if (row > 0)
                    sb.AppendLine();

                var y = originY + row;

                // What is currently open, and the style that opened it. Two keys, answering different questions: the
                // style compare is the cheap skip, the sequence compare is the correct one. See the class docs.
                var openSequence = string.Empty;
                var openStyle = TextStyle.None;
                var haveStyle = false;

                for (var column = 0; column < columns; column++)
                {
                    var x = originX + column;
                    var inside = Contains(x, y);
                    var glyph = inside ? _glyphs[y*Width + x] : Blank;

                    if (colored)
                    {
                        var style = inside ? _styles[y*Width + x] : TextStyle.None;
                        if (!haveStyle || style != openStyle)
                        {
                            var open = style.OpenSequence(mode);
                            if (!string.Equals(open, openSequence, StringComparison.Ordinal))
                            {
                                if (openSequence.Length > 0)
                                    sb.Append(TextStyle.ResetSequence);

                                sb.Append(open);
                                openSequence = open;
                            }

                            openStyle = style;
                            haveStyle = true;
                        }
                    }

                    sb.Append(glyph, cellWidth);
                }

                // Closed at the end of every row, never left hanging across the newline. An escape has length but no
                // width, so a style that survived into the next row would colour cells nobody styled and would be
                // measured as columns by anything laying this out beside something else.
                if (openSequence.Length > 0)
                    sb.Append(TextStyle.ResetSequence);
            }

            return sb.ToString();
        }
    }
}
