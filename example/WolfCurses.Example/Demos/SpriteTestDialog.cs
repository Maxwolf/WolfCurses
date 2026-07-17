// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Bounces the DVD logo around <c>image_001.jpg</c> the way the screensaver did, until ENTER is pressed. The
    ///     basic sprite test: one picture moving over another, with the transparency that makes it a sprite rather than
    ///     a rectangle, recomposed every frame.
    ///     <para>
    ///         Where the compositing demo puts the penguin on the photograph once and keeps the answer, nothing here can
    ///         be kept — the sprite has moved by the next frame, so the scene is composed and rendered again. That is the
    ///         whole difference between a sprite and an overlay, and the only reason this demo exists separately.
    ///     </para>
    ///     <para>
    ///         <b>The working canvas is deliberately small</b>, and it is the one decision worth understanding here. The
    ///         photograph is 1280x987 and the terminal shows perhaps 78x16 characters, so composing and rendering at the
    ///         photograph's own size costs about 9.2 ms a frame — nearly all of it the renderer averaging 1.26 million
    ///         pixels down to a few dozen cells, over and over, to throw almost all of it away. The background is
    ///         therefore resized once, up front, to <see cref="CanvasWidth" /> across; the frame then costs well under a
    ///         millisecond. The canvas is still several times larger than the character grid, which is what keeps the
    ///         motion smooth: the renderer's area-averaging turns a sub-cell position into shading rather than a jump.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class SpriteTestDialog : Form<ExampleWindowInfo>
    {
        /// <summary>
        ///     Width of the canvas the sprite moves on, in pixels. Comfortably above any character grid a terminal is
        ///     going to have, and far below the photograph's own 1280 — see the class remarks for why both halves of
        ///     that matter.
        /// </summary>
        private const int CanvasWidth = 360;

        /// <summary>How wide the logo is drawn, as a fraction of the canvas. The screensaver's was about this.</summary>
        private const double LogoWidthFraction = 0.22;

        /// <summary>How long a frame lasts. Thirty a second is smooth and leaves the machine alone.</summary>
        private static readonly TimeSpan _frameLength = TimeSpan.FromMilliseconds(33);

        private readonly Stopwatch _clock = new();

        private int _bounces;
        private string _current = string.Empty;
        private int _deltaX = 2;
        private int _deltaY = 1;
        private string _error;
        private AnsiImageOptions _options;
        private SpriteScene _scene;
        private Sprite _sprite;

        /// <summary>Initializes a new instance of the <see cref="SpriteTestDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SpriteTestDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "Press ENTER to return to the menu";
            Build();
            _clock.Restart();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // On the system tick, not the simulation tick, which fires once a second: a sprite moving once a second is
            // not moving. Everything below runs at most thirty times a second regardless of how fast the host loops.
            if (_scene == null || _clock.Elapsed < _frameLength)
                return;

            _clock.Restart();
            Move();

            // Composed and rendered here rather than in OnRenderForm, which the scene graph calls on every one of those
            // thousands of ticks. This is the expensive part of the frame and it happens once per move.
            _current = _scene.ToAnsi(_options);
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            if (_error != null)
                return $"{Environment.NewLine}{_error}";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"Sprite Test (Basic)  —  DVD logo over image_001.jpg  ({_bounces} bounces)");
            sb.AppendLine();
            sb.Append(_current);
            return sb.ToString();
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>Moves the sprite one frame and turns it round at the edges.</summary>
        private void Move()
        {
            _sprite.X += _deltaX;
            _sprite.Y += _deltaY;

            // Reflecting off the wall means clamping to it as well as reversing: without the clamp a sprite that
            // overshot by a pixel would spend the next frame still outside, reverse again, and shiver against the edge
            // instead of leaving it.
            var maxX = _scene.Width - _sprite.Width;
            var maxY = _scene.Height - _sprite.Height;

            if (_sprite.X <= 0 || _sprite.X >= maxX)
            {
                _sprite.X = Math.Clamp(_sprite.X, 0, Math.Max(0, maxX));
                _deltaX = -_deltaX;
                _bounces++;
            }

            if (_sprite.Y <= 0 || _sprite.Y >= maxY)
            {
                _sprite.Y = Math.Clamp(_sprite.Y, 0, Math.Max(0, maxY));
                _deltaY = -_deltaY;
                _bounces++;
            }
        }

        /// <summary>Loads the photograph and the logo, sizes both to the working canvas, and builds the scene.</summary>
        private void Build()
        {
            var backgroundPath = Path.Combine(DemoImages.Folder, "image_001.jpg");
            var logoPath = Path.Combine(DemoImages.Folder, "dvd_logo.png");

            if (!File.Exists(backgroundPath) || !File.Exists(logoPath))
            {
                _error = "image_001.jpg and dvd_logo.png are both needed for this demo and one is missing." +
                         Environment.NewLine + $"Looked in {DemoImages.Folder}.";
                return;
            }

            // Both of these hand back the magenta error texture rather than throwing if the file is unreadable, so a
            // broken image shows as a broken image and the demo still runs.
            var background = AnsiImage.FromFile(backgroundPath);
            var logo = AnsiImage.FromFile(logoPath);

            // Once, up front: the whole point of the working canvas.
            var canvasHeight = Math.Max(1, background.Height * CanvasWidth / background.Width);
            var logoWidth = Math.Max(1, (int) (CanvasWidth * LogoWidthFraction));
            var logoHeight = Math.Max(1, logo.Height * logoWidth / logo.Width);

            _scene = new SpriteScene(background.Resize(CanvasWidth, canvasHeight).Pixels);

            // Anywhere but a corner, which is where the whole joke ends rather than starts.
            _sprite = new Sprite(logo.Resize(logoWidth, logoHeight).Pixels, CanvasWidth / 3, canvasHeight / 4);
            _scene.Sprites.Add(_sprite);

            _options = DemoImages.FitOptions();
            _current = _scene.ToAnsi(_options);
        }
    }
}
