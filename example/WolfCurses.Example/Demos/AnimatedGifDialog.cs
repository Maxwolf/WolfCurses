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
using WolfCurses.Window.Control;
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
    ///     <para>
    ///         <b>Rendering all of them up front is the wait this dialog is named for, and on sixel it is about two
    ///         seconds — long enough that a frozen console would read as a hang.</b> So the rendering is not done in one
    ///         blocking call but spread across the tick loop a slice at a time (see <see cref="AdvanceLoad" />): each
    ///         tick spends about <see cref="_loadBudgetPerTick" /> turning the next frames into strings and then hands
    ///         control straight back, which is the only thing that lets a <see cref="ProgressBar" /> fill as the frames
    ///         arrive rather than the screen sitting dead. The bar can be a real percentage, not a spinner, because
    ///         <see cref="GifDecoder.CountFrames" /> reads the frame total off the file's block framing up front without
    ///         decoding a pixel — a GIF states that total nowhere in its header, only by reaching the end. Nothing kept
    ///         is kept any longer than before — the pixels are still dropped as each frame is rendered — the same bill
    ///         is simply paid in visible installments instead of all at once behind a stalled screen. The
    ///         <c>DecodeFrames</c> walk is lazy and reads its bytes eagerly, so the file is closed the instant the walk
    ///         is set up and only the in-memory cursor is carried from tick to tick.
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

        /// <summary>
        ///     How much of any one tick to spend rendering frames before handing control back so the progress bar can
        ///     advance and the screen can redraw.
        ///     <para>
        ///         A whole frame is always finished even when it overruns this, so the slowest renderer still makes a
        ///         frame of progress a tick; the budget only decides how many <i>more</i> a fast one packs in before it
        ///         yields. Small enough that the loop stays responsive to ENTER while loading, large enough that the
        ///         cost of yielding — the host loop's own sleep, about fifteen milliseconds on Windows — does not come to
        ///         dominate the total. The bar itself needs no help keeping pace: it reads real frames rendered against
        ///         the real total, so it advances by exactly what was done however the slices happen to fall.
        ///     </para>
        /// </summary>
        private static readonly TimeSpan _loadBudgetPerTick = TimeSpan.FromMilliseconds(30);

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
        ///     The determinate bar shown while frames are being rendered, so several seconds of pre-rendering read as
        ///     working — and read as <i>how far along</i>, which a spinner cannot. It fills against the frame total
        ///     <see cref="GifDecoder.CountFrames" /> reads up front, and only advances because the load yields the tick
        ///     loop between slices — a blocking load would leave it stuck at zero, which is the whole reason the load was
        ///     broken up.
        /// </summary>
        private readonly ProgressBar _progress = new ProgressBar {Width = 30, Label = "Loading"};

        /// <summary>
        ///     TAB switches renderer here as it does in the sprite tests, but it costs something quite different, and
        ///     the difference is the point.
        ///     <para>
        ///         A sprite test changes renderer between one frame and the next, because it draws every frame anyway.
        ///         This one drew all ninety-one of them before it started, so switching means drawing them all again —
        ///         about half a second into half blocks, and <b>about two seconds</b> into sixel (down from seven
        ///         before the 2026-07-17 renderer rework). That wait now fills the progress bar rather than freezing the
        ///         screen, because the reload is spread across the tick loop the same way the first load is; but it is
        ///         the same wait, the entire cost of true pixels, paid once at the door, and afterwards playback is an
        ///         array lookup either way. Watch the "cached in" figure rather than the fps: fps will read about 32
        ///         whichever is chosen, which is exactly the finding.
        ///     </para>
        /// </summary>
        private readonly RendererSwitch _renderer = new();

        private TimeSpan[] _delays = Array.Empty<TimeSpan>();
        private string _error;
        private int _index;
        private TimeSpan _loadTime;
        private string[] _slides = Array.Empty<string>();

        // Loading state. A non-null cursor means frames are still being rendered and playback has not begun; the two
        // lists accumulate what is finished, and are published to _slides/_delays in one go when the walk runs out. All
        // of it is null once loading is over, so nothing here outlives the wait at the door.
        private IEnumerator<GifFrame> _loadCursor;
        private List<TimeSpan> _loadDelays;
        private AnsiImageOptions _loadOptions;
        private IImageRenderer _loadRenderer;
        private List<string> _loadSlides;
        private int _loadTotal;
        private TimeSpan _loadWork;

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

            ParentWindow.PromptText = "TAB to switch renderer, ENTER or ESC to return to the menu";

            // Only sets the load going; the frames are rendered a slice per tick from here. The playback clock is not
            // started here but in FinishLoad, so the wait at the door is never charged to the first frame's timing.
            BeginLoad();
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (key != ConsoleKey.Tab)
                return;

            // One load at a time. While frames are still being rendered, TAB is ignored rather than tearing a half-done
            // load down and starting the other renderer from scratch; the switch is available again the moment the
            // bar gives way to playback, which is a couple of seconds at most.
            if (_loadCursor != null)
                return;

            // Every frame is a string the previous renderer wrote, and the pixels were dropped as each was rendered, so
            // there is nothing to reuse — switching means rendering all of them again, and re-decoding the file to do
            // it. Before the load was broken up this stopped the screen dead for about two seconds on sixel; now the
            // same work runs a slice per tick and the progress bar fills through it.
            _renderer.Toggle();
            BeginLoad();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // While frames are still being rendered, spend this tick's slice on that and let the bar advance; playback
            // has not begun, so none of the timing below applies yet.
            if (_loadCursor != null)
            {
                AdvanceLoad();
                return;
            }

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

            if (_loadCursor != null)
                return RenderLoading();

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
            // Leaving mid-load abandons the walk. The file was already closed when the load was set up, so this only
            // drops the in-memory cursor and the frames rendered so far; nothing is left open behind us.
            DisposeCursor();
            ClearForm();
        }

        /// <summary>
        ///     The loading screen: what is being done and how far it has got, the bar filling toward the frame total.
        ///     Shown until <see cref="FinishLoad" /> hands over to playback.
        /// </summary>
        private string RenderLoading()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Animated GIF  —  media/animated.gif");
            sb.AppendLine();
            sb.AppendLine("Decoding every frame and rendering it up front — the wait at the door.");
            sb.AppendLine();
            sb.AppendLine(_progress.Render(_loadSlides.Count, _loadTotal));
            sb.Append($"frame {_loadSlides.Count} / {_loadTotal}");
            return sb.ToString();
        }

        /// <summary>
        ///     Sets a load going: opens the file, checks it is a GIF, and keeps the lazy frame walk to be drawn a slice
        ///     at a time from <see cref="AdvanceLoad" />. Does not render any frame itself, so it returns at once and the
        ///     progress bar is up on the very next render.
        /// </summary>
        private void BeginLoad()
        {
            // A fresh load throws away any current playback and any earlier error. _index back to the start because the
            // strip about to be built is a new one; the animation loops, so beginning it from frame zero after a switch
            // is no different to a viewer than resuming would be.
            DisposeCursor();
            _error = null;
            _slides = Array.Empty<string>();
            _delays = Array.Empty<TimeSpan>();
            _index = 0;
            _loadTotal = 0;
            _loadWork = TimeSpan.Zero;
            _loadTime = TimeSpan.Zero;

            if (!File.Exists(DemoImages.AnimatedGifPath))
            {
                _error = $"{DemoImages.AnimatedGifPath} was not found." + Environment.NewLine +
                         "The project copies media/*.gif next to the executable; try rebuilding.";
                return;
            }

            // Fixed at the start of the load so every frame is sized and drawn the same way even if the console is
            // resized while the load is running.
            _loadOptions = DemoImages.FitOptions();
            _loadRenderer = _renderer.Current;
            _loadSlides = new List<string>();
            _loadDelays = new List<TimeSpan>();

            try
            {
                using var stream = File.OpenRead(DemoImages.AnimatedGifPath);
                var decoder = new GifDecoder();

                // The bar needs a denominator, and a GIF only gives up its frame count at the very end. CountFrames
                // walks the block framing to get it without decoding a pixel — microseconds — then the stream is rewound
                // and the real, lazy decode reads from the top. Two passes over one open file, the second served from
                // the OS cache. DecodeFrames reads its bytes eagerly and walks them lazily, so the file can be closed
                // the moment the walk is set up — the cursor holds the bytes, not the handle, and rides from tick to
                // tick. The "not a GIF" and "too large" checks run here, inside the using, exactly as the decoder
                // documents; damage found deeper in the file surfaces later, from MoveNext, and is caught there.
                _loadTotal = decoder.CountFrames(stream);
                stream.Position = 0;
                _loadCursor = decoder.DecodeFrames(stream).GetEnumerator();
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                FailLoad("media/animated.gif could not be decoded." + Environment.NewLine + ex.Message);
            }
        }

        /// <summary>
        ///     Renders as many of the next frames as fit in <see cref="_loadBudgetPerTick" />, then returns so the screen
        ///     can redraw with the bar advanced. Called once a tick until the walk runs out, at which point it hands over
        ///     to <see cref="FinishLoad" />.
        /// </summary>
        private void AdvanceLoad()
        {
            var budgetStart = Stopwatch.GetTimestamp();

            try
            {
                while (true)
                {
                    // Before MoveNext, so the decode is timed along with the render: "cached in" should mean the whole
                    // cost of turning a frame into a string, which is what it meant when this was one blocking loop.
                    var frameStart = Stopwatch.GetTimestamp();
                    if (!_loadCursor.MoveNext())
                        break;

                    var frame = _loadCursor.Current;
                    _loadSlides.Add(AnsiImage.FromPixels(frame.Image).ToAnsi(_loadOptions, _loadRenderer));
                    _loadDelays.Add(frame.Delay < _fastFrameThreshold ? _fastFrameDelay : frame.Delay);
                    _loadWork += Stopwatch.GetElapsedTime(frameStart);

                    // Spent this tick's slice: hand control back so the bar redraws before the next frames are rendered.
                    if (Stopwatch.GetElapsedTime(budgetStart) >= _loadBudgetPerTick)
                        return;
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                // Damage discovered mid-walk surfaces here rather than from the call that started the load. Caught for
                // the same reason the initial checks are: in a text UI the console is the screen, so an unhandled
                // exception paints its stack trace over the interface it escaped from.
                FailLoad("media/animated.gif could not be decoded." + Environment.NewLine + ex.Message);
                return;
            }

            // The walk is done — every frame is a string now. Publish them and let playback begin.
            FinishLoad();
        }

        /// <summary>Publishes the rendered frames and starts playback and its clock.</summary>
        private void FinishLoad()
        {
            _loadTime = _loadWork;
            _slides = _loadSlides.ToArray();
            _delays = _loadDelays.ToArray();
            if (_slides.Length == 0)
                _error = "media/animated.gif carries no frames.";

            // Built the arrays before this drops the lists it built them from.
            DisposeCursor();

            // Playback, and the readout, begin now rather than when the form opened, so the load is not divided into the
            // first frame's timing. Restart leaves the counter's clock stopped until the first frame actually arrives.
            _clock.Restart();
            _counter.Restart();
        }

        /// <summary>Abandons the load and shows why.</summary>
        private void FailLoad(string message)
        {
            _error = message;
            DisposeCursor();
        }

        /// <summary>Drops the frame walk and everything the in-progress load was accumulating.</summary>
        private void DisposeCursor()
        {
            _loadCursor?.Dispose();
            _loadCursor = null;
            _loadSlides = null;
            _loadDelays = null;
        }
    }
}
