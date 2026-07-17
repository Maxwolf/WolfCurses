// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Graphics.Decoding;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Plays <c>media/animated.gif</c> on a loop, at the speed the file asks for, until ENTER is pressed. The test
    ///     rig for animated GIF decoding: 91 frames, of which 83 are stored as a small rectangle of whatever moved, so
    ///     anything wrong with how frames are composited onto one another shows up here immediately and obviously.
    ///     <para>
    ///         This is deliberately not a <see cref="SlideshowFormBase" />, though it looks like one. That base advances
    ///         a frame per simulation tick, which is fixed at one second — fine for showing a folder of photographs and
    ///         thirty times too slow for an animation. This one keeps its own clock and advances on real elapsed time.
    ///         It can, because <c>SceneGraph</c> re-renders on every system tick (thousands a second here) and raises
    ///         its redraw event whenever the text it gets back has changed, so a form is free to change its mind about
    ///         what it looks like as often as it likes.
    ///     </para>
    ///     <para>
    ///         <b>Every frame is rendered to its ANSI string up front and the pixels are thrown away, which is the only
    ///         shape that fits in memory.</b> The obvious alternative — decode once, keep the frames, render whichever
    ///         is showing — sounds cheaper and is not: a frame is the whole 540x540 screen (it has to be; see
    ///         <see cref="GifFrame.Image" />), so holding all 91 costs about 106 MB, for a file of under 4 MB. Rendered
    ///         to half blocks and dropped, the same 91 frames come to a couple of MB of text in about half a second.
    ///         The decoder hands frames over one at a time for exactly this reason, so the pixels never pile up. The
    ///         cost lands instead on whoever draws real pixels — at a 200x50 terminal about 41 MB of sixel, which
    ///         grows with the window, or about 135 MB of kitty, which does not: kitty transmits the source's own
    ///         540x540 pixels and lets the terminal scale, so its cost is capped by the file rather than the screen.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class AnimatedGifDialog : Form<ExampleWindowInfo>
    {
        /// <summary>
        ///     Slowest a frame may be declared before it is taken to mean "as fast as you can" instead.
        ///     <para>
        ///         GIF counts delays in hundredths of a second and an enormous number of real files say zero, which
        ///         meant "flat out" to a browser in 1998 and would mean "spin the CPU redrawing" here. Every browser
        ///         imposes a floor and none of them agree on it; this is the common one, applied to the 0 and 1
        ///         hundredth cases the rule has always been about. It lives here rather than in the decoder because it
        ///         is a decision about playing an animation, and a decoder cannot see one being played.
        ///     </para>
        /// </summary>
        private static readonly TimeSpan _fastFrameThreshold = TimeSpan.FromMilliseconds(20);

        /// <summary>What a frame declaring no meaningful delay is shown for instead.</summary>
        private static readonly TimeSpan _fastFrameDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>Measures real time between frames, rather than counting ticks of unknown length.</summary>
        private readonly Stopwatch _clock = new();

        /// <summary>
        ///     The same readout the sprite tests carry, and it says something different here — which is the useful part.
        ///     <para>
        ///         <b>ms/frame reads essentially zero, and that is the result rather than a fault.</b> Showing a frame
        ///         costs an array lookup, because every one of them was turned into its string up front (the cached-in
        ///         figure beside it is where that time went). The sprite tests compose and render on every frame and pay
        ///         about a millisecond each time; the contrast between the two readouts is the whole argument for
        ///         pre-rendering when the frames are known in advance.
        ///     </para>
        ///     <para>
        ///         Which leaves <b>fps as a pure measure of pacing</b>, and the only number here worth watching: the
        ///         file asks for 30ms a frame, so anything under about 33 fps means playback is not keeping up and the
        ///         animation is running slow. On Windows it usually is, slightly — see <see cref="FrameCounter" /> for
        ///         why the host loop cannot be relied on to come back round in under fifteen milliseconds.
        ///     </para>
        /// </summary>
        private readonly FrameCounter _counter = new();

        /// <summary>
        ///     TAB switches renderer here as it does in the sprite tests, but it costs something quite different, and
        ///     the difference is the point.
        ///     <para>
        ///         A sprite test changes renderer between one frame and the next, because it draws every frame anyway.
        ///         This one drew all ninety-one of them before it started, so switching means drawing them all again —
        ///         about half a second into half blocks, and <b>about two seconds</b> into sixel (down from seven
        ///         before the 2026-07-17 renderer rework). The wait is the entire cost of true pixels here, paid once
        ///         at the door, and afterwards playback is an array lookup either way. Watch the "cached in" figure
        ///         rather than the fps: fps will read about 32 whichever is chosen, which is exactly the finding.
        ///     </para>
        /// </summary>
        private readonly RendererSwitch _renderer = new();

        private TimeSpan[] _delays = Array.Empty<TimeSpan>();
        private string _error;
        private int _index;
        private TimeSpan _loadTime;
        private string[] _slides = Array.Empty<string>();

        /// <summary>Initializes a new instance of the <see cref="AnimatedGifDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public AnimatedGifDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "TAB to switch renderer, ENTER to return to the menu";
            Load();

            // Only once the frames are ready, or the first of them would be charged the whole decode.
            _clock.Restart();
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (key != ConsoleKey.Tab)
                return;

            // Every frame has to be built again, because every frame is a string that the other renderer wrote. There
            // is nothing to reuse: the pixels were thrown away as each one was rendered, deliberately, since keeping
            // them would cost a hundred megabytes. So this re-decodes the file too, and the screen simply stops for as
            // long as that takes — about two seconds on sixel, and is the honest price of the switch.
            _renderer.Toggle();
            Load();

            // The frame it was showing, if the new strip still reaches that far. It does — same file, same count — but
            // a failed reload leaves nothing behind and an index into it would be the only thing that crashed.
            if (_index >= _slides.Length)
                _index = 0;

            _clock.Restart();
            _counter.Restart();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // On the system tick, not the simulation tick: the simulation ticks once a second and the frames here last
            // thirty milliseconds.
            if (_slides.Length == 0 || _clock.Elapsed < _delays[_index])
                return;

            // Restarting rather than subtracting the delay drops whatever the tick overshot by, so a long frame is
            // never made up for later. With the host looping every millisecond that overshoot is about a millisecond,
            // and letting it accumulate into a debt the animation tries to repay is how a player ends up sprinting.
            _clock.Restart();

            var started = Stopwatch.GetTimestamp();
            _index = (_index + 1) % _slides.Length;
            _counter.Record(Stopwatch.GetElapsedTime(started));
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick, so it hands back a string that is already built. Rendering an image here
            // instead would decode and resample a picture thousands of times a second.
            if (_error != null)
                return $"{Environment.NewLine}{_error}";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Animated GIF  —  media/animated.gif on loop");
            sb.AppendLine($"{_counter.Describe()} | {_renderer.Describe()} | " +
                          $"frame {_index + 1}/{_slides.Length} | " +
                          $"cached in {_loadTime.TotalMilliseconds:F0}ms");
            sb.AppendLine();
            sb.Append(_slides[_index]);
            return sb.ToString();
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Decodes every frame and renders each to the string that will be shown for it, keeping no pixels.
        /// </summary>
        private void Load()
        {
            if (!File.Exists(DemoImages.AnimatedGifPath))
            {
                _error = $"{DemoImages.AnimatedGifPath} was not found." + Environment.NewLine +
                         "The project copies media/*.gif next to the executable; try rebuilding.";
                return;
            }

            var options = DemoImages.FitOptions();
            var slides = new List<string>();
            var delays = new List<TimeSpan>();
            var clock = Stopwatch.StartNew();

            try
            {
                // GifDecoder.Decode is the IImageDecoder seam and answers with one still picture, which is the right
                // answer everywhere else in this app and useless here. DecodeFrames is the way in that knows about
                // time. Its laziness is what keeps this loop to one frame of pixels at a time.
                using var stream = File.OpenRead(DemoImages.AnimatedGifPath);
                foreach (var frame in new GifDecoder().DecodeFrames(stream))
                {
                    slides.Add(AnsiImage.FromPixels(frame.Image).ToAnsi(options, _renderer.Current));
                    delays.Add(frame.Delay < _fastFrameThreshold ? _fastFrameDelay : frame.Delay);
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                // Unlike AnsiImage, which answers a broken picture with the magenta error texture, a decoder is the
                // strict path and says so by throwing. Worth catching rather than letting fly: in a text UI the console
                // is the screen, so an unhandled exception paints its stack trace over the interface it escaped from.
                _error = "media/animated.gif could not be decoded." + Environment.NewLine + ex.Message;
                return;
            }

            _loadTime = clock.Elapsed;
            _slides = slides.ToArray();
            _delays = delays.ToArray();

            if (_slides.Length == 0)
                _error = "media/animated.gif carries no frames.";
        }
    }
}
