// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Text;
using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Snake
{
    /// <summary>
    ///     Snake: the arcade's real-time game, and the one that shows what steering a form looks like.
    ///     <para>
    ///         Three things about it are worth copying into a game of your own. It <b>paces itself with an
    ///         <see cref="IntervalTimer" /> off the system tick</b>, never on the simulation tick — that one fires
    ///         once a second, and a snake that moved once a second would be a screensaver. It <b>renders in
    ///         <see cref="OnTick" /> and caches the string</b>, because <see cref="OnRenderForm" /> is called every
    ///         system tick, roughly a thousand times a second, and rebuilding a playfield that often would spend the
    ///         whole frame budget drawing frames nobody asked for. And it <b>turns the input buffer off</b>
    ///         (<see cref="InputFillsBuffer" />), because it is steered rather than typed at: without that, WASD would
    ///         quietly accumulate in the prompt at the bottom of the screen while you played.
    ///     </para>
    ///     <para>
    ///         What it does <i>not</i> do is handle ESC — <see cref="GamesWindow" /> does that for every game at once —
    ///         or pause itself when the game-over box appears. It gets that free: a
    ///         <see cref="MessageBox" /> is a window, only the focused window is ticked, so the snake stops moving the
    ///         moment the box goes up without a line of code saying so. What it does <i>not</i> get free is the other
    ///         half of that: the timer keeps measuring real time while the box is up, so the snake would come back
    ///         owing itself a move. <see cref="Form{TData}.RestartOnActivate" /> is that half, and saying it is the
    ///         whole fix.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class SnakeDialog : Form<GamesWindowInfo>
    {
        /// <summary>Playfield size in cells. Each cell is drawn two characters wide, so the board comes out roughly square.</summary>
        private const int BoardWidth = 30;

        /// <summary>Playfield height in cells, which is also its height in screen rows.</summary>
        private const int BoardHeight = 15;

        /// <summary>How long a step takes at the start, and how much each piece of food takes off it, down to a floor.</summary>
        private static readonly TimeSpan _startingStep = TimeSpan.FromMilliseconds(130);

        private static readonly TimeSpan _fastestStep = TimeSpan.FromMilliseconds(60);
        private static readonly TimeSpan _stepPerFood = TimeSpan.FromMilliseconds(3);

        /// <summary>
        ///     Paces the steps on real elapsed time rather than on ticks of unknown length. Registered with
        ///     <see cref="Form{TData}.RestartOnActivate" /> below, which is what stops the snake owing itself a free
        ///     move for however long the game-over box was up.
        /// </summary>
        private readonly IntervalTimer _step = new(_startingStep);

        /// <summary>The playfield's frame. A widget from the library, doing here exactly what it does in a dialog.</summary>
        private readonly Box _frame = new() {Title = "Snake", Padding = 0};

        /// <summary>
        ///     The playfield itself, as cells. <c>CellWidth</c> is two because a character cell is about twice as
        ///     tall as it is wide, so a board meant to look square has to draw each cell two columns across.
        ///     <para>
        ///         This used to be a <c>char[,]</c> and thirty lines that walked each row breaking it into runs of
        ///         like cells and styling each run once — which is what put <see cref="TextGrid" /> in the library,
        ///         since the missile field and the chess text board had each written the same loop. The grid is kept
        ///         and cleared rather than reallocated, and <b>the clear is load-bearing here</b> in a way it is not
        ///         everywhere: the snake vacates its tail cell every step, and a cell nobody repaints keeps whatever
        ///         it had.
        ///     </para>
        /// </summary>
        private readonly TextGrid _playfield = new(BoardWidth, BoardHeight) {CellWidth = 2};

        private static readonly TextStyle _headStyle = new(ConsoleColor.Green);
        private static readonly TextStyle _bodyStyle = new(ConsoleColor.DarkGreen);
        private static readonly TextStyle _foodStyle = new(ConsoleColor.Red);

        private SnakeBoard _board;
        private string _rendered;
        private bool _resultShown;

        /// <summary>Initializes a new instance of the <see cref="SnakeDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SnakeDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Keeps typed characters out of the input buffer. This game is steered, so a key press is a move and not
        ///     the beginning of a word; leaving this at its default would echo every W, A, S and D at the prompt.
        ///     ENTER still arrives at <see cref="OnInputBufferReturned" /> — it is buffer control, not buffer content —
        ///     which is what lets ENTER quit even though nothing can be typed.
        /// </summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "Arrow keys or WASD to steer, ENTER or ESC to quit";

            _board = new SnakeBoard(BoardWidth, BoardHeight, SimUnit.Random);
            _rendered = Compose();

            // Registering also starts the timer, so there is no separate Restart here — and it is the whole of what
            // the hand-written OnFormActivate override used to do.
            RestartOnActivate(_step);
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            // ENTER and BACKSPACE never reach here at all — the input manager consumes both as buffer control before
            // a key press is ever queued — so quitting is handled in OnInputBufferReturned instead.
            var steer = key switch
            {
                ConsoleKey.UpArrow or ConsoleKey.W => DirectionEnum.Up,
                ConsoleKey.DownArrow or ConsoleKey.S => DirectionEnum.Down,
                ConsoleKey.LeftArrow or ConsoleKey.A => DirectionEnum.Left,
                ConsoleKey.RightArrow or ConsoleKey.D => DirectionEnum.Right,
                _ => DirectionEnum.None
            };

            if (steer == DirectionEnum.None)
                return;

            _board.Steer(steer);

            // Redraw at once rather than waiting for the step: the direction is not visible on the board, but the
            // heading in the status line is, and an input that takes up to a tenth of a second to acknowledge feels
            // broken however correct it is.
            _rendered = Compose();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (_board.IsOver)
            {
                ShowResult();
                return;
            }

            // On the system tick, not the simulation tick: the simulation ticks once a second. The interval is
            // passed per step rather than set on the timer because it shortens with every piece of food eaten.
            if (!_step.TryConsume(StepInterval()))
                return;

            _board.Advance();
            _rendered = Compose();
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

        /// <summary>How long the current step lasts: shorter the longer the snake, down to a floor it cannot pass.</summary>
        /// <returns>How long this step should last.</returns>
        private TimeSpan StepInterval()
        {
            var shortened = _startingStep - _stepPerFood*_board.Score;
            return shortened < _fastestStep ? _fastestStep : shortened;
        }

        /// <summary>
        ///     Puts up the game-over box exactly once, records the score, and returns to the menu when it is
        ///     dismissed. The guard matters because this is reached from a tick, and ticks keep coming while the box
        ///     is being opened.
        /// </summary>
        private void ShowResult()
        {
            if (_resultShown)
                return;

            _resultShown = true;

            if (_board.Score > UserData.SnakeHighScore)
                UserData.SnakeHighScore = _board.Score;

            var message = _board.Won
                ? $"You filled the entire board. Final score: {_board.Score}."
                : $"You crashed. Final score: {_board.Score}.";

            MessageBox.Show(SimUnit, message, () => ClearForm());
        }

        /// <summary>Draws the whole screen: a status line, then the playfield inside its box.</summary>
        private string Compose()
        {
            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine($"Score {_board.Score}    Length {_board.Body.Count}    " +
                            $"Heading {_board.Direction}    Best {UserData.SnakeHighScore}");
            body.AppendLine();
            body.Append(_frame.Render(ComposePlayfield()));
            return body.ToString();
        }

        /// <summary>
        ///     Draws the cells. The grid does the colouring, a run of like cells at a time rather than a cell at a
        ///     time — a snake fifty segments long would otherwise be fifty identical escape sequences the terminal
        ///     did not need.
        ///     <para>
        ///         The glyphs differ as well as the colors on purpose: under <c>NO_COLOR</c>, or on a terminal that
        ///         cannot do color at all, the food has to still be findable. The head shares the body's glyph and
        ///         differs only by colour, which is the one exception and is deliberate — losing the head in the body
        ///         costs nothing, since it is always the end that is moving.
        ///     </para>
        /// </summary>
        private string ComposePlayfield()
        {
            _playfield.Clear();

            // Body first, then the head over it: the head is part of the body, so painting it first would be
            // painted over by its own second segment.
            foreach (var (x, y) in _board.Body)
                _playfield.Set(x, y, '█', _bodyStyle);

            var head = _board.Body[0];
            _playfield.Set(head.X, head.Y, '█', _headStyle);
            _playfield.Set(_board.Food.X, _board.Food.Y, '▓', _foodStyle);

            return _playfield.Render();
        }
    }
}
