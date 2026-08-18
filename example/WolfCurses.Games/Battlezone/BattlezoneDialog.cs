// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     Battlezone: a tank, a plain, and the only screen in this arcade that is a <i>view</i> rather than a map.
    ///     <para>
    ///         <b>That is the whole reason it exists.</b> Every other game here shows the player the entire board
    ///         from outside it — a maze, a well, a chessboard, a table of cards — so the information problem is
    ///         nil and the difficulty is all in what to do with it. Here the screen shows only what is in front of
    ///         the tank, the radar shows only which way things are, and combining the two while something you cannot
    ///         see manoeuvres behind you <i>is</i> the game. Nothing else in this arcade could demonstrate that,
    ///         because nothing else has a camera.
    ///     </para>
    ///     <para>
    ///         It is also the third answer to "where do the pictures come from": chess loads artwork off disk,
    ///         Missile Command draws shapes into a buffer, and this one has no artwork and no shapes — it has a
    ///         world, and works out from where the player is standing which lines that world would look like. The
    ///         same scene then goes to a <c>PixelBuffer</c> or to a <c>TextGrid</c> through one walk in
    ///         <see cref="BattleScene" />, which is why the two views cannot disagree.
    ///     </para>
    ///     <para>
    ///         The controls need <see cref="HeldAxis" /> for the reason Missile Command needed it first: a tank
    ///         turning is a key being <i>held</i>, and a terminal never reports a key being let go. That type came
    ///         out of that game while this one was being written, which is the whole point of this folder.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class BattlezoneDialog : Form<GamesWindowInfo>
    {
        /// <summary>How often the world is advanced and redrawn.</summary>
        private static readonly TimeSpan _framePace = TimeSpan.FromMilliseconds(33);

        /// <summary>Rows of chrome around the view: a blank, the status, a blank, the message and the prompt.</summary>
        private const int ChromeRows = 6;

        private readonly IntervalTimer _frame = new(_framePace);
        private readonly HeldAxis _turn = new();
        private readonly HeldAxis _throttle = new();

        private BattleField _field;
        private BattlezoneArt _art;
        private BattlezoneText _text;
        private string _rendered;

        private bool _forceText;
        private bool _scoreRecorded;

        /// <summary>Initializes a new instance of the <see cref="BattlezoneDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public BattlezoneDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Keeps typed characters out of the input buffer, since firing is SPACE and steering is WASD — all
        ///     printable, and all of which would otherwise pile up in the echoed prompt.
        /// </summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            // Kept under eighty columns on purpose: the prompt is a real row of the frame, and one long enough to
            // wrap costs a row of the view on the smallest terminal this has to fit.
            ParentWindow.PromptText = "Arrows/WASD drive, SPACE fires, TAB view, ENTER replays, ESC leaves";

            RestartOnActivate(_frame);
            NewGame();
        }

        /// <summary>
        ///     Notes which way the player is holding the sticks. It moves nothing — see <see cref="HeldAxis" />.
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            switch (key)
            {
                case ConsoleKey.LeftArrow or ConsoleKey.A:
                    _turn.Press(-1);
                    break;
                case ConsoleKey.RightArrow or ConsoleKey.D:
                    _turn.Press(1);
                    break;
                case ConsoleKey.UpArrow or ConsoleKey.W:
                    _throttle.Press(1);
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.S:
                    _throttle.Press(-1);
                    break;
                case ConsoleKey.Spacebar:
                    _field.Fire();
                    break;
                case ConsoleKey.Tab:
                    // TAB because it is not printable, so it cannot end up in the prompt. The same choice every
                    // other demo in this repository makes, for the same reason.
                    _forceText = !_forceText;
                    break;
            }
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // On the system tick and paced. A simulation tick is once a second, which for a game with ballistics in
            // it would be a slideshow.
            if (!_frame.TryConsume())
                return;

            _field.Advance(_frame.LastElapsed, _turn.Direction, _throttle.Direction);
            RecordScoreOnce();
            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called about a thousand times a second, so it hands back a string built at most thirty times a second.
            return _rendered;
        }

        /// <summary>
        ///     ENTER deals another game once this one is over, and does nothing at all while one is being played.
        ///     <para>
        ///         The binding the card tables settled on, and it is right for the same reason: this is a game with
        ///         rounds, so the key a player hits by reflex when the screen breaks must not be the one that closes
        ///         the cabinet. ESC is the way out, and it is on the prompt. Mid-game the key is ignored rather than
        ///         restarting, since throwing away a game in progress for a stray keystroke is the worst of the
        ///         three things it could do.
        ///     </para>
        /// </summary>
        /// <param name="input">Whatever was typed, which is nothing — the buffer is disabled.</param>
        public override void OnInputBufferReturned(string input)
        {
            if (!_field.IsOver)
                return;

            NewGame();
        }

        /// <summary>Starts a fresh game on a fresh plain.</summary>
        private void NewGame()
        {
            _field = new BattleField(SimUnit.Random);
            _turn.Release();
            _throttle.Release();
            _scoreRecorded = false;
            Compose();
        }

        /// <summary>Banks the score the first time the game ends, and only then.</summary>
        private void RecordScoreOnce()
        {
            if (_scoreRecorded || !_field.IsOver)
                return;

            _scoreRecorded = true;
            if (_field.Score > UserData.BattlezoneBestScore)
                UserData.BattlezoneBestScore = _field.Score;
        }

        /// <summary>Rebuilds the screen: status above, the view, then the message.</summary>
        private void Compose()
        {
            // Read once into locals; each of these is a live syscall and this runs thirty times a second.
            var height = AnsiConsole.SafeWindowHeight();
            var width = AnsiConsole.SafeWindowWidth();
            var rows = Math.Max(8, height - ChromeRows);
            var columns = Math.Max(24, width - 2);

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(StatusLine());
            sb.AppendLine();
            sb.AppendLine(PictureIsWorthIt(rows) ? PaintPicture(columns, rows) : PaintText(columns, rows));
            sb.Append(_field.Message);

            _rendered = sb.ToString();
        }

        /// <summary>
        ///     Decides whether a picture is worth drawing at all.
        ///     <para>
        ///         The half-block threshold is deliberately high — higher than any other game here. A wireframe is
        ///         the one subject a character grid is genuinely good at, since a line drawn in slashes is still the
        ///         line, so on a small terminal the character view is not a fallback but the better picture. Real
        ///         pixels win as soon as there are enough of them to be worth the trip.
        ///     </para>
        /// </summary>
        /// <param name="rows">How many rows the view may claim.</param>
        /// <returns>True to draw a picture, false to draw characters.</returns>
        private bool PictureIsWorthIt(int rows)
        {
            if (!AnsiConsole.SupportsPictures() || _forceText)
                return false;

            return ImageRenderers.Default.DrawsTruePixels ? rows >= 12 : rows >= 30;
        }

        /// <summary>Draws the view as real pixels, through whatever the terminal turned out to support.</summary>
        /// <param name="columns">How many columns the view may claim.</param>
        /// <param name="rows">How many rows the view may claim.</param>
        /// <returns>The view as one block of ANSI.</returns>
        private string PaintPicture(int columns, int rows)
        {
            var (canvasWidth, canvasHeight) = BattlezoneArt.SizeFor(columns, rows);

            // Rebuilt only when the terminal has really been resized; the buffer and the camera are reused between
            // frames, and rebuilding either per frame would be the cost of the game.
            if (_art == null || _art.Width != canvasWidth || _art.Height != canvasHeight)
                _art = new BattlezoneArt(canvasWidth, canvasHeight);

            var options = new AnsiImageOptions
            {
                MaxRows = rows,
                MaxColumns = columns,
                RowMargin = 0
            };

            return AnsiImage.FromPixels(_art.Paint(_field)).ToAnsi(options, ImageRenderers.Default);
        }

        /// <summary>Draws the view as characters.</summary>
        /// <param name="columns">How many columns the view may claim.</param>
        /// <param name="rows">How many rows the view may claim.</param>
        /// <returns>The view as text.</returns>
        private string PaintText(int columns, int rows)
        {
            if (_text == null || _text.Columns != columns || _text.Rows != rows)
                _text = new BattlezoneText(columns, rows);

            return _text.Render(_field);
        }

        /// <summary>The line above the view: the score, what is left of the fleet, and the best so far.</summary>
        /// <returns>The status line.</returns>
        private string StatusLine()
        {
            var tanks = new StringBuilder();
            for (var i = 0; i < _field.Lives; i++)
                tanks.Append('#');

            if (_field.Lives == 0)
                tanks.Append('-');

            return string.Format(CultureInfo.InvariantCulture,
                "Score {0:N0}  ·  Tanks {1}  ·  Kills {2}  ·  Best {3:N0}",
                _field.Score, tanks, _field.Kills, UserData.BattlezoneBestScore);
        }
    }
}
