// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Tetris
{
    /// <summary>
    ///     Tetris. Steered like <see cref="Snake.SnakeDialog" /> and paced the same way, but here to make a point
    ///     about <b>layout</b>: it is the first screen in this arcade that puts two things beside each other rather
    ///     than one under the other.
    ///     <para>
    ///         That turns out to be the interesting part. A row of the well is twenty visible columns wrapped in
    ///         several hundred bytes of color escapes, so lining a panel up against it needs a width measured in
    ///         columns and not in characters. This game once carried its own <c>SideBySide</c> helper to do that;
    ///         the library owns it now as <see cref="TextColumns" />, so there is nothing left in this file that is
    ///         not either the game or a call into the library.
    ///     </para>
    ///     <para>
    ///         The well is <b>sixteen rows rather than the traditional twenty</b>, so the whole screen — status line,
    ///         well, message and prompt — fits an eighty by twenty-four terminal without the presenter clipping the
    ///         bottom off. It plays the same; it is simply a shorter well.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class TetrisDialog : Form<GamesWindowInfo>
    {
        /// <summary>Well width in cells. Ten, as it has always been.</summary>
        private const int WellWidth = 10;

        /// <summary>Well height in cells; short enough that the whole screen fits a standard terminal.</summary>
        private const int WellHeight = 16;

        /// <summary>How wide one cell is drawn. Two, because a character cell is about twice as tall as it is wide.</summary>
        /// <summary>
        ///     How wide the score panel's lines are, and how much of that the figure gets. Said once, because
        ///     the alternative is the arithmetic that used to be here: a label plus a subtraction from a total
        ///     that appeared nowhere. That version held only while every figure was short enough for the pad to
        ///     bite, and a seven-figure score would have widened the line, pushed past the box's MinimumWidth
        ///     and shifted the whole right-hand column of a frame this app measures to fit eighty by
        ///     twenty-four.
        /// </summary>
        private const int PanelColumns = 12;

        private const int FigureColumns = 7;

        /// <summary>How big a box the preview is drawn in: the largest any piece needs, so it never changes.</summary>
        private const int PreviewCells = 4;

        /// <summary>
        ///     Paces gravity on real elapsed time. The interval is passed per step rather than set on the timer,
        ///     because it shortens with every level.
        /// </summary>
        private readonly IntervalTimer _fall = new(TimeSpan.FromMilliseconds(800));

        /// <summary>The well's frame.</summary>
        /// <summary>
        ///     The well itself, as cells. <c>CellWidth</c> is two because a character cell is about twice as
        ///     tall as it is wide, so a board meant to look square draws each cell two columns across.
        ///     <para>
        ///         This was the fourth copy of the <c>char[,]</c> plus per-row run-coalescing loop that put
        ///         <see cref="TextGrid" /> in the library, and the last one left in this app. It is also the
        ///         copy that broke a run when the <i>piece</i> changed rather than when the drawn escape did,
        ///         which is the trap the grid closes: two colours can resolve to the same sequence, and
        ///         comparing the source instead spends a reset and a re-open between two cells the terminal
        ///         draws identically.
        ///     </para>
        ///     <para>
        ///         <b>The clear is load-bearing here</b>, as it is in Snake and for the same reason: the
        ///         falling piece and its ghost vacate cells every step, and a cell nobody repaints keeps
        ///         whatever it had.
        ///     </para>
        /// </summary>
        private readonly TextGrid _playfield = new(WellWidth, WellHeight) {CellWidth = 2};

        /// <summary>
        ///     The next piece, always drawn into the largest box any piece needs.
        ///     <para>
        ///         <b>A fixed four by four is the whole point</b>, and the comment this replaces claimed it
        ///         without it being true. Pieces come in 2x2, 3x3 and 4x4 boxes, so drawing each piece's own box
        ///         made the Next panel four, five or six rows tall - and since a seven-bag deals every piece
        ///         every seven pieces, the Stats panel underneath it moved up and down several times a minute
        ///         for the whole game. The grid is the shape the panel is supposed to be, rather than padding
        ///         arithmetic bolted on afterwards.
        ///     </para>
        /// </summary>
        private readonly TextGrid _preview = new(PreviewCells, PreviewCells) {CellWidth = 2};

        private readonly Box _frame = new() {Title = "Tetris", Padding = 0};

        /// <summary>The preview panel, sized so it and the stats panel line up as one column.</summary>
        private readonly Box _nextFrame = new() {Title = "Next", Padding = 0, MinimumWidth = 12};

        /// <summary>The score panel.</summary>
        private readonly Box _statsFrame = new() {Title = "Stats", Padding = 0, MinimumWidth = 12};

        private string _message;
        private string _rendered;
        private TetrisWell _well;

        /// <summary>Initializes a new instance of the <see cref="TetrisDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public TetrisDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Keeps typed characters out of the input buffer, which matters more here than in the snake: the hard
        ///     drop is bound to SPACE, and a space is a printable character. Left at the default it would land in the
        ///     buffer as well as reaching this form, and every slam of the space bar would widen the echoed prompt at
        ///     the bottom of the screen. ENTER still arrives at <see cref="OnInputBufferReturned" /> regardless, being
        ///     buffer control rather than buffer content.
        /// </summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText =
                "Arrows/WASD to move, UP or W to turn, SPACE to drop, R for a new well, ENTER or ESC to quit";

            // Also starts the timer, and replaces the OnFormActivate override this form used to hand-write so that
            // time spent under a dialog was not owed back as a free row of gravity.
            RestartOnActivate(_fall);
            StartNewWell();
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (key == ConsoleKey.R)
            {
                StartNewWell();
                return;
            }

            if (_well.IsOver)
                return;

            switch (key)
            {
                case ConsoleKey.LeftArrow or ConsoleKey.A:
                    _well.Move(-1);
                    break;
                case ConsoleKey.RightArrow or ConsoleKey.D:
                    _well.Move(1);
                    break;
                case ConsoleKey.UpArrow or ConsoleKey.W:
                    _well.Rotate(true);
                    break;
                case ConsoleKey.Z:
                    _well.Rotate(false);
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.S:
                    // A soft drop restarts the fall clock, or the piece the player just nudged down would be taken
                    // down again by gravity a few milliseconds later and drop two rows for one keystroke.
                    _well.Drop(true);
                    _fall.Restart();
                    break;
                case ConsoleKey.Spacebar:
                    _well.HardDrop();
                    _fall.Restart();
                    break;
                default:
                    return;
            }

            Refresh();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (_well.IsOver || !_fall.TryConsume(_well.FallInterval))
                return;

            _well.Drop(false);
            Refresh();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick, so it hands back a string that is already built.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>Deals a fresh well.</summary>
        private void StartNewWell()
        {
            _well = new TetrisWell(WellWidth, WellHeight, SimUnit.Random);
            _message = "Fill a row to clear it. Four at once is worth the most.";
            _rendered = Compose();
            _fall.Restart();
        }

        /// <summary>Redraws, and notices the end of the game on the way past.</summary>
        private void Refresh()
        {
            if (_well.IsOver)
            {
                if (_well.Lines > UserData.TetrisBestLines)
                    UserData.TetrisBestLines = _well.Lines;

                _message = $"Stacked out with {_well.Lines} rows cleared. " +
                           "R for a new well, ENTER to return to the menu.";
            }

            _rendered = Compose();
        }

        /// <summary>Draws the header, the well with its panels beside it, and the status line under them.</summary>
        private string Compose()
        {
            var panels = new StringBuilder();
            panels.AppendLine(_nextFrame.Render(ComposeNext()));
            panels.Append(_statsFrame.Render(ComposeStats()));

            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine(TextColumns.Join(_frame.Render(ComposeWell()), panels.ToString()));
            body.AppendLine();
            body.Append(_message);
            return body.ToString();
        }

        /// <summary>
        ///     Draws the well: what has settled, the piece falling through it, and the outline showing where
        ///     that piece would land.
        /// </summary>
        private string ComposeWell()
        {
            _playfield.Clear();

            for (var y = 0; y < WellHeight; y++)
            for (var x = 0; x < WellWidth; x++)
            {
                var settled = _well.SettledAt(x, y);
                if (settled != null)
                    _playfield.Set(x, y, '█', StyleOf(settled.Value));
            }

            if (_well.Active != null)
            {
                // The ghost first, so the piece itself paints over it where the two overlap.
                Stamp(_well.ActiveX, _well.GhostY, '░');
                Stamp(_well.ActiveX, _well.ActiveY, '█');
            }

            return _playfield.Render();
        }

        /// <summary>
        ///     Writes the active piece's cells into the well at the given spot. The four-way bounds test this
        ///     used to carry is the grid's own contract now: a write that does not land is dropped.
        /// </summary>
        /// <param name="px">The column the piece's box starts at.</param>
        /// <param name="py">The row the piece's box starts at.</param>
        /// <param name="glyph">Which glyph to write, solid for the piece and shaded for its ghost.</param>
        private void Stamp(int px, int py, char glyph)
        {
            var piece = _well.Active;
            var style = StyleOf(piece.Kind);

            for (var y = 0; y < piece.Size; y++)
            for (var x = 0; x < piece.Size; x++)
            {
                if (piece.IsFilled(x, y))
                    _playfield.Set(px + x, py + y, glyph, style);
            }
        }

        /// <summary>
        ///     Draws the previewed piece inside the panel, in its spawn orientation and always in the same
        ///     four-by-four box however small the piece is.
        /// </summary>
        private string ComposeNext()
        {
            var piece = Tetromino.Create(_well.NextKind);
            var style = StyleOf(piece.Kind);

            // Centred in the fixed box with the odd cell on the right, which is the convention AnsiText.Fit
            // follows and the only one that is stable between calls.
            var offset = (PreviewCells - piece.Size)/2;

            _preview.Clear();

            for (var y = 0; y < piece.Size; y++)
            for (var x = 0; x < piece.Size; x++)
            {
                if (piece.IsFilled(x, y))
                    _preview.Set(offset + x, offset + y, '█', style);
            }

            return _preview.Render();
        }

        /// <summary>Draws the score panel.</summary>
        private string ComposeStats()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Stat("Score", _well.Score));
            sb.AppendLine(Stat("Lines", _well.Lines));
            sb.AppendLine(Stat("Level", _well.Level));
            sb.Append(Stat("Best", UserData.TetrisBestLines));
            return sb.ToString();
        }

        /// <summary>One line of the score panel, label left and figure right.</summary>
        /// <param name="label">What the figure is.</param>
        /// <param name="value">The figure.</param>
        /// <returns>The formatted line.</returns>
        private static string Stat(string label, int value)
        {
            // AnsiText.Fit rather than PadLeft, because Fit is the one that also TRIMS: a figure too wide for
            // its column is cut rather than allowed to push the line out, so the panel holds its width whatever
            // the score reaches. The two calls also say the panel's width once, where the old subtraction from
            // eleven said it nowhere.
            return AnsiText.Fit(label, PanelColumns - FigureColumns) +
                   AnsiText.Fit(value.ToString(CultureInfo.InvariantCulture), FigureColumns,
                       AnsiHorizontalAlignmentEnum.Right);
        }

        /// <summary>How each piece is drawn. A struct, so naming one per cell allocates nothing.</summary>
        /// <param name="kind">Which piece.</param>
        /// <returns>Its style.</returns>
        private static TextStyle StyleOf(TetrominoEnum kind)
        {
            return new TextStyle(ColorOf(kind));
        }

        /// <summary>
        ///     The color each piece has worn since the Game Boy, taken as <see cref="ConsoleColor" /> rather than
        ///     exact RGB so the pieces follow whatever theme the terminal is wearing.
        /// </summary>
        /// <param name="kind">Which piece.</param>
        /// <returns>Its color.</returns>
        private static ConsoleColor ColorOf(TetrominoEnum kind)
        {
            return kind switch
            {
                TetrominoEnum.I => ConsoleColor.Cyan,
                TetrominoEnum.O => ConsoleColor.Yellow,
                TetrominoEnum.T => ConsoleColor.Magenta,
                TetrominoEnum.S => ConsoleColor.Green,
                TetrominoEnum.Z => ConsoleColor.Red,
                TetrominoEnum.J => ConsoleColor.Blue,
                _ => ConsoleColor.DarkYellow
            };
        }
    }
}
