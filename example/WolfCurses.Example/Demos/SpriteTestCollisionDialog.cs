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

namespace WolfCurses.Example.Demos
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
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class SpriteTestCollisionDialog : Form<ExampleWindowInfo>
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

        private readonly Stopwatch _clock = new();
        private readonly FrameCounter _counter = new();

        private string _current = string.Empty;
        private string _error;
        private AnsiImageOptions _options;
        private Sprite _partner;
        private Sprite _player;
        private string _reaction = "Use the ARROW KEYS to walk the left penguin into the right one.";
        private SpriteScene _scene;
        private int _touches;

        /// <summary>Whether they were touching last frame, which is the whole of the edge trigger.</summary>
        private bool _wasTouching;

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

            ParentWindow.PromptText = "Arrow keys to move, ENTER to return to the menu";
            Build();
            _clock.Restart();
        }

        /// <inheritdoc />
        public override void OnFormActivate()
        {
            base.OnFormActivate();

            // Back from the message box, and both clocks have been running the whole time it was up. Without the first
            // line the next frame thinks it has been away for however long the reading took; without the second the fps
            // readout divides the frames from before the box by the time spent reading it and reports something like 3,
            // which is a measurement of the reader rather than of anything this code did.
            _clock.Restart();
            _counter.Restart();
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

            if (_scene == null || _clock.Elapsed < _frameLength)
                return;

            _clock.Restart();
            CheckCollision();

            var started = Stopwatch.GetTimestamp();
            _current = _scene.ToAnsi(_options);
            _counter.Record(Stopwatch.GetElapsedTime(started));
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            if (_error != null)
                return $"{Environment.NewLine}{_error}";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Sprite Test (Collision)  —  arrow keys move the left penguin");
            sb.AppendLine($"{_counter.Describe()} | {(_wasTouching ? "TOUCHING" : "apart")} | " +
                          $"{_touches} touches | {_scene.Width}x{_scene.Height} canvas");
            sb.AppendLine(_reaction);
            sb.Append(_current);
            return sb.ToString();
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
            // handed only its parent window. The example already leans on this singleton in Program.Main.
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
            _current = _scene.ToAnsi(_options);
        }
    }
}
