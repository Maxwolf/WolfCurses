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
    ///         columns and not in characters — see <see cref="SideBySide" />, which is a dozen lines and the only
    ///         thing here that is not either the game or a call into the library.
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
        private const int CellWidth = 2;

        /// <summary>
        ///     Paces gravity on real elapsed time. The interval is passed per step rather than set on the timer,
        ///     because it shortens with every level.
        /// </summary>
        private readonly IntervalTimer _fall = new(TimeSpan.FromMilliseconds(800));

        /// <summary>The well's frame.</summary>
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
            body.AppendLine(SideBySide.Join(_frame.Render(ComposeWell()), panels.ToString(), 2));
            body.AppendLine();
            body.Append(_message);
            return body.ToString();
        }

        /// <summary>
        ///     Draws the well: what has settled, the piece falling through it, and the outline showing where that
        ///     piece would land. Runs of like cells are styled once rather than one escape per cell.
        /// </summary>
        private string ComposeWell()
        {
            var glyphs = new char[WellHeight, WellWidth];
            var kinds = new TetrominoEnum?[WellHeight, WellWidth];

            for (var y = 0; y < WellHeight; y++)
            for (var x = 0; x < WellWidth; x++)
            {
                glyphs[y, x] = ' ';
                kinds[y, x] = _well.SettledAt(x, y);
                if (kinds[y, x] != null)
                    glyphs[y, x] = '█';
            }

            if (_well.Active != null)
            {
                // The ghost first, so the piece itself paints over it where the two overlap.
                Stamp(glyphs, kinds, _well.ActiveX, _well.GhostY, '░');
                Stamp(glyphs, kinds, _well.ActiveX, _well.ActiveY, '█');
            }

            var sb = new StringBuilder();
            for (var y = 0; y < WellHeight; y++)
            {
                if (y > 0)
                    sb.AppendLine();

                var runStart = 0;
                for (var x = 1; x <= WellWidth; x++)
                {
                    var sameAsRun = x < WellWidth &&
                                    glyphs[y, x] == glyphs[y, runStart] &&
                                    kinds[y, x] == kinds[y, runStart];
                    if (sameAsRun)
                        continue;

                    AppendRun(sb, glyphs[y, runStart], kinds[y, runStart], x - runStart);
                    runStart = x;
                }
            }

            return sb.ToString();
        }

        /// <summary>Writes the active piece's cells into the drawing buffers at the given spot.</summary>
        /// <param name="glyphs">The glyph buffer.</param>
        /// <param name="kinds">The color buffer.</param>
        /// <param name="px">The column the piece's box starts at.</param>
        /// <param name="py">The row the piece's box starts at.</param>
        /// <param name="glyph">Which glyph to write, solid for the piece and shaded for its ghost.</param>
        private void Stamp(char[,] glyphs, TetrominoEnum?[,] kinds, int px, int py, char glyph)
        {
            var piece = _well.Active;
            for (var y = 0; y < piece.Size; y++)
            for (var x = 0; x < piece.Size; x++)
            {
                if (!piece.IsFilled(x, y))
                    continue;

                var wx = px + x;
                var wy = py + y;
                if (wx < 0 || wx >= WellWidth || wy < 0 || wy >= WellHeight)
                    continue;

                glyphs[wy, wx] = glyph;
                kinds[wy, wx] = piece.Kind;
            }
        }

        /// <summary>Draws the previewed piece inside the panel, in its spawn orientation.</summary>
        private string ComposeNext()
        {
            var piece = Tetromino.Create(_well.NextKind);
            var sb = new StringBuilder();

            for (var y = 0; y < piece.Size; y++)
            {
                if (y > 0)
                    sb.AppendLine();

                // Every piece has at least one empty row in its box; skipping them would make the panel jump about
                // as the preview changed, so the box is drawn whole and the panel stays a fixed height.
                for (var x = 0; x < piece.Size; x++)
                {
                    if (piece.IsFilled(x, y))
                        AppendRun(sb, '█', piece.Kind, 1);
                    else
                        sb.Append(' ', CellWidth);
                }
            }

            return sb.ToString();
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
            return $"{label} {value.ToString(CultureInfo.InvariantCulture).PadLeft(11 - label.Length)}";
        }

        /// <summary>Writes one run of identical cells, styled once.</summary>
        /// <param name="sb">Where to write.</param>
        /// <param name="glyph">The glyph to repeat.</param>
        /// <param name="kind">Which piece's color to use, or null for an empty cell.</param>
        /// <param name="count">How many cells the run covers.</param>
        private static void AppendRun(StringBuilder sb, char glyph, TetrominoEnum? kind, int count)
        {
            var text = new string(glyph, count*CellWidth);
            if (kind == null || glyph == ' ')
            {
                sb.Append(text);
                return;
            }

            sb.Append(new TextStyle(ColorOf(kind.Value)).Apply(text));
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
