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

        /// <summary>How long the crosshair takes to work up from its opening nudge to full speed.</summary>
        private static readonly TimeSpan _rampOver = TimeSpan.FromMilliseconds(420);

        /// <summary>Crosshair speed in world units a second, from a first tap to a held sweep.</summary>
        private const double SlowAim = 0.30;

        private const double FastAim = 1.35;

        /// <summary>Rows of chrome around the field: a blank, the status, a blank, the message and the prompt.</summary>
        private const int ChromeRows = 6;

        /// <summary>The renderer used while the mouse is on — see <see cref="BoardRenderer" /> for why.</summary>
        private static readonly HalfBlockImageRenderer _halfBlocks = new();

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

        /// <summary>
        ///     Which way the player is holding the crosshair, one axis each way.
        ///     <para>
        ///         This used to be five fields and a pair of timestamp comparisons written out here; it is now
        ///         <see cref="HeldAxis" />, which is where the library keeps the trap. The extraction also fixed a
        ///         real bug in this file: the speed ramp asked "were we standing still?" <i>after</i> assigning the
        ///         new direction, by which point something always was, so the answer was never yes, the ramp start
        ///         stayed at zero forever and every aim was at full speed from about half a second into the
        ///         program. The gentle first tap this screen advertises had simply never worked.
        ///     </para>
        /// </summary>
        private readonly HeldAxis _driftX = new();

        private readonly HeldAxis _driftY = new();

        private bool _forceText;
        private bool _scoreRecorded;

        /// <summary>
        ///     Where the board landed in the frame the player is looking at, so a click can be turned back into a
        ///     world position. Rebuilt every time the screen is composed - see <see cref="MissileBoardMap" />.
        /// </summary>
        private MissileBoardMap _boardMap;

        /// <summary>
        ///     The last thing the mouse did, kept only so the status line can show it.
        ///     <para>
        ///         This is a diagnostic and it earns its place: whether the mouse works at all depends on the
        ///         terminal, on the console host, and on whether something else has already claimed the pointer, and
        ///         none of that can be established from inside a test. Showing the received cell means "nothing
        ///         happens when I move it" can be told apart from "it aims in the wrong place" without a debugger.
        ///     </para>
        /// </summary>
        private MouseEvent _lastPointer;

        private bool _sawPointer;

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

            // The mouse is only advertised when the host actually got one - a prompt promising a click that the
            // terminal will never report is worse than saying nothing.
            ParentWindow.PromptText = AnsiConsole.MouseEnabled
                ? "Move the mouse to aim and click to fire, or arrows/WASD and SPACE; Z/X/C picks a battery, TAB switches board, ENTER quits"
                : "Arrows or WASD aim, SPACE fires, Z/X/C picks a battery, TAB switches board, ENTER or ESC quits";

            // Asked for by the screen that wants it rather than by the host, and handed back in OnFormClosing.
            // Motion is one event for every cell the pointer crosses, so an arcade whose other games only want
            // clicks should not be paying for it while they are the ones on screen - and the arcade's own menu
            // certainly should not. See OnFormClosing for the half that makes this safe.
            SimUnit.InputManager.ReportsMouseMotion = true;

            RestartOnActivate(_frame);
            NewGame();
        }

        /// <summary>
        ///     Hands pointer reporting back.
        ///     <para>
        ///         <b>This is the counterpart to the line in <see cref="OnFormPostCreate" />, and without it the
        ///         flood outlives the screen that asked for it.</b> A form being dropped is no signal at all on its
        ///         own, which is the whole reason <c>IForm.OnFormClosing</c> exists: leaving the arcade for the menu
        ///         would leave every later screen, and the menu itself, receiving one event per cell the pointer
        ///         crosses forever after, for nothing. The library fires this from every path a form is detached by,
        ///         quitting included, so there is no way out of this screen that skips it.
        ///     </para>
        /// </summary>
        public override void OnFormClosing()
        {
            base.OnFormClosing();

            SimUnit.InputManager.ReportsMouseMotion = false;
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
        ///         you is when it was let go. So this records a heading on a <see cref="HeldAxis" />, and
        ///         <see cref="OnTick" /> integrates real elapsed time against it — the axis treating silence longer
        ///         than <see cref="HeldAxis.ReleaseAfter" /> as the key-up event that is never coming.
        ///     </para>
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            switch (key)
            {
                case ConsoleKey.LeftArrow or ConsoleKey.A:
                    _driftX.Press(-1);
                    break;
                case ConsoleKey.RightArrow or ConsoleKey.D:
                    _driftX.Press(1);
                    break;
                case ConsoleKey.UpArrow or ConsoleKey.W:
                    _driftY.Press(1);
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.S:
                    _driftY.Press(-1);
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

        /// <summary>
        ///     Aims at a world position, clamped to the field and to the floor the player may not shoot below. Both
        ///     the keyboard and the mouse come through here so the two clamps exist in exactly one place.
        /// </summary>
        /// <param name="x">Where to aim, in world units.</param>
        /// <param name="y">Where to aim, in world units.</param>
        private void SetAim(double x, double y)
        {
            _aimX = Math.Clamp(x, 0.0, MissileField.Aspect);
            _aimY = Math.Clamp(y, MissileField.MinAimY, 1.0);
        }

        /// <summary>
        ///     Everything the mouse does, and the one thing this game wanted that a click could never give it: the
        ///     crosshair follows the pointer.
        ///     <para>
        ///         <b>This is the trackball the cabinet had.</b> Aiming with presses alone means the crosshair only
        ///         ever exists where the last shot was fired, so a player lines up by firing, which spends the very
        ///         ammunition the game is about. A pointer reports an absolute position, so the sight is simply
        ///         wherever the hand is and the button is only the trigger. It needed
        ///         <c>MouseEventKindEnum.Move</c>, which did not exist when this screen was written.
        ///     </para>
        ///     <para>
        ///         <b>A move with the button still held keeps firing</b>, which is a drag and is the other thing
        ///         only the new kinds can express. <see cref="FireAt" /> is already rate limited by
        ///         <see cref="_shotPace" />, so sweeping across the sky with the button down lays down a barrage at
        ///         the same cadence a held SPACE does rather than one shell per cell crossed.
        ///     </para>
        ///     <para>
        ///         Releases are deliberately ignored. Nothing here is latched to a held button - the drag reads the
        ///         button off each move - so there is no state a release would have to unwind, and a screen that
        ///         handles an event it has no use for is a screen somebody later has to work out the purpose of.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What the mouse did, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            // Presses go the old road on purpose: OnMousePressed stays the single place a shot is decided, so the
            // click path is byte-for-byte the one that was already pinned by tests.
            if (mouse.Kind == MouseEventKindEnum.Press)
            {
                OnMousePressed(mouse);
                return;
            }

            if (mouse.Kind != MouseEventKindEnum.Move)
                return;

            _lastPointer = mouse;
            _sawPointer = true;

            if (_field.IsOver)
                return;

            // Off the board is dropped rather than clamped, the same rule a click follows: sliding the pointer up
            // over the status line must not drag the sight to the top of the sky.
            if (!_boardMap.TryToWorld(mouse.Row, mouse.Column, out var worldX, out var worldY))
                return;

            SetAim(worldX, worldY);
            _driftX.Release();
            _driftY.Release();

            if (mouse.Button == MouseButtonEnum.Left)
                FireAt(_field.BestSilo(_aimX, _aimY));
        }

        /// <summary>
        ///     A click puts the crosshair on the clicked cell and fires from whichever battery can get there first.
        ///     <para>
        ///         <b>The drift is zeroed, and that is the whole of the arbitration between mouse and keyboard.</b>
        ///         Nothing revives drift without a fresh key press and <see cref="SteerCrosshair" /> returns
        ///         immediately when both axes are zero, so the pointer wins until the player touches an arrow again
        ///         and then the arrow wins - with no mode flag and nothing to get out of step.
        ///     </para>
        ///     <para>
        ///         A click that lands off the board is <i>dropped</i> rather than clamped onto it: clamping would
        ///         turn a click on the status line into a shot at the top of the sky, which is a worse answer than
        ///         doing nothing.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            _lastPointer = mouse;
            _sawPointer = true;

            if (mouse.Button != MouseButtonEnum.Left || _field.IsOver)
                return;

            if (!_boardMap.TryToWorld(mouse.Row, mouse.Column, out var worldX, out var worldY))
                return;

            SetAim(worldX, worldY);
            _driftX.Release();
            _driftY.Release();

            FireAt(_field.BestSilo(_aimX, _aimY));

            // Deliberately no Refresh() here. The next paced frame is at most 33 ms away and will draw this, whereas
            // recomposing per click repeats the ChessDialog bug of rebuilding the screen once per input event.
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
            // Read once each: the key-up a terminal never sends is inferred inside these, so the answer is a
            // function of the clock and asking twice in one frame could in principle disagree with itself.
            var driftX = _driftX.Direction;
            var driftY = _driftY.Direction;

            if (driftX == 0 && driftY == 0)
                return;

            // The longer of the two, so a diagonal that began as a single held arrow keeps the speed it had built
            // up when the second one joins it rather than dropping back to a crawl mid-sweep.
            var moving = _driftX.HeldFor > _driftY.HeldFor ? _driftX.HeldFor : _driftY.HeldFor;
            var held = moving.TotalSeconds/_rampOver.TotalSeconds;
            var speed = SlowAim + (FastAim - SlowAim)*Math.Clamp(held, 0.0, 1.0);

            // A diagonal would otherwise be about forty per cent faster than a straight line, which is a thing
            // players notice without being able to say what is wrong.
            if (driftX != 0 && driftY != 0)
                speed /= Math.Sqrt(2.0);

            var step = speed*elapsed.TotalSeconds;
            SetAim(_aimX + driftX*step, _aimY + driftY*step);
        }

        /// <summary>Deals a fresh field.</summary>
        private void NewGame()
        {
            _field = new MissileField(SimUnit.Random);
            _aimX = MissileField.Aspect/2.0;
            _aimY = 0.62;
            _driftX.Release();
            _driftY.Release();
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
            // Both halves of "will a picture survive to this terminal at all" - virtual terminal processing, and a
            // colour mode that is not None - now live in the library, which is the only thing that knows its own
            // presenter blanks a payload row it cannot interpret.
            if (!AnsiConsole.SupportsPictures() || _forceText)
                return false;

            // Half blocks give two pixels a row, so a field squeezed into a dozen rows is a smear; real pixels only
            // need enough rows to be worth looking at. Asked of the renderer rather than type-tested against the
            // built-in classes, which gets the wrong answer for exactly the third-party renderers the seam allows.
            return BoardRenderer().DrawsTruePixels ? rows >= 12 : rows >= 26;
        }

        /// <summary>
        ///     Which renderer draws the picture.
        ///     <para>
        ///         <b>Half blocks are forced while the mouse is enabled, and that is not a shortcut.</b> Aiming with
        ///         a pointer means knowing exactly which rectangle of the screen the board occupies, and for a
        ///         true-pixel renderer nothing does. Sixel emits pixel rows the terminal draws against a character
        ///         cell of its own; <see cref="IImageRenderer.CellPixelWidth" /> and
        ///         <see cref="IImageRenderer.CellPixelHeight" /> will say what size the renderer <i>assumes</i> that
        ///         cell is, which is a real and useful answer for sizing a canvas and is still not the one needed
        ///         here - it is an assumption the terminal never confirms, so a click map built on it is wrong by
        ///         however far the two disagree, silently, and can be a quarter of the field out. Half blocks are
        ///         exactly one pixel pair per cell by definition, so the board's rectangle is the string it
        ///         returned and the map is arithmetic rather than a guess. A player who would rather have the better
        ///         picture than the mouse simply does not enable the mouse.
        ///     </para>
        /// </summary>
        /// <returns>The renderer to draw with.</returns>
        private static IImageRenderer BoardRenderer()
        {
            return AnsiConsole.MouseEnabled ? _halfBlocks : ImageRenderers.Default;
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

            var usePicture = PictureIsWorthIt(rows);
            var columns = Math.Max(20, width - 2);
            var board = usePicture ? PaintPicture(rows, width) : PaintText(columns, rows);

            // COUNTED, never written down as a constant. The library contributes exactly one un-terminated line
            // above the form - SceneGraph appends the spinner, the window label and the application's pre-render
            // text with no newline between them - so the leading AppendLine above TERMINATES that line rather than
            // making a blank one. Counting the breaks already in the builder cannot drift when any of that changes,
            // where a hardcoded 3 quietly becomes wrong and puts every shot a row out.
            var originRow = CountLineBreaks(sb);

            _boardMap = usePicture
                ? MissileBoardMap.ForPicture(originRow, 0, MeasuredColumns(board), MeasuredRows(board))
                : MissileBoardMap.ForCharacters(originRow, 0, columns, rows);

            sb.AppendLine(board);
            sb.Append(_field.Message);

            _rendered = sb.ToString();
        }

        /// <summary>How many line breaks are already in the builder, which is the row the next line will occupy.</summary>
        /// <param name="builder">The frame being composed.</param>
        /// <returns>The count.</returns>
        private static int CountLineBreaks(StringBuilder builder)
        {
            var breaks = 0;
            for (var i = 0; i < builder.Length; i++)
            {
                if (builder[i] == '\n')
                    breaks++;
            }

            return breaks;
        }

        /// <summary>How many rows a drawn board really covers.</summary>
        /// <param name="board">The board as it will be written.</param>
        /// <returns>The row count.</returns>
        private static int MeasuredRows(string board)
        {
            var rows = 1;
            foreach (var character in board)
            {
                if (character == '\n')
                    rows++;
            }

            return rows;
        }

        /// <summary>
        ///     How many columns a drawn board really covers, measured rather than assumed.
        ///     <para>
        ///         The picture is fitted with <c>Contain</c>, so it keeps the field's proportions and is usually
        ///         <i>narrower</i> than the columns it was offered - at eighty by twenty-four it draws about
        ///         fifty-eight of the seventy-nine it was allowed. Inverting a click against the offered width rather
        ///         than the drawn width is wrong by a third of the field at the right-hand edge, which is several
        ///         blast radii and looks exactly like the collision test being broken.
        ///     </para>
        /// </summary>
        /// <param name="board">The board as it will be written.</param>
        /// <returns>The visible width of its widest row.</returns>
        private static int MeasuredColumns(string board)
        {
            var widest = 0;
            foreach (var row in board.Replace("\r\n", "\n").Split('\n'))
                widest = Math.Max(widest, AnsiText.VisibleLength(row));

            return widest;
        }

        /// <summary>Draws the field as real pixels, through whatever the terminal turned out to support.</summary>
        private string PaintPicture(int rows, int columns)
        {
            // Read once and handed to both, so the canvas is sized against the very renderer that is about to
            // draw it. Sizing it for half blocks and then handing it to sixel is what magnified every stroke.
            var renderer = BoardRenderer();
            var (canvasWidth, canvasHeight) = MissileFieldArt.SizeFor(rows, renderer);

            // Rebuilt only when the terminal has actually been resized, since the buffer is reused between frames.
            if (_art == null || _art.Width != canvasWidth || _art.Height != canvasHeight)
                _art = new MissileFieldArt(canvasWidth, canvasHeight);

            var options = new AnsiImageOptions
            {
                MaxRows = rows,
                MaxColumns = Math.Max(16, columns - 1),
                RowMargin = 0
            };

            return AnsiImage.FromPixels(_art.Paint(_field, _aimX, _aimY)).ToAnsi(options, renderer);
        }

        /// <summary>
        ///     Draws the field as characters, at exactly the size the board map was told about — the two must agree
        ///     or a click lands somewhere other than where it was drawn.
        /// </summary>
        /// <param name="columns">How many columns the board covers.</param>
        /// <param name="rows">How many rows the board covers.</param>
        private string PaintText(int columns, int rows)
        {
            return MissileFieldText.Render(_field, _aimX, _aimY, columns, rows);
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

            // The pointer readout is a diagnostic, and it stays. Whether a click is reported at all depends on the
            // terminal and on the console host, neither of which any test running here can establish — so showing
            // the cell that actually arrived is what separates "the mouse is dead" from "the mouse is aiming
            // somewhere else", without anybody having to attach a debugger to find out which.
            var pointer = !AnsiConsole.MouseEnabled
                ? "  ·  Mouse off"
                : _sawPointer
                    ? string.Format(CultureInfo.InvariantCulture, "  ·  Mouse r{0}c{1}",
                        _lastPointer.Row, _lastPointer.Column)
                    : "  ·  Mouse ready";

            return string.Format(CultureInfo.InvariantCulture,
                "Wave {0}  ·  Score {1:N0}  ·  x{2}  ·  Cities {3}  ·  Ammo {4}  ·  Best {5:N0}{6}",
                _field.Wave, _field.Score, _field.Multiplier, cities, ammo, UserData.MissileCommandBestScore,
                pointer);
        }
    }
}
