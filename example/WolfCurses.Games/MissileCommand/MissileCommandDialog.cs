// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     Missile Command, and the game in this arcade that is animated rather than stepped.
    ///     <para>
    ///         <b>Everything here moves on elapsed time, and nothing moves per key press or per tick.</b> Snake and
    ///         Tetris advance a grid one cell at a time on a clock, which makes a tick a perfectly good unit for
    ///         them. Nothing on this screen is on a grid: warheads fall at fractions of a field a second along
    ///         arbitrary diagonals, so the only honest unit is however long really passed, and that is what
    ///         <see cref="IntervalTimer.LastElapsed" /> hands back.
    ///     </para>
    ///     <para>
    ///         <b>The picture is generated, not decoded</b>, which is the other half of the graphics stack from
    ///         WolfChess 5000 — no artwork, no content copied beside the executable, just lines and circles drawn
    ///         into a <c>PixelBuffer</c> thirty times a second. And because a picture is one payload row that paints
    ///         across many, nothing may sit beside it: the status goes above and the message below, exactly as in
    ///         chess, and the ammunition counters are drawn <i>inside</i> the picture where the arcade also put them.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class MissileCommandDialog : Form<GamesWindowInfo>
    {
        /// <summary>How often the field is advanced and redrawn. Thirty a second is what the frame budget allows.</summary>
        private static readonly TimeSpan _framePace = TimeSpan.FromMilliseconds(33);

        /// <summary>The shortest gap between two shells, so one long press of SPACE is not the whole magazine.</summary>
        private static readonly TimeSpan _shotPace = TimeSpan.FromMilliseconds(110);

        /// <summary>
        ///     How long an arrow may go unheard before the crosshair is treated as having stopped. Comfortably longer
        ///     than the operating system's key-repeat interval, which is about a thirtieth of a second, and
        ///     comfortably shorter than a human tapping a key on purpose.
        /// </summary>
        private static readonly TimeSpan _keyUpAfter = TimeSpan.FromMilliseconds(180);

        /// <summary>How long the crosshair takes to work up from its opening nudge to full speed.</summary>
        private static readonly TimeSpan _rampOver = TimeSpan.FromMilliseconds(420);

        /// <summary>Crosshair speed in world units a second, from a first tap to a held sweep.</summary>
        private const double SlowAim = 0.30;

        private const double FastAim = 1.35;

        /// <summary>Rows of chrome around the field: a blank, the status, a blank, the message and the prompt.</summary>
        private const int ChromeRows = 6;

        private readonly IntervalTimer _frame = new(_framePace);

        /// <summary>
        ///     When the last shell actually left a battery, measured on <see cref="IntervalTimer.TotalElapsed" />.
        ///     <para>
        ///         <b>Deliberately not a second <see cref="IntervalTimer" />, and this is worth knowing before
        ///         reaching for one.</b> That type is a <i>pacer</i>: it exists to make something happen every so
        ///         often, so a fresh one is not due until its first period has passed. A rate limit is the other
        ///         shape — the first action must be free and only the <i>next</i> one waits — so paced with a timer,
        ///         the player's opening shot is silently eaten, and because
        ///         <see cref="Form{TData}.RestartOnActivate" /> restarts a registered timer, so is the first shot
        ///         after every modal dialog. Starting the stamp one whole gap in the past says "ready now" with no
        ///         special case at all.
        ///     </para>
        /// </summary>
        private TimeSpan _lastShotAt = -_shotPace;

        private MissileField _field;
        private MissileFieldArt _art;
        private string _rendered;

        private double _aimX = MissileField.Aspect/2.0;
        private double _aimY = 0.62;

        private int _driftX;
        private int _driftY;
        private TimeSpan _driftXAt;
        private TimeSpan _driftYAt;
        private TimeSpan _movingSince;

        private bool _vtReady;
        private bool _forceText;
        private bool _scoreRecorded;

        /// <summary>Initializes a new instance of the <see cref="MissileCommandDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public MissileCommandDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Keeps typed characters out of the input buffer. It matters here for the same reason it matters in
        ///     Tetris: firing is bound to SPACE, which is a printable character, and left at the default every shot
        ///     would widen the echoed prompt at the bottom of the screen. ENTER still arrives at
        ///     <see cref="OnInputBufferReturned" />, being buffer control rather than buffer content, which is what
        ///     lets it quit.
        /// </summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText =
                "Arrows or WASD aim, SPACE fires, Z/X/C picks a battery, TAB switches board, ENTER or ESC quits";

            // Asked once, not per frame. Enable does real work - it sets the output encoding and, on Windows, calls
            // into the console API - and the answer cannot change under a running process.
            _vtReady = AnsiConsole.Enable();

            RestartOnActivate(_frame);
            NewGame();
        }

        /// <summary>
        ///     Notes which way the player is pointing and when they last said so. <b>It does not move anything.</b>
        ///     <para>
        ///         This is the trap this game exists to demonstrate, and it is the opposite of the right answer for
        ///         every other game in this arcade. <c>InputManager</c> drains the console's key buffer with a
        ///         <c>while</c> loop and dispatches every key it found inside a single tick — which is a fix, and the
        ///         thing that stopped the collision demo feeling like a hockey puck — so a held arrow arrives as a
        ///         burst of eight or ten presses with no time at all between them. Moving the crosshair one step per
        ///         press therefore makes aiming speed a function of the player's key-repeat setting and of how far
        ///         behind the tick loop has fallen, and the harder the machine is working the faster the crosshair
        ///         flies. In Snake, Tetris and chess a key press is one discrete action and a burst of repeats is
        ///         exactly right; here it is a direction being held, and the only thing a terminal will never tell
        ///         you is when it was let go. So this records a heading and a timestamp, and
        ///         <see cref="OnTick" /> integrates real elapsed time against it — treating silence longer than
        ///         <see cref="_keyUpAfter" /> as the key-up event that is never coming.
        ///     </para>
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            // TotalElapsed, deliberately: it is the one reading RestartOnActivate does not reset, so coming back
            // from a modal dialog leaves these stamps correctly stale and the crosshair stops rather than lurching
            // off in whatever direction was last held before the interruption.
            var now = _frame.TotalElapsed;

            switch (key)
            {
                case ConsoleKey.LeftArrow or ConsoleKey.A:
                    _driftX = -1;
                    _driftXAt = now;
                    break;
                case ConsoleKey.RightArrow or ConsoleKey.D:
                    _driftX = 1;
                    _driftXAt = now;
                    break;
                case ConsoleKey.UpArrow or ConsoleKey.W:
                    _driftY = 1;
                    _driftYAt = now;
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.S:
                    _driftY = -1;
                    _driftYAt = now;
                    break;
                case ConsoleKey.Spacebar:
                    FireAt(_field.BestSilo(_aimX, _aimY));
                    return;
                case ConsoleKey.Z:
                    FireAt(0);
                    return;
                case ConsoleKey.X:
                    FireAt(1);
                    return;
                case ConsoleKey.C:
                    FireAt(2);
                    return;
                case ConsoleKey.Tab:
                    // TAB because it is not printable, so AddCharToInputBuffer drops it and it cannot pollute the
                    // prompt the way a letter would. The same reason the other demos use it.
                    _forceText = !_forceText;
                    return;
                case ConsoleKey.R:
                    if (_field.IsOver)
                        NewGame();

                    return;
                default:
                    return;
            }

            // The ramp starts when the crosshair starts moving, not when a key arrives, so a held sweep accelerates
            // once rather than restarting every time the operating system repeats.
            if (_driftX == 0 && _driftY == 0)
                _movingSince = now;
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // On the system tick, and paced. The simulation tick fires once a second, which would be a slideshow.
            if (!_frame.TryConsume())
                return;

            var elapsed = _frame.LastElapsed;

            SteerCrosshair(elapsed);
            _field.Advance(elapsed);
            RecordScoreOnce();
            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick, roughly a thousand times a second, so it hands back a string built at
            // most thirty times a second up in OnTick.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>Fires from a battery, if it can, and refuses quietly rather than loudly when it cannot.</summary>
        /// <param name="silo">Which battery, or -1 when nothing can answer.</param>
        private void FireAt(int silo)
        {
            if (silo < 0)
                return;

            var now = _frame.TotalElapsed;
            if (now - _lastShotAt < _shotPace)
                return;

            // Stamped only when a shell really left, so a press that was refused for having no ammunition does not
            // also cost the player the next tenth of a second.
            if (_field.Fire(silo, _aimX, _aimY))
                _lastShotAt = now;
        }

        /// <summary>
        ///     Moves the crosshair by however long really passed, in whatever direction was most recently asked for.
        /// </summary>
        /// <param name="elapsed">How long the frame lasted.</param>
        private void SteerCrosshair(TimeSpan elapsed)
        {
            var now = _frame.TotalElapsed;

            // The key-up event a terminal never sends, inferred from silence.
            if (now - _driftXAt > _keyUpAfter)
                _driftX = 0;
            if (now - _driftYAt > _keyUpAfter)
                _driftY = 0;

            if (_driftX == 0 && _driftY == 0)
                return;

            var held = (now - _movingSince).TotalSeconds/_rampOver.TotalSeconds;
            var speed = SlowAim + (FastAim - SlowAim)*Math.Clamp(held, 0.0, 1.0);

            // A diagonal would otherwise be about forty per cent faster than a straight line, which is a thing
            // players notice without being able to say what is wrong.
            if (_driftX != 0 && _driftY != 0)
                speed /= Math.Sqrt(2.0);

            var step = speed*elapsed.TotalSeconds;
            _aimX = Math.Clamp(_aimX + _driftX*step, 0.0, MissileField.Aspect);
            _aimY = Math.Clamp(_aimY + _driftY*step, MissileField.MinAimY, 1.0);
        }

        /// <summary>Deals a fresh field.</summary>
        private void NewGame()
        {
            _field = new MissileField(SimUnit.Random);
            _aimX = MissileField.Aspect/2.0;
            _aimY = 0.62;
            _driftX = 0;
            _driftY = 0;
            _scoreRecorded = false;
            Compose();
        }

        /// <summary>Banks the score the first time the game ends, and only then.</summary>
        private void RecordScoreOnce()
        {
            if (_scoreRecorded || !_field.IsOver)
                return;

            _scoreRecorded = true;
            if (_field.Score > UserData.MissileCommandBestScore)
                UserData.MissileCommandBestScore = _field.Score;
        }

        /// <summary>
        ///     Decides whether a picture is worth drawing at all.
        ///     <para>
        ///         The first test is the one that is easy to forget and impossible to miss once it bites: on a
        ///         console where virtual-terminal processing cannot be turned on, <c>ConsolePresenter</c> falls back
        ///         to a plain-text path that <i>blanks</i> a true-pixel payload row rather than printing its escape
        ///         bytes as garbage. Entirely the right call for the presenter, and it means a game that only knew
        ///         how to draw pixels would show a black screen and a prompt, with nothing anywhere reporting a
        ///         problem.
        ///     </para>
        /// </summary>
        /// <param name="rows">How many rows the board may claim.</param>
        /// <returns>True to draw a picture, false to draw characters.</returns>
        private bool PictureIsWorthIt(int rows)
        {
            if (!_vtReady || _forceText)
                return false;

            // Neither true-pixel renderer consults the colour mode, so without this a forced grayscale or a NO_COLOR
            // environment would grey every widget on screen and leave the picture in full colour.
            if (AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None)
                return false;

            // Half blocks give two pixels a row, so a field squeezed into a dozen rows is a smear; real pixels only
            // need enough rows to be worth looking at. Asked of the renderer rather than type-tested against the
            // built-in classes, which gets the wrong answer for exactly the third-party renderers the seam allows.
            return ImageRenderers.Default.DrawsTruePixels ? rows >= 12 : rows >= 26;
        }

        /// <summary>Rebuilds the screen: status above, the field, then the message.</summary>
        private void Compose()
        {
            // Read once into locals - every one of these is a live syscall, and this runs thirty times a second.
            var height = AnsiConsole.SafeWindowHeight();
            var width = AnsiConsole.SafeWindowWidth();
            var rows = Math.Max(6, height - ChromeRows);

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(StatusLine());
            sb.AppendLine();
            sb.AppendLine(PictureIsWorthIt(rows) ? PaintPicture(rows, width) : PaintText(rows, width));
            sb.Append(_field.Message);

            _rendered = sb.ToString();
        }

        /// <summary>Draws the field as real pixels, through whatever the terminal turned out to support.</summary>
        private string PaintPicture(int rows, int columns)
        {
            var (canvasWidth, canvasHeight) = MissileFieldArt.SizeFor(rows);

            // Rebuilt only when the terminal has actually been resized, since the buffer is reused between frames.
            if (_art == null || _art.Width != canvasWidth || _art.Height != canvasHeight)
                _art = new MissileFieldArt(canvasWidth, canvasHeight);

            var options = new AnsiImageOptions
            {
                MaxRows = rows,
                MaxColumns = Math.Max(16, columns - 1),
                RowMargin = 0
            };

            return AnsiImage.FromPixels(_art.Paint(_field, _aimX, _aimY)).ToAnsi(options);
        }

        /// <summary>Draws the field as characters.</summary>
        private string PaintText(int rows, int columns)
        {
            return MissileFieldText.Render(_field, _aimX, _aimY, Math.Max(20, columns - 2), rows);
        }

        /// <summary>The line above the field: which wave, the score, and what is left to defend it with.</summary>
        private string StatusLine()
        {
            var cities = new StringBuilder();
            foreach (var standing in _field.CitiesStanding)
                cities.Append(standing ? '#' : '.');

            var ammo = new StringBuilder();
            for (var silo = 0; silo < _field.SiloAmmo.Count; silo++)
            {
                if (silo > 0)
                    ammo.Append('/');

                ammo.Append(_field.SilosStanding[silo]
                    ? _field.SiloAmmo[silo].ToString(CultureInfo.InvariantCulture)
                    : "-");
            }

            return string.Format(CultureInfo.InvariantCulture,
                "Wave {0}  ·  Score {1:N0}  ·  x{2}  ·  Cities {3}  ·  Ammo {4}  ·  Best {5:N0}",
                _field.Wave, _field.Score, _field.Multiplier, cities, ammo, UserData.MissileCommandBestScore);
        }
    }
}
