// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     A media player: pick a file, and watch it in the terminal with the sound playing.
    ///     <para>
    ///         <b>The one screen in this suite that is about a clock nothing here controls.</b> The planner shows
    ///         the time passing; this one has to keep up with it. A frame belongs at a moment, and if the terminal
    ///         was busy when that moment arrived the answer is to skip to the frame that belongs <i>now</i> rather
    ///         than to show the one that is late - which is why <see cref="PlaybackClock" /> exists and why it is
    ///         deliberately the opposite of <see cref="IntervalTimer" />.
    ///     </para>
    ///     <para>
    ///         <b>Three programs, each doing the one thing it is best at.</b> <c>ffprobe</c> says what the file is,
    ///         <c>ffmpeg</c> turns it into pictures already the size of the window, and <c>ffplay</c> makes the
    ///         sound - because this library has no way to make one and getting one means platform interop it does
    ///         not have. Each is optional and each absence degrades differently, which is what the report on the
    ///         idle screen is for.
    ///     </para>
    ///     <para>
    ///         <b>Rendering is cached and that is not an optimisation.</b> <c>OnRenderForm</c> runs about a
    ///         thousand times a second and encoding one sixel frame takes twenty milliseconds, so a player that
    ///         encoded per render would manage under one frame a second and spend every cycle doing it. The
    ///         picture is encoded when the picture changes - thirty times a second at most - and the render method
    ///         does nothing but join strings that already exist.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (OfficeWindow))]
    public sealed class MediaPlayerDialog : Form<OfficeWindowInfo>, IHandlesEscape
    {
        /// <summary>How far the arrow keys seek.</summary>
        private static readonly TimeSpan _smallStep = TimeSpan.FromSeconds(5d);

        /// <summary>How far the up and down keys seek.</summary>
        private static readonly TimeSpan _bigStep = TimeSpan.FromSeconds(30d);

        /// <summary>
        ///     The most frames a second to ask for. A terminal repainting a picture cannot keep up with sixty and
        ///     asking for them only means dropping half, which costs the decoding of every one that is dropped.
        /// </summary>
        private const double MaxFrameRate = 30d;

        /// <summary>What a file with no frame rate of its own is played at.</summary>
        private const double DefaultFrameRate = 25d;

        /// <summary>Where the clock is in the media.</summary>
        private readonly PlaybackClock _clock = new();

        /// <summary>The scrub bar, which keeps its own layout so a click on it seeks to where it was drawn.</summary>
        private readonly Timeline _timeline = new();

        /// <summary>The bars drawn for something with no picture in it.</summary>
        private readonly ColumnChart _bars = new();

        /// <summary>The sound, which is ffplay running beside us.</summary>
        private readonly AudioPlayer _sound = new();

        /// <summary>The pull-down menus across the top.</summary>
        private MenuBar _menuBar;

        /// <summary>What is open, or null.</summary>
        private MediaInfo _media;

        /// <summary>Pictures coming out of ffmpeg, or null when there are none to come.</summary>
        private VideoPipe _video;

        /// <summary>Sound coming out of ffmpeg as numbers, or null.</summary>
        private AudioPipe _samples;

        /// <summary>The frame on screen, kept so its storage can be handed back when the next one replaces it.</summary>
        private PixelBuffer _frame;

        /// <summary>The stage, already drawn. Rebuilt when what it shows changes and not once per render.</summary>
        private IReadOnlyList<string> _stage;

        /// <summary>How many frames have been taken off the pipe, which is what the clock is compared against.</summary>
        private long _shown;

        /// <summary>How many frames a second the file is being played at.</summary>
        private double _fps = DefaultFrameRate;

        /// <summary>How tall the bars are, from zero to one.</summary>
        private double[] _bands = Array.Empty<double>();

        /// <summary>Where each bar has recently been, falling back slowly.</summary>
        private double[] _peaks = Array.Empty<double>();

        /// <summary>The console width, sampled on the tick rather than read while drawing.</summary>
        private int _width = 78;

        /// <summary>Whether what is playing was generated rather than opened.</summary>
        private bool _generated;

        /// <summary>What the status strip has to say, when it is not listing the keys.</summary>
        private string _message;

        /// <summary>Initializes a new instance of the <see cref="MediaPlayerDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public MediaPlayerDialog(IWindow window) : base(window)
        {
        }

        /// <summary>ENTER arrives as a key press, which is the only way the menu bar can be chosen from.</summary>
        public override bool EditsText => true;

        /// <summary>Typed characters do not go into the prompt underneath; none of them are text here.</summary>
        public override bool InputFillsBuffer => false;

        /// <summary>Whether anything is open at all.</summary>
        private bool HasMedia => _media != null;

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (_menuBar == null || !_menuBar.IsOpen)
                return false;

            _menuBar.Close();
            return true;
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            SampleWidth();
            BuildMenus();
            Style();

            // The three version banners cost three process launches, so they are asked for here rather than on the
            // first frame, where the pause would look like the picture being slow.
            _message = FfmpegTools.HasFfmpeg ? null : "ffmpeg was not found. Nothing can be decoded without it.";

            Idle();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (!systemTick)
            {
                SampleWidth();
                return;
            }

            // Every system tick, which is as often as the host loops. This is the one screen in the suite that
            // wants that rather than the once-a-second one: thirty frames a second cannot be paced by a heartbeat
            // that fires once.
            Advance();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "F10 opens the menus, ESC returns to the suite:";

            _timeline.Position = _clock.Position;
            _timeline.Duration = _clock.Duration;
            _timeline.Width = _width;
            _timeline.Row = PlayerChrome.TimelineRow;
            _timeline.Column = 0;

            return Environment.NewLine +
                   PlayerChrome.Compose(_menuBar, _stage, _timeline, InfoText(), StatusText(), _width);
        }

        /// <summary>Never called: <see cref="EditsText" /> puts ENTER on the key path instead.</summary>
        /// <param name="input">Unused.</param>
        public override void OnInputBufferReturned(string input)
        {
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            if (_menuBar != null && _menuBar.HandleKey(keyInfo))
                return;

            switch (keyInfo.Key)
            {
                case ConsoleKey.Spacebar:
                    TogglePause();
                    return;

                case ConsoleKey.LeftArrow:
                    Skip(-_smallStep);
                    return;

                case ConsoleKey.RightArrow:
                    Skip(_smallStep);
                    return;

                case ConsoleKey.DownArrow:
                    Skip(-_bigStep);
                    return;

                case ConsoleKey.UpArrow:
                    Skip(_bigStep);
                    return;

                case ConsoleKey.Home:
                    SeekTo(TimeSpan.Zero);
                    return;

                case ConsoleKey.F3:
                    OpenFile();
                    return;

                case ConsoleKey.F5:
                    Restart();
                    return;

                case ConsoleKey.F6:
                    Close();
                    return;

                case ConsoleKey.F7:
                    PlayTestPattern();
                    return;

                case ConsoleKey.F8:
                    PlayTestTone();
                    return;
            }
        }

        /// <inheritdoc />
        public override void OnMouseEvent(MouseEvent mouse)
        {
            switch (mouse.Kind)
            {
                case MouseEventKindEnum.Press:
                    OnMousePressed(mouse);
                    return;

                case MouseEventKindEnum.Wheel:
                    Skip(mouse.WheelDelta > 0 ? _smallStep : -_smallStep);
                    return;

                case MouseEventKindEnum.Move:
                    if (_menuBar != null)
                        _menuBar.HandleMouseMove(mouse.Row, mouse.Column);

                    return;
            }
        }

        /// <inheritdoc />
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (_menuBar != null && _menuBar.HandleMouse(mouse.Row, mouse.Column))
                return;

            if (mouse.Button != MouseButtonEnum.Left)
                return;

            // The bar answers for its own layout, so the moment seeked to is the moment drawn under the pointer.
            var at = _timeline.TimeAt(mouse.Row, mouse.Column);

            if (at != null)
                SeekTo(at.Value);
        }

        /// <inheritdoc />
        public override void OnFormClosing()
        {
            base.OnFormClosing();

            // Three child processes, and leaving one behind means a film still playing to nobody with no window
            // to close it from.
            Close();
        }

        /// <summary>
        ///     Catches up with the clock: takes whatever frames are due, and looks at whatever sound is due.
        ///     <para>
        ///         <b>The while loop is the whole point.</b> Frames are taken until the one on screen is the one
        ///         the clock asks for, so a slow moment drops frames rather than making the film run late. Showing
        ///         one frame per look instead is the version everybody writes first, and it plays a
        ///         thirty-a-second film at whatever rate the terminal happens to manage, permanently behind the
        ///         sound.
        ///     </para>
        /// </summary>
        private void Advance()
        {
            if (!_clock.IsRunning)
                return;

            if (_video != null)
                AdvanceVideo();
            else if (_samples != null)
                AdvanceBars();

            if (Finished())
                Ended();
        }

        /// <summary>Takes the frames that are due and encodes the last of them.</summary>
        private void AdvanceVideo()
        {
            var wanted = _clock.FrameAt(_fps);
            var moved = false;

            while (_shown < wanted && _video.TryRead(out var frame))
            {
                // The frame being replaced has been drawn already, so its megabyte can be read into again.
                _video.Recycle(_frame);

                _frame = frame;
                _shown++;
                moved = true;
            }

            if (!moved)
                return;

            _stage = StageView.Picture(_frame, _width, PlayerChrome.StageRows);
        }

        /// <summary>Looks at the sound that is due and moves the bars towards it.</summary>
        private void AdvanceBars()
        {
            if (!_samples.TryReadAt(_clock.Position, out var block))
                return;

            Spectrum.Compute(block, _bands);
            _samples.Recycle(block);

            for (var i = 0; i < _bands.Length; i++)
            {
                // Peaks fall slowly and bars fall quickly, which is what makes a set of bars readable rather than
                // a flicker. Kept here rather than in the chart, since how fast a peak drops is a decision about
                // the thing being measured.
                _peaks[i] = Math.Max(_bands[i], _peaks[i] - 0.015d);
            }

            _stage = StageView.Bars(_bars, _bands, _peaks, Caption(), _width, PlayerChrome.StageRows);
        }

        /// <summary>Whether whatever was playing has run out.</summary>
        /// <returns>TRUE when it is over.</returns>
        private bool Finished()
        {
            if (_clock.HasEnded)
                return true;

            // A file of unknown length ends when the pictures do, which is the only signal there is for one.
            return _video != null && _video.IsFinished && _clock.Duration <= TimeSpan.Zero;
        }

        /// <summary>Stops at the end of a file, leaving the last picture on screen.</summary>
        private void Ended()
        {
            _clock.Pause();
            _sound.Stop();

            _message = "Finished. F5 plays it again.";
        }

        /// <summary>Opens a file.</summary>
        private void OpenFile()
        {
            if (!FfmpegTools.HasFfmpeg)
            {
                _message = "ffmpeg was not found, so there is nothing that can decode a file.";
                return;
            }

            FileDialog.OpenFile(
                SimUnit,
                MediaLibrary.BrowseFolder,
                MediaLibrary.Extensions,
                Load,
                () => _message = "Open cancelled.");
        }

        /// <summary>Probes a file and starts playing it.</summary>
        /// <param name="path">The file.</param>
        private void Load(string path)
        {
            Close();

            _media = MediaProbe.Describe(path);
            _generated = false;

            if (!_media.IsPlayable)
            {
                _message = "Nothing in " + _media.Name + " that could be played.";
                _media = null;
                Idle();
                return;
            }

            _message = null;
            Play(TimeSpan.Zero);
        }

        /// <summary>
        ///     Plays ffmpeg's own test pattern, which needs no file, no download and nobody's permission.
        ///     <para>
        ///         It is what stands in for the sample every other application in this suite ships: a video is
        ///         megabytes and every one worth watching belongs to somebody, and this exercises the entire
        ///         pipeline from decode to pixels without any of that.
        ///     </para>
        /// </summary>
        private void PlayTestPattern()
        {
            Generated(
                "testsrc2=size=640x360:rate=30",
                new MediaInfo
                {
                    Title = "Test pattern",
                    HasVideo = true,
                    Width = 640,
                    Height = 360,
                    FrameRate = 30d,
                    VideoCodec = "ffmpeg testsrc2",
                    Duration = TimeSpan.FromSeconds(30d)
                });
        }

        /// <summary>Plays a generated tone, which is what the bars can be watched against.</summary>
        private void PlayTestTone()
        {
            Generated(
                "sine=frequency=440:beep_factor=4:duration=30",
                new MediaInfo
                {
                    Title = "Test tone (440Hz, beeping)",
                    HasAudio = true,
                    Channels = 1,
                    SampleRate = 44100,
                    AudioCodec = "ffmpeg sine",
                    Duration = TimeSpan.FromSeconds(30d)
                });
        }

        /// <summary>Starts one of ffmpeg's own sources.</summary>
        /// <param name="filter">The filter description.</param>
        /// <param name="info">What to say it is.</param>
        private void Generated(string filter, MediaInfo info)
        {
            if (!FfmpegTools.HasFfmpeg)
            {
                _message = "ffmpeg was not found, so there is nothing to generate it with.";
                return;
            }

            Close();

            info.Path = filter;
            _media = info;
            _generated = true;
            _message = null;

            Play(TimeSpan.Zero);
        }

        /// <summary>
        ///     Starts everything from a position: the pictures, the sound, the numbers behind the bars, and the
        ///     clock they are all measured against.
        /// </summary>
        /// <param name="from">Where to start.</param>
        private void Play(TimeSpan from)
        {
            StopPipes();

            if (_media == null)
                return;

            _clock.Duration = _media.Duration;
            _shown = 0;
            _frame = null;

            var columns = Math.Max(8, _width);

            if (_media.HasVideo && AnsiConsole.SupportsPictures())
            {
                _fps = _media.FrameRate > 0d ? Math.Min(_media.FrameRate, MaxFrameRate) : DefaultFrameRate;

                var size = StageView.PixelSize(ImageRenderers.Default, columns, PlayerChrome.StageRows);

                _video = new VideoPipe(_media.Path, from, size.Width, size.Height, _fps, _generated);

                if (_video.Failed)
                {
                    _message = _video.Error;
                    _video.Dispose();
                    _video = null;
                }
            }

            if (_video == null && _media.HasAudio)
            {
                var bands = StageView.BandsFor(columns);

                _bands = new double[bands];
                _peaks = new double[bands];
                _samples = new AudioPipe(_media.Path, from, _generated);
            }

            _sound.PlayFrom(_media.Path, from, _generated);

            _clock.SeekTo(from);
            _clock.Resume();

            // Something on screen at once, rather than a blank stage until the first frame arrives.
            if (_video != null)
                _stage = StageView.Picture(null, _width, PlayerChrome.StageRows);
            else if (_samples != null)
                _stage = StageView.Bars(_bars, _bands, _peaks, Caption(), _width, PlayerChrome.StageRows);
            else
                Idle();
        }

        /// <summary>Plays and pauses, which is one key and the thing a player is asked to do most.</summary>
        private void TogglePause()
        {
            if (!HasMedia)
                return;

            if (_clock.IsRunning)
            {
                _clock.Pause();

                // The sound is a separate program with no way to be told anything, so pausing it is stopping it.
                // Resuming starts it again where the clock says, which is also exactly what a seek is.
                _sound.Stop();
                _message = "Paused.";
                return;
            }

            if (_clock.HasEnded)
            {
                Restart();
                return;
            }

            Play(_clock.Position);
            _message = null;
        }

        /// <summary>Seeks by an amount from where the clock is.</summary>
        /// <param name="delta">How far; negative goes back.</param>
        private void Skip(TimeSpan delta)
        {
            if (!HasMedia)
                return;

            SeekTo(_clock.Position + delta);
        }

        /// <summary>
        ///     Seeks to a moment, which means starting the whole pipeline again there: a pipe cannot be rewound,
        ///     and asking ffmpeg to start somewhere is what it does well.
        /// </summary>
        /// <param name="position">Where to go.</param>
        private void SeekTo(TimeSpan position)
        {
            if (!HasMedia)
                return;

            var wasRunning = _clock.IsRunning;

            _clock.SeekTo(position);
            Play(_clock.Position);

            if (!wasRunning)
            {
                _clock.Pause();
                _sound.Stop();
            }

            _message = null;
        }

        /// <summary>Plays whatever is open again from the beginning.</summary>
        private void Restart()
        {
            if (!HasMedia)
            {
                _message = "Nothing is open. F3 opens a file.";
                return;
            }

            Play(TimeSpan.Zero);
            _message = null;
        }

        /// <summary>Stops everything and goes back to the idle page.</summary>
        private void Close()
        {
            StopPipes();

            _sound.Stop();
            _clock.Stop();

            // Stopping rewinds and pauses; it does not make the length unknown, because a stopped film is still
            // that long. Closing the file is what does, and forgetting it leaves the scrub bar claiming the length
            // of something that is no longer open.
            _clock.Duration = TimeSpan.Zero;

            _media = null;
            _frame = null;
            _shown = 0;

            Idle();
        }

        /// <summary>Ends the two decoders, which are child processes and threads rather than objects.</summary>
        private void StopPipes()
        {
            _video?.Dispose();
            _video = null;

            _samples?.Dispose();
            _samples = null;
        }

        /// <summary>Puts the page that explains this screen back on the stage.</summary>
        private void Idle()
        {
            _stage = StageView.Idle(_width, PlayerChrome.StageRows);
        }

        /// <summary>Reads the console width, and redraws whatever is on the stage at the new size.</summary>
        private void SampleWidth()
        {
            var width = Math.Max(40, AnsiConsole.SafeWindowWidth() - 1);

            if (width == _width)
                return;

            _width = width;

            // A picture is the size the decoder was told to make it, so a resize means starting it again. Anything
            // else can simply be drawn wider.
            if (_video != null && _clock.IsRunning)
                Play(_clock.Position);
            else if (HasMedia && _samples != null)
                _stage = StageView.Bars(_bars, _bands, _peaks, Caption(), _width, PlayerChrome.StageRows);
            else if (!HasMedia)
                Idle();
        }

        /// <summary>Sets the colours, which every application here takes from the same place.</summary>
        private void Style()
        {
            _bars.Minimum = 0d;
            _bars.Maximum = 1d;
            _bars.ColumnStyle = DosTheme.Field;
            _bars.EmptyStyle = DosTheme.Field;
            _bars.PeakStyle = DosTheme.Frame;
            _bars.ColumnColorRamp = ColorRamp.PrideRainbow;
            _bars.RampMode = ColorRampModeEnum.Spread;

            _timeline.FilledStyle = DosTheme.Frame;
            _timeline.TrackStyle = DosTheme.Header;
            _timeline.MarkerStyle = DosTheme.Highlight;
            _timeline.TimeStyle = DosTheme.Header;
        }

        /// <summary>Builds the pull-downs.</summary>
        private void BuildMenus()
        {
            _menuBar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("Open...", OpenFile, "F3") {EnabledWhen = () => FfmpegTools.HasFfmpeg},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Test Pattern", PlayTestPattern, "F7")
                        {EnabledWhen = () => FfmpegTools.HasFfmpeg},
                    new MenuBarEntry("Test Tone", PlayTestTone, "F8") {EnabledWhen = () => FfmpegTools.HasFfmpeg},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Close", Close, "F6") {EnabledWhen = () => HasMedia},
                    new MenuBarEntry("Exit", () => ParentWindow.ClearForm(), "Esc")),
                new MenuBarMenu("Play",
                    new MenuBarEntry("Play / Pause", TogglePause, "Space") {EnabledWhen = () => HasMedia},
                    new MenuBarEntry("Restart", Restart, "F5") {EnabledWhen = () => HasMedia},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Back 5s", () => Skip(-_smallStep), "Left") {EnabledWhen = () => HasMedia},
                    new MenuBarEntry("On 5s", () => Skip(_smallStep), "Right") {EnabledWhen = () => HasMedia},
                    new MenuBarEntry("Back 30s", () => Skip(-_bigStep), "Down") {EnabledWhen = () => HasMedia},
                    new MenuBarEntry("On 30s", () => Skip(_bigStep), "Up") {EnabledWhen = () => HasMedia},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Back to Start", () => SeekTo(TimeSpan.Zero), "Home")
                        {EnabledWhen = () => HasMedia}),
                new MenuBarMenu("Help",
                    new MenuBarEntry("What Was Found...", ShowReport),
                    new MenuBarEntry("About", ShowAbout)) {AlignRight = true})
            {
                BarStyle = DosTheme.MenuBar,
                HighlightStyle = DosTheme.MenuHighlight,
                PanelStyle = DosTheme.MenuPanel,
                PanelHighlightStyle = DosTheme.MenuHighlight,
                DisabledStyle = DosTheme.MenuDisabled,
                CheckMark = '√',
                BarRow = PlayerChrome.BarRow,
                PanelRow = PlayerChrome.InfoRow
            };
        }

        /// <summary>Shows what was found on this machine and what the terminal can do with it.</summary>
        private void ShowReport()
        {
            var lines = new List<string>(FfmpegTools.Report()) {string.Empty, StageView.PictureReport()};

            MessageBox.Show(SimUnit, string.Join(Environment.NewLine, lines));
        }

        /// <summary>Says what this is.</summary>
        private void ShowAbout()
        {
            _message = "WolfCurses media player - ffmpeg makes the pixels the exact size of the window.";
        }

        /// <summary>What the bars are told they are showing.</summary>
        /// <returns>The caption.</returns>
        private string Caption()
        {
            return _media == null ? string.Empty : _media.Name;
        }

        /// <summary>The strip above the picture, saying what is open and how it is being drawn.</summary>
        /// <returns>The text.</returns>
        private string InfoText()
        {
            if (_media == null)
                return "Nothing open";

            var sb = _media.Name + "   " + _media.Summary();

            if (_video != null)
                sb += "   drawn as " + ImageRenderers.Default.Name +
                      " at " + _video.Width + "x" + _video.Height;
            else if (_media.HasVideo)
                sb += "   no pictures: this terminal cannot take them";

            return sb;
        }

        /// <summary>The key-hint strip, or whatever the last action had to say.</summary>
        /// <returns>The status text.</returns>
        private string StatusText()
        {
            var state = !HasMedia
                ? "Stopped"
                : _clock.IsRunning
                    ? "Playing"
                    : _clock.HasEnded
                        ? "Finished"
                        : "Paused";

            if (HasMedia && _video != null)
                state += "  " + _fps.ToString("0.##", CultureInfo.InvariantCulture) + "fps";

            if (HasMedia && !AudioPlayer.IsAvailable)
                state += "  silent";

            if (!string.IsNullOrEmpty(_message))
                return "  " + state + "   " + _message;

            return "  " + state + "   SPACE=Play/Pause  F3=Open  Arrows=Seek  F10=Menu";
        }
    }
}
