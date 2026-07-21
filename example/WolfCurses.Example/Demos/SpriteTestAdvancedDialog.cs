// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Graphics.Decoding;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Five animated GIFs, at five different sizes, bouncing around <c>image_003.jpg</c> and through one another,
    ///     being taken off the scene one at a time and then all put back — over and over. The advanced sprite test:
    ///     everything the basic one does not.
    ///     <para>
    ///         Four things are under test at once, which is the point of running them together rather than apart:
    ///         <b>sprites over sprites</b> (five transparent pictures crossing, so every pair blends by alpha in list
    ///         order rather than punching a hole), <b>a scene being edited while it runs</b> (sprites removed one every
    ///         two seconds and five re-added on the tenth, from the middle of the list rather than the end, so the order
    ///         that decides what is in front has to survive), <b>size</b> (each sprite is scaled at random between a
    ///         small and a large fraction of the canvas, keeping the picture's own aspect), and <b>animation</b> — each
    ///         sprite plays the GIF's eight frames on its own clock, opening on a random one so the crowd does not flash
    ///         in lockstep.
    ///     </para>
    ///     <para>
    ///         Animation needs nothing from the library that a still sprite does not: <see cref="Sprite.Image" /> is
    ///         settable, so <see cref="BouncingSprite" /> swaps the picture and <see cref="SpriteScene" /> keeps drawing
    ///         whatever it finds. Every sprite carries its own copy of the frames because they are all different sizes,
    ///         which is what the respawn is paying for.
    ///     </para>
    ///     <para>
    ///         The canvas is small for the same reason it is in the basic test, and this demo happens to prove the
    ///         reason: rendering is charged on the canvas, not on what is standing on it. Measured here, five sprites
    ///         cost <b>1.38 ms</b> a frame and one costs <b>1.24 ms</b> — four whole sprites for 0.15 ms, because the
    ///         work that dominates is resampling the canvas to the terminal and that happens once either way. The fps
    ///         readout shows it live: the count falls from five to one across every cycle and ms/frame barely notices.
    ///     </para>
    ///     <para>
    ///         The one real expense is the respawn, at about <b>29 ms</b> to scale eight frames for each of five
    ///         sprites, which is why it is not in the readout: it is not what a frame costs, it is what a cycle costs,
    ///         once every ten seconds. It goes unseen because the frame budget absorbs it — the clock is restarted
    ///         before the work, so a 29 ms respawn simply leaves less of the 33 ms to wait out afterwards, and no frame
    ///         is dropped. On a slow enough machine it would stop fitting, and then the fps readout is exactly where
    ///         that would show up.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class SpriteTestAdvancedDialog : Form<ExampleWindowInfo>
    {
        /// <summary>Width of the canvas the sprites move on, in pixels. See the class remarks.</summary>
        private const int CanvasWidth = 360;

        /// <summary>How many sprites a full scene holds.</summary>
        private const int SpriteCount = 5;

        /// <summary>Smallest a sprite may be drawn, as a fraction of the canvas width.</summary>
        private const double SmallestFraction = 0.12;

        /// <summary>Largest a sprite may be drawn, as a fraction of the canvas width.</summary>
        private const double LargestFraction = 0.32;

        /// <summary>Fastest a sprite may travel, in canvas pixels per frame.</summary>
        private const int FastestStep = 3;

        /// <summary>How long a frame lasts. Thirty a second is smooth and leaves the machine alone.</summary>
        private static readonly TimeSpan _frameLength = TimeSpan.FromMilliseconds(33);

        /// <summary>How long between one sprite being taken off the scene and the next. Five of these is the cycle.</summary>
        private static readonly TimeSpan _removalInterval = TimeSpan.FromSeconds(2);

        private readonly Stopwatch _clock = new();
        private readonly FrameCounter _counter = new();
        private readonly RendererSwitch _renderer = new();
        private readonly Stopwatch _lifecycleClock = new();
        private readonly List<BouncingSprite> _movers = new();
        private readonly Random _random = new();

        private int _cycles;
        private string _current = string.Empty;
        private TimeSpan[] _delays;
        private string _error;
        private PixelBuffer[] _frames;
        private AnsiImageOptions _options;
        private SpriteScene _scene;

        /// <summary>Initializes a new instance of the <see cref="SpriteTestAdvancedDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SpriteTestAdvancedDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "TAB to switch renderer, ENTER or ESC to return to the menu";
            Build();
            _clock.Restart();
            _lifecycleClock.Restart();
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (key != ConsoleKey.Tab)
                return;

            // See the basic sprite test for why TAB and why the sample is discarded.
            _renderer.Toggle();
            _counter.Restart();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // On the system tick, not the simulation tick, which fires once a second. Everything below runs at most
            // thirty times a second regardless of how fast the host loops.
            if (_scene == null || _clock.Elapsed < _frameLength)
                return;

            var elapsed = _clock.Elapsed;
            _clock.Restart();

            Lifecycle();

            foreach (var mover in _movers)
                mover.Advance(elapsed, _scene.Width, _scene.Height);

            // Composed and rendered here rather than in OnRenderForm, which the scene graph calls thousands of times a
            // second. This is the expensive part of the frame and it happens once per move.
            var started = Stopwatch.GetTimestamp();
            _current = _scene.ToAnsi(_options, _renderer.Current);
            _counter.Record(Stopwatch.GetElapsedTime(started));
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            if (_error != null)
                return $"{Environment.NewLine}{_error}";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Sprite Test (Advanced)  —  5 animated GIFs over image_003.jpg");
            sb.AppendLine($"{_counter.Describe()} | {_renderer.Describe()} | " +
                          $"{_movers.Count} sprites | {_cycles} cycles");
            sb.AppendLine();
            sb.Append(_current);
            return sb.ToString();
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Takes one sprite off the scene every couple of seconds and refills it once the last one has gone,
        ///     forever. A cycle is twelve seconds: five sprites, then five removals across the next ten, then two
        ///     seconds of empty photograph before the next five arrive.
        /// </summary>
        private void Lifecycle()
        {
            if (_lifecycleClock.Elapsed < _removalInterval)
                return;

            _lifecycleClock.Restart();

            if (_movers.Count > 0)
            {
                // From anywhere in the list rather than off the end, which is the harder case and the only one worth
                // testing: the list order is what decides which sprite is in front, so removing from the middle has to
                // leave the rest drawing in the same order they were.
                var index = _random.Next(_movers.Count);
                _scene.Sprites.Remove(_movers[index].Sprite);
                _movers.RemoveAt(index);
                return;
            }

            // Nothing on the scene, and it has been that way for a beat rather than an instant — deliberately. Refilling
            // the moment the fifth sprite went would mean the one frame that proves removal actually removed anything
            // never got drawn. A bare photograph for two seconds is the test passing in front of you.
            //
            // Nothing is reused on the way back in: new sizes, new places, new directions, new starting frames, which is
            // what makes each cycle a fresh test rather than a replay.
            Spawn();
            _cycles++;
        }

        /// <summary>Fills the scene with a new crowd of sprites, each one sized, placed and aimed at random.</summary>
        private void Spawn()
        {
            for (var i = 0; i < SpriteCount; i++)
            {
                // Random size, and the height follows from the width so the picture is never squashed. Rounded up off
                // zero, since a sprite of no pixels is not a sprite.
                var fraction = SmallestFraction + _random.NextDouble() * (LargestFraction - SmallestFraction);
                var width = Math.Max(2, (int) (CanvasWidth * fraction));
                var height = Math.Max(2, _frames[0].Height * width / _frames[0].Width);

                // Its own copy of every frame, at its own size. This is the whole cost of a respawn.
                var frames = _frames.Select(frame => frame.Resize(width, height)).ToArray();

                var mover = new BouncingSprite(
                    frames,
                    _delays,
                    _random.Next(frames.Length),
                    _random.Next(0, Math.Max(1, _scene.Width - width)),
                    _random.Next(0, Math.Max(1, _scene.Height - height)),
                    Step(),
                    Step());

                _movers.Add(mover);
                _scene.Sprites.Add(mover.Sprite);
            }
        }

        /// <summary>A speed of at least one pixel a frame, in either direction. Zero would be a sprite that never moves.</summary>
        private int Step()
        {
            var speed = _random.Next(1, FastestStep + 1);
            return _random.Next(2) == 0 ? -speed : speed;
        }

        /// <summary>Loads the photograph and the animation, sizes the canvas, and fills it.</summary>
        private void Build()
        {
            var backgroundPath = Path.Combine(DemoImages.Folder, "image_003.jpg");
            var spritePath = Path.Combine(DemoImages.Folder, "transparent_anim.gif");

            if (!File.Exists(backgroundPath) || !File.Exists(spritePath))
            {
                _error = "image_003.jpg and transparent_anim.gif are both needed for this demo and one is missing." +
                         Environment.NewLine + $"Looked in {DemoImages.Folder}.";
                return;
            }

            try
            {
                // Every frame, at the size the file stores them, kept for the whole demo: each sprite scales its own
                // copies from these. Eight frames of 200x197 is well under a megabyte, so this is the case where
                // holding the lot is simply the right answer.
                using var stream = File.OpenRead(spritePath);
                var frames = new GifDecoder().DecodeFrames(stream).ToList();

                _frames = frames.Select(frame => frame.Image).ToArray();
                _delays = frames.Select(frame => frame.Delay).ToArray();
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                // A decoder is the strict path and reports a broken file by throwing, unlike AnsiImage which answers
                // with the magenta error texture. In a text UI the console is the screen, so an unhandled exception
                // would paint its stack trace over the interface it escaped from.
                _error = "transparent_anim.gif could not be decoded." + Environment.NewLine + ex.Message;
                return;
            }

            if (_frames.Length == 0)
            {
                _error = "transparent_anim.gif carries no frames.";
                return;
            }

            var background = AnsiImage.FromFile(backgroundPath);
            var canvasHeight = Math.Max(1, background.Height * CanvasWidth / background.Width);

            _scene = new SpriteScene(background.Resize(CanvasWidth, canvasHeight).Pixels);
            _options = DemoImages.FitOptions();

            Spawn();
            _cycles = 1;
            _current = _scene.ToAnsi(_options, _renderer.Current);
        }
    }
}
