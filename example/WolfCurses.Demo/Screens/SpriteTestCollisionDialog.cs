// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     Two penguins on a photograph. The arrow keys drive one of them; walk it into the other and a message box asks
    ///     whether they should kiss. The collision test.
    ///     <para>
    ///         What is actually under test is <see cref="SpriteScene.SpritesTouching" /> — knowing <i>which</i> sprite
    ///         ran into which, rather than merely that something did, since that is what a game needs in order to care.
    ///         Boxes rather than pixels, so the penguins collide at the edge of their rectangles and not at the edge of
    ///         the birds; see <see cref="Sprite.Intersects" /> for why that bargain is the usual one.
    ///     </para>
    ///     <para>
    ///         <b>The trigger is the edge, not the state.</b> Touching is true for as long as they overlap, which is
    ///         hundreds of frames, and asking the question on all of them would be a message box that cannot be
    ///         dismissed. So the prompt fires on the frame where "not touching" becomes "touching" and not again until
    ///         they have come apart. Walk away and walk back to ask twice. This is the whole difference between a level
    ///         and an edge and it is where collision code usually goes wrong first.
    ///     </para>
    ///     <para>
    ///         Pausing needs no work at all, which is worth noticing: the message box is a window, it goes on top, and
    ///         only the focused window is ticked — so this form stops moving because it stops being asked to. When the
    ///         box is dismissed this window is focused again, <see cref="OnFormActivate" /> fires, and the frame clock is
    ///         restarted so the first frame back does not think it has been away for however long the joke took to read.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class SpriteTestCollisionDialog : Form<DemoWindowInfo>
    {
        /// <summary>Width of the canvas the penguins move on, in pixels. See the basic sprite test for why it is small.</summary>
        private const int CanvasWidth = 360;

        /// <summary>How wide a penguin is drawn, as a fraction of the canvas.</summary>
        private const double PenguinWidthFraction = 0.2;

        /// <summary>How far an arrow key moves the penguin, in canvas pixels.</summary>
        private const int StepSize = 6;

        /// <summary>What the message box asks when they meet.</summary>
        private const string KissQuestion = "Sprites touched! Should they kiss? Y/N";

        /// <summary>How long a frame lasts.</summary>
        private static readonly TimeSpan _frameLength = TimeSpan.FromMilliseconds(33);

        private readonly IntervalTimer _frame = new(_frameLength);
        private readonly FrameCounter _counter = new();
        private readonly RendererSwitch _renderer = new();

        private string _current = string.Empty;

        /// <summary>The whole frame, built on the paced tick and handed back unchanged by every render between.</summary>
        private string _frameText = string.Empty;

        private string _error;
        private AnsiImageOptions _options;
        private Sprite _partner;
        private Sprite _player;
        private string _reaction = "Use the ARROW KEYS to walk the left penguin into the right one.";
        private SpriteScene _scene;
        private int _touches;

        /// <summary>Whether they were touching last frame, which is the whole of the edge trigger.</summary>
        private bool _wasTouching;

        /// <summary>
        ///     Where the picture landed in the frame the viewer is looking at, measured from what the renderer
        ///     actually returned rather than assumed. Rebuilt every time the scene is composed, the same shape
        ///     Missile Command's board map takes.
        /// </summary>
        private int _pictureTopRow = -1;

        private int _pictureColumns;
        private int _pictureRows;

        /// <summary>
        ///     Whether a cell of the picture can be turned back into a canvas pixel at all.
        ///     <para>
        ///         <b>Only half blocks can.</b> They are one pixel per column and two per row by definition, so the
        ///         drawn rectangle is exactly the string that came back. A sixel or kitty payload is one row of
        ///         escapes of no visible width that paints across many rows, so there is nothing to measure - and
        ///         the renderer's own <c>CellPixelWidth</c> is what it <i>assumes</i> the terminal's cell is, never
        ///         a measurement of it. TAB already switches renderers on this screen, so the honest answer is to
        ///         say which one can be dragged rather than to guess a rectangle.
        ///     </para>
        /// </summary>
        private bool _draggable;

        /// <summary>Which penguin is being dragged, and where inside it the pointer took hold.</summary>
        private Sprite _dragging;

        private int _grabX;
        private int _grabY;

        /// <summary>Initializes a new instance of the <see cref="SpriteTestCollisionDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SpriteTestCollisionDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = AnsiConsole.MouseEnabled
                ? "Drag a penguin with the mouse, or arrow keys move the left one; TAB switches renderer, ENTER/ESC for the menu"
                : "Arrow keys to move, TAB to switch renderer, ENTER/ESC for the menu";

            // Asked for by the screen that wants it, and handed back in OnFormClosing. Motion is one event for
            // every cell the pointer crosses, so the other demos should not be paying for it while they are the
            // ones on screen.
            SimUnit.InputManager.ReportsMouseMotion = true;

            Build();
            RestartOnActivate(_frame);
        }

        /// <summary>
        ///     Hands pointer reporting back, which is the half a form has nowhere else to put.
        ///     <para>
        ///         Being dropped is no signal at all, which is why <c>IForm.OnFormClosing</c> exists. Without this
        ///         every later demo, and the menu itself, would go on receiving one event per cell the pointer
        ///         crossed for the rest of the session, for nothing. The library fires it from every path a form is
        ///         detached by, so ESC, ENTER, the window being removed and quitting are all covered by the one
        ///         override.
        ///     </para>
        /// </summary>
        public override void OnFormClosing()
        {
            base.OnFormClosing();

            SimUnit.InputManager.ReportsMouseMotion = false;
        }

        /// <inheritdoc />
        public override void OnFormActivate()
        {
            // Back from the message box, and both clocks have been running the whole time it was up. The frame timer
            // is restarted by the base call, because it was registered above — without that the next frame thinks it
            // has been away for however long the reading took. The counter is this app's own type and has to be told
            // separately: without it the fps readout divides the frames from before the box by the time spent reading
            // it and reports something like 3, which measures the reader rather than anything this code did.
            base.OnFormActivate();

            _counter.Restart();

            // Whatever was being dragged is dropped. The message box is a separate window and it takes focus, so
            // the RELEASE that ended the drag was delivered to the box and this form never saw it - the penguin
            // would otherwise still be stuck to the pointer when the box is dismissed. That is the trap in any
            // drag built on a screen that can be interrupted, and it is why a drag needs both a release and a
            // second way of being told it is over.
            _dragging = null;
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (_scene == null)
                return;

            // An arrow key has no character, so it never reaches the input buffer and can only arrive here. Note this is
            // not called at all while the message box is up: that window is the focused one, and key presses go to
            // whoever has focus.
            switch (key)
            {
                case ConsoleKey.Tab:
                    // See the basic sprite test for why TAB and why the sample is discarded.
                    _renderer.Toggle();
                    _counter.Restart();
                    return;
                case ConsoleKey.LeftArrow:
                    _player.X -= StepSize;
                    break;
                case ConsoleKey.RightArrow:
                    _player.X += StepSize;
                    break;
                case ConsoleKey.UpArrow:
                    _player.Y -= StepSize;
                    break;
                case ConsoleKey.DownArrow:
                    _player.Y += StepSize;
                    break;
                default:
                    return;
            }

            // Kept on the canvas. A sprite is clipped rather than refused, so walking off the edge would be legal and
            // would simply lose the penguin.
            _player.X = Math.Clamp(_player.X, 0, Math.Max(0, _scene.Width - _player.Width));
            _player.Y = Math.Clamp(_player.Y, 0, Math.Max(0, _scene.Height - _player.Height));
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (_scene == null || !_frame.TryConsume())
                return;

            CheckCollision();

            var started = Stopwatch.GetTimestamp();
            _current = _scene.ToAnsi(_options, _renderer.Current);
            _counter.Record(Stopwatch.GetElapsedTime(started));

            Compose();
        }

        /// <summary>
        ///     Builds the whole frame, and notes which row the picture starts on while it is building it.
        ///     <para>
        ///         <b>The row is counted from the builder, never written down as a constant.</b> A constant is
        ///         right until somebody adds a line to the header, at which point every grab lands one row out and
        ///         nothing anywhere reports a problem. Counting the breaks already in the builder cannot drift.
        ///     </para>
        ///     <para>
        ///         Composing here rather than in <c>OnRenderForm</c> is the other half: the scene graph calls that
        ///         method on every system tick, about a thousand times a second, and the picture only changes
        ///         thirty times a second. It also means the render is a pure read, so nothing measures anything
        ///         from inside it.
        ///     </para>
        /// </summary>
        private void Compose()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Sprite Test (Collision)  —  arrow keys move the left penguin, or drag either one");

            // Said out loud rather than left to be discovered, because "the drag does nothing" and "this terminal
            // has no mouse" look identical from the other side of the screen. A true-pixel picture is one payload
            // row of no measurable width, so there is no rectangle to hit-test and TAB is the way to get one.
            var drag = !AnsiConsole.MouseEnabled
                ? "no mouse"
                : _draggable
                    ? "drag: on"
                    : "drag: needs half blocks (TAB)";

            sb.AppendLine($"{_counter.Describe()} | {_renderer.Describe()} | {drag} | " +
                          $"{(_wasTouching ? "TOUCHING" : "apart")} | {_touches} touches");
            sb.AppendLine(_reaction);

            Measure(CountLineBreaks(sb));

            sb.Append(_current);
            _frameText = sb.ToString();
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

        /// <summary>
        ///     Records where the picture really landed, so a pointer can be turned back into a canvas pixel.
        ///     <para>
        ///         <b>Measured from what the renderer returned rather than from what it was asked for.</b> The fit
        ///         keeps the canvas's proportions, so the picture is usually narrower than the columns it was
        ///         offered; inverting a click against the offered width instead is wrong by more the further right
        ///         the pointer is, which reads as the drag being broken rather than as the arithmetic being wrong.
        ///     </para>
        /// </summary>
        private void Measure(int topRow)
        {
            _pictureTopRow = topRow;
            _pictureRows = 0;
            _pictureColumns = 0;
            _draggable = false;

            if (string.IsNullOrEmpty(_current))
                return;

            var rows = _current.Replace("\r\n", "\n").Split('\n');

            // A true-pixel payload is one row of escapes of no visible width followed by placeholder rows, so there
            // is no rectangle here to hit-test. The library publishes the question rather than leaving callers to
            // know that the marker sits at index zero.
            if (rows.Length == 0 || AnsiGraphics.IsPictureRow(rows[0]))
                return;

            foreach (var row in rows)
                _pictureColumns = Math.Max(_pictureColumns, AnsiText.VisibleLength(row));

            _pictureRows = rows.Length;
            _draggable = _pictureColumns > 1 && _pictureRows > 1;
        }

        /// <summary>
        ///     Picking a penguin up, dragging it and putting it down, which is the whole of what the pointer adds
        ///     here and is not expressible in presses however many arrive.
        ///     <para>
        ///         <b>A drag is three different events.</b> The press says which sprite was taken hold of and
        ///         where inside it, every move carrying that button says where it has got to, and the release
        ///         says it is over.
        ///     </para>
        ///     <para>
        ///         <b>What actually ends it is the button test on the move, not the release</b>, and that is
        ///         worth knowing rather than glossing: a move arrives with whichever buttons are still held, so
        ///         a hover already fails the test and the sprite is put down whether a release was seen or not.
        ///         Handling the release is belt and braces - which is exactly why it survived being mutated
        ///         away under this screen's own tests. It stays because it says what the code means, and
        ///         because the self-healing half is the thing that saves a drag interrupted by a modal, where
        ///         the release is delivered to the message box and this form never sees it at all.
        ///     </para>
        ///     <para>
        ///         Miss the grab offset, though, and the sprite jumps so its corner sits under the pointer the
        ///         moment you touch it. That one has no second line of defence.
        ///     </para>
        ///     <para>
        ///         The sprite under the pointer is found by asking the scene rather than by comparing rectangles
        ///         here: a one-pixel probe sprite is offered to <c>SpriteScene.SpritesTouching</c>, which answers in
        ///         draw order, so the LAST one is the nearest. The probe is never added to the scene, which that
        ///         method explicitly allows, so a position can be tried without anything moving there.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What the mouse did, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (_scene == null || !_draggable)
                return;

            if (mouse.Kind == MouseEventKindEnum.Release)
            {
                _dragging = null;
                return;
            }

            if (mouse.Kind != MouseEventKindEnum.Move || _dragging == null ||
                mouse.Button != MouseButtonEnum.Left)
                return;

            if (!ToCanvas(mouse.Row, mouse.Column, out var canvasX, out var canvasY))
                return;

            _dragging.X = Math.Clamp(canvasX - _grabX, 0, Math.Max(0, _scene.Width - _dragging.Width));
            _dragging.Y = Math.Clamp(canvasY - _grabY, 0, Math.Max(0, _scene.Height - _dragging.Height));
        }

        /// <summary>
        ///     Takes hold of whichever penguin is under the pointer, which is where a drag begins.
        ///     <para>
        ///         <b>This has to be its own override and cannot live in <see cref="OnMouseEvent" />.</b>
        ///         <c>Window.OnMouseEvent</c> hands a press to <c>OnMousePressed</c> and returns, so a press never
        ///         reaches the form's <c>OnMouseEvent</c> at all - which means a grab written there compiles, runs
        ///         never, and leaves a drag that silently does nothing. That is the same shape as the library's own
        ///         note about <c>case ConsoleKey.Enter:</c> being dead code in an override, and it cost a red test
        ///         here before it cost anything else.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (_scene == null || !_draggable || mouse.Button != MouseButtonEnum.Left)
                return;

            if (!ToCanvas(mouse.Row, mouse.Column, out var canvasX, out var canvasY))
                return;

            // Asked of the scene rather than answered by comparing rectangles here. Last rather than first, because
            // the scene answers in draw order, so the last one is the nearest and picking the first would take hold
            // of whatever is buried under the penguin that was pointed at. The probe is never added to the scene,
            // which SpriteScene.SpritesTouching explicitly allows.
            var probe = new Sprite(new PixelBuffer(1, 1), canvasX, canvasY);
            _dragging = _scene.SpritesTouching(probe).LastOrDefault();

            if (_dragging == null)
                return;

            // Where inside the sprite the pointer took hold, so it does not jump to put its corner under the
            // pointer the moment you touch it.
            _grabX = canvasX - _dragging.X;
            _grabY = canvasY - _dragging.Y;
        }

        /// <summary>Turns a screen cell into a canvas pixel, or answers false when that cell is not on the picture.</summary>
        /// <param name="row">Screen row.</param>
        /// <param name="column">Screen column.</param>
        /// <param name="canvasX">Where that lands on the canvas.</param>
        /// <param name="canvasY">Where that lands on the canvas.</param>
        /// <returns>TRUE when the cell was on the picture.</returns>
        private bool ToCanvas(int row, int column, out int canvasX, out int canvasY)
        {
            canvasX = 0;
            canvasY = 0;

            var cy = row - _pictureTopRow;
            if (cy < 0 || cy >= _pictureRows || column < 0 || column >= _pictureColumns)
                return false;

            // The centre of the cell, because a cell of a half-block picture covers a BAND of canvas pixels rather
            // than sampling one. Taking the near edge instead puts every grab half a cell high and left.
            canvasX = (int) ((column + 0.5)/_pictureColumns*_scene.Width);
            canvasY = (int) ((cy + 0.5)/_pictureRows*_scene.Height);
            return true;
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            if (_error != null)
                return $"{Environment.NewLine}{_error}";

            // A pure read of what the paced tick built. This runs about a thousand times a second.
            return _frameText;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Asks the scene what the player is touching, and puts the question up on the frame it becomes true.
        /// </summary>
        private void CheckCollision()
        {
            // The scene is asked what is touching the player rather than the two sprites being compared by hand, because
            // naming the thing that was hit is the entire point: with a dozen sprites the answer is which one, not
            // whether.
            var hit = _scene.SpritesTouching(_player).FirstOrDefault();
            var touching = hit != null;

            if (touching && !_wasTouching)
            {
                _touches++;
                Ask();
            }

            _wasTouching = touching;
        }

        /// <summary>Puts the question up, which pauses everything by taking the focus away.</summary>
        private void Ask()
        {
            // ConsoleSimulationApp.Instance is how a form reaches the simulation: SimUnit is the window's, and a form is
            // handed only its parent window. The demo already leans on this singleton in Program.Main.
            MessageBox.Show(
                ConsoleSimulationApp.Instance,
                KissQuestion,
                MessageBoxButtonsEnum.YesNo,
                result =>
                {
                    _reaction = result == MessageBoxResultEnum.Yes
                        ? "They fell in love."
                        : "It was never meant to be.";

                    // Nothing else to do: dismissing the box removes its window, this one is focused again, and
                    // OnFormActivate has already restarted the clock by the time anything moves.
                });
        }

        /// <summary>Loads the photograph and the penguins and puts them a walk apart.</summary>
        private void Build()
        {
            var backgroundPath = Path.Combine(DemoImages.Folder, "image_002.jpg");

            if (!File.Exists(backgroundPath) || !File.Exists(DemoImages.PenguinPath))
            {
                _error = $"image_002.jpg and {DemoImages.PenguinFileName} are both needed for this demo and one is " +
                         "missing." + Environment.NewLine + $"Looked in {DemoImages.Folder}.";
                return;
            }

            var background = AnsiImage.FromFile(backgroundPath);
            var penguin = AnsiImage.FromFile(DemoImages.PenguinPath);

            var canvasHeight = Math.Max(1, background.Height * CanvasWidth / background.Width);
            var penguinWidth = Math.Max(2, (int) (CanvasWidth * PenguinWidthFraction));
            var penguinHeight = Math.Max(2, penguin.Height * penguinWidth / penguin.Width);

            _scene = new SpriteScene(background.Resize(CanvasWidth, canvasHeight).Pixels);

            // Both penguins share one picture, which is allowed and worth doing: a Sprite holds a reference to its
            // image and never writes to it, so two sprites of the same thing cost one of them.
            var image = penguin.Resize(penguinWidth, penguinHeight).Pixels;
            var middle = Math.Max(0, (canvasHeight - penguinHeight) / 2);

            _player = new Sprite(image, 0, middle);
            _partner = new Sprite(image, Math.Max(0, CanvasWidth - penguinWidth), middle);

            _scene.Sprites.Add(_partner);
            _scene.Sprites.Add(_player);

            _options = DemoImages.FitOptions();
            _current = _scene.ToAnsi(_options, _renderer.Current);
        }
    }
}
