// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.PacMan
{
    /// <summary>
    ///     Pac-Man: eat the board, avoid four ghosts that are each hunting you differently.
    ///     <para>
    ///         <b>What this one is here to show is that interesting behaviour does not need clever code.</b> The
    ///         ghosts have no path-finding, no search and no coordination — each picks whichever neighbouring square
    ///         is nearest its own target, and the four targets are one line each. Being flanked in a corridor is an
    ///         emergent property of that, and it is worth reading beside <see cref="Chess.ChessDialog" />, which
    ///         spends a whole sliced alpha-beta search to play one opponent well. Two demonstrations of "how does a
    ///         game think", at opposite ends.
    ///     </para>
    ///     <para>
    ///         It is also the first screen here to draw with <see cref="BoxDrawing" />, which is the other half of the
    ///         library's line vocabulary: <see cref="Box" /> knows a rectangle's six glyphs up front, and a maze needs
    ///         all sixteen chosen per cell from its neighbours. The board is one <see cref="TextGrid" />, the panel
    ///         beside it is ordinary widgets, and <see cref="TextColumns" /> puts them side by side — which is the
    ///         same trick <see cref="Tetris.TetrisDialog" /> uses and the reason that helper is in the library.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class PacManDialog : Form<GamesWindowInfo>
    {
        /// <summary>
        ///     How long one step of the game takes. Everything — the player, the ghosts, the scatter clock, the blue
        ///     timer — is counted in these, so the rules never touch a clock and the whole game can be driven a
        ///     thousand steps deep in a test with nothing sleeping.
        /// </summary>
        private static readonly TimeSpan _stepLength = TimeSpan.FromMilliseconds(115);

        /// <summary>How fast the power pellets and an expiring ghost blink.</summary>
        private static readonly TimeSpan _blinkLength = TimeSpan.FromMilliseconds(220);

        private readonly IntervalTimer _step = new(_stepLength);
        private readonly IntervalTimer _blink = new(_blinkLength);

        /// <summary>The blue-time readout. A determinate bar from the library, doing what it does anywhere else.</summary>
        private readonly ProgressBar _frightBar = new()
        {
            Width = 12,
            ShowPercentage = false,
            FilledStyle = ConsoleColor.White,
            EmptyStyle = ConsoleColor.DarkGray
        };

        private PacManGame _game;
        private TextGrid _board;
        private string _rendered;
        private bool _blinkOn = true;
        private bool _resultShown;

        /// <summary>Initializes a new instance of the <see cref="PacManDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public PacManDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Keeps typed characters out of the input buffer: this game is steered, so WASD would otherwise pile up
        ///     in the prompt while the player is running away from something.
        /// </summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "Arrows or WASD to steer, ENTER or ESC to quit";

            _game = new PacManGame(SimUnit.Random);
            _board = PacManView.CreateGrid(_game);
            _rendered = Compose();

            // Registering also starts them, and is what stops the board owing itself a fistful of steps after the
            // game-over box has been sitting on screen.
            RestartOnActivate(_step, _blink);
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            var steer = key switch
            {
                ConsoleKey.UpArrow or ConsoleKey.W => DirectionEnum.Up,
                ConsoleKey.DownArrow or ConsoleKey.S => DirectionEnum.Down,
                ConsoleKey.LeftArrow or ConsoleKey.A => DirectionEnum.Left,
                ConsoleKey.RightArrow or ConsoleKey.D => DirectionEnum.Right,
                _ => DirectionEnum.None
            };

            // Not redrawn here on purpose, unlike the snake: a turn is a *wish* that the game grants at the next
            // corner it can, so there is nothing new to show until the step happens. Redrawing per key press would
            // recompose the whole board a dozen times inside one tick for a held arrow - the exact bug ChessDialog
            // had and the reason it now sets a flag instead.
            _game.Steer(steer);
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (_game.IsOver)
            {
                ShowResult();
                return;
            }

            var moved = false;

            if (_blink.TryConsume())
            {
                _blinkOn = !_blinkOn;
                moved = true;
            }

            // On the system tick, never the simulation tick: that one fires once a second, and a Pac-Man that moved
            // once a second would be a spreadsheet.
            if (_step.TryConsume())
            {
                _game.Step();
                moved = true;
            }

            if (moved)
                _rendered = Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called every system tick, roughly a thousand times a second, so it hands back a string that is already
            // built. Composing here would rebuild a six-hundred-cell board for every one of those.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>Puts up the game-over box exactly once, records the score, and returns to the menu.</summary>
        private void ShowResult()
        {
            if (_resultShown)
                return;

            _resultShown = true;

            if (_game.Score > UserData.PacManHighScore)
                UserData.PacManHighScore = _game.Score;

            MessageBox.Show(SimUnit,
                $"The ghosts got you on board {_game.Level}. Final score: {_game.Score:N0}.",
                () => ClearForm());
        }

        /// <summary>Draws the heading, then the board with its panel beside it.</summary>
        private string Compose()
        {
            PacManView.Paint(_board, _game, _blinkOn);

            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine($"Score {_game.Score:N0}    Board {_game.Level}    " +
                            $"Lives {new string('<', Math.Max(0, _game.Lives - 1))}    " +
                            $"Best {UserData.PacManHighScore:N0}");

            // TextColumns rather than PadRight, because the board's rows are a few hundred bytes of colour each and
            // padding them by character count would shred the panel diagonally down the screen.
            body.Append(TextColumns.Join(_board.Render(), ComposePanel(), 3));
            return body.ToString();
        }

        /// <summary>The panel down the right-hand side: what the board is doing, and who is where.</summary>
        private string ComposePanel()
        {
            var panel = new StringBuilder();
            panel.AppendLine();
            panel.AppendLine(_game.Mode == GhostModeEnum.Scatter
                ? new TextStyle(ConsoleColor.DarkGreen).Apply("SCATTER")
                : new TextStyle(ConsoleColor.Red, bold: true).Apply("CHASE"));
            panel.AppendLine();

            if (_game.FrightenedLeft > 0)
            {
                panel.AppendLine("BLUE");
                panel.AppendLine(_frightBar.Render(_game.FrightenedLeft, _game.FrightenedLength));
            }
            else
            {
                panel.AppendLine();
                panel.AppendLine();
            }

            panel.AppendLine();

            foreach (var ghost in _game.Ghosts)
            {
                var state = ghost.State switch
                {
                    GhostStateEnum.Eaten => "eyes",
                    GhostStateEnum.Frightened => "blue",
                    _ => ghost.Penned ? "home" : _game.Mode.ToString().ToLowerInvariant()
                };

                var name = ghost.Kind.ToString().PadRight(7);
                panel.AppendLine(new TextStyle(PacManView.ColorOf(ghost.Kind)).Apply($"{name}{state}"));
            }

            panel.AppendLine();
            panel.Append(string.Create(CultureInfo.InvariantCulture,
                $"Pellets {_game.PelletsEaten}/{_game.Maze.TotalPellets}"));

            return panel.ToString();
        }
    }
}
