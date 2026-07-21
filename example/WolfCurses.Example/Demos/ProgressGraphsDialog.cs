// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Utility;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     A live dashboard that shows the standard progress and graph controls working together: a determinate
    ///     <see cref="ProgressBar" />, an indeterminate <see cref="MarqueeBar" /> with a <see cref="SpinningPixel" />
    ///     spinner beside it, an inline <see cref="Sparkline" />, a <see cref="BarChart" /> of the most recent
    ///     readings, and a scrolling <see cref="LineGraph" />. A synthetic
    ///     signal advances one step per simulation tick so everything animates; state is only mutated on the simulation
    ///     tick (not the many fast system ticks) so <see cref="OnRenderForm" /> stays a pure read of the current frame.
    ///     Pressing ENTER returns to the menu.
    ///     <para>
    ///         It is also the showcase for the widgets' color support, and deliberately uses it the way the feature is
    ///         meant to be used rather than the way that shows off the most hues. Every ramp here is in
    ///         <see cref="ColorRampModeEnum.Level" />, where the color <em>means</em> something about the value it is
    ///         drawn on — the download bar reddens as it fills, tall bars run hot, the plotted line warms with its own
    ///         reading. (<see cref="ColorRampModeEnum.Spread" />, where the color tracks position rather than value, is
    ///         what the pride flag demo is built on.) Nothing here is load-bearing: run with <c>NO_COLOR</c> set and
    ///         this screen renders byte-for-byte what it rendered before any of it was colored.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class ProgressGraphsDialog : Form<ExampleWindowInfo>
    {
        /// <summary>
        ///     The ramp the signal is read through — cool blue when the reading is low, through green and amber to a
        ///     hot orange-red at the top.
        ///     <para>
        ///         Hand-built rather than <see cref="ColorRamp.Heat" />, which is the obvious choice and the wrong one
        ///         here: black-body heat starts at black, so on a dark terminal every low reading would be painted
        ///         invisible. Every stop below is mid-luminance on purpose, which is what keeps this screen legible on
        ///         a white terminal and a black one without asking which it is.
        ///     </para>
        /// </summary>
        private static readonly ColorRamp _signalRamp = ColorRamp.Smooth(
            new Rgb24(0x00, 0xB3, 0xFF), // calm: sky blue
            new Rgb24(0x00, 0xD0, 0x7A), // green
            new Rgb24(0xFF, 0xC1, 0x07), // amber
            new Rgb24(0xFF, 0x45, 0x00)); // hot: orange-red

        /// <summary>The screen's own heading. Bold plus a mid cyan, both of which survive either terminal polarity.</summary>
        private static readonly TextStyle _headingStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null,
            true);

        /// <summary>The label column down the left. Named colors on purpose — a theme gets to have an opinion about them.</summary>
        private static readonly TextStyle _labelStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan));

        /// <summary>Supporting text that should read as quieter than the data it explains.</summary>
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        private readonly ProgressBar _download = new ProgressBar
        {
            Label = "Download ",
            Width = 24,
            // Level, not Spread: the whole filled run takes one color picked by how full the bar is, so it reads as a
            // gauge going from healthy to alarming rather than as decoration. Traffic is the one ramp whose meaning
            // needs no legend.
            FillRamp = ColorRamp.Traffic,
            RampMode = ColorRampModeEnum.Level,
            // Dimming the track does more for readability than coloring the fill does: progress is the contrast
            // between the two, not the brightness of either.
            EmptyStyle = ConsoleColor.DarkGray,
            BracketStyle = ConsoleColor.DarkGray,
            LabelStyle = ConsoleColor.DarkCyan,
            PercentageStyle = new TextStyle(bold: true)
        };

        private readonly MarqueeBar _marquee = new MarqueeBar
        {
            PointerStyle = ConsoleColor.Magenta,
            TrackStyle = ConsoleColor.DarkGray
        };

        // The spinner half of the indeterminate pair, sharing the marquee's magenta so the two read as one "working"
        // motif. ColorMode stays at its Auto default like every other widget here, so NO_COLOR still empties it.
        private readonly SpinningPixel _spinner = new SpinningPixel {GlyphStyle = ConsoleColor.Magenta};

        private readonly List<double> _samples = new List<double>();

        // The sparkline colors each glyph by that sample's own value, which is the reading that matches what a
        // sparkline is: the glyph already says how high, and the color says the same thing a second way.
        private readonly Sparkline _spark = new Sparkline {SparklineColorRamp = _signalRamp};

        private BarChart _barChart;
        private int _capacity = 48;
        private int _downloadPercent;
        private LineGraph _lineGraph;
        private string _marqueeFrame = string.Empty;
        private int _phase;
        private string _spinnerFrame = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="ProgressGraphsDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public ProgressGraphsDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            // Size the graph to the current console, leaving room for the labels and the surrounding chrome.
            var graphWidth = Math.Clamp(SafeWindowWidth() - 14, 24, 64);
            var graphHeight = Math.Clamp(SafeWindowHeight() - 19, 5, 12);
            _capacity = graphWidth;

            _lineGraph = new LineGraph
            {
                Width = graphWidth,
                Height = graphHeight,
                Minimum = 0,
                Maximum = 100,
                Fill = true,
                // The ramp colors the plotted column by the value it is plotting, so the curve warms as it climbs;
                // the frame around it stays deliberately quiet so the data is the only thing shouting.
                Ramp = _signalRamp,
                AxisStyle = ConsoleColor.DarkGray,
                ScaleStyle = ConsoleColor.DarkCyan
            };
            _barChart = new BarChart
            {
                Width = 24,
                ShowTrack = true,
                Ramp = _signalRamp,
                RampMode = ColorRampModeEnum.Level,
                TrackStyle = ConsoleColor.DarkGray,
                SeparatorStyle = ConsoleColor.DarkGray,
                LabelStyle = ConsoleColor.DarkCyan,
                ValueStyle = new TextStyle(bold: true)
            };
            _spark.Minimum = 0;
            _spark.Maximum = 100;

            // Seed a full window of history so the graph is already populated on the first frame, then let it scroll.
            for (_phase = 0; _phase < _capacity; _phase++)
                _samples.Add(Wave(_phase));

            _marqueeFrame = _marquee.Step();
            _spinnerFrame = _spinner.Step();

            ParentWindow.PromptText = "Press ENTER or ESC to return to the menu";
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Advance the animation once per simulation tick (about once a second); ignore fast system ticks so the
            // rendered frame only changes on a real beat.
            if (systemTick)
                return;

            _samples.Add(Wave(_phase++));
            while (_samples.Count > _capacity)
                _samples.RemoveAt(0);

            _downloadPercent = (_downloadPercent + 6) % 101;
            _marqueeFrame = _marquee.Step();
            _spinnerFrame = _spinner.Step();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            var current = _samples.Count > 0 ? _samples[_samples.Count - 1] : 0d;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(_headingStyle.Apply("Progress bars & graphs (live)"));

            // Wrapped rather than written out flat: every widget below is sized from the console, and this sentence
            // was the one row on the screen that was not. At 86 characters it overran an 80-column console, and with
            // auto-wrap disabled for the frame (ConsolePresenter turns DECAWM off) the tail was clipped, not reflowed.
            sb.AppendLine(Wrap(_dimStyle,
                "Colors come from TextStyle and ColorRamp; with NO_COLOR set not one escape is emitted.",
                Math.Max(20, SafeWindowWidth() - 2)));
            sb.AppendLine();

            // Determinate progress bar and the indeterminate marquee side by side conceptually, with the spinner
            // leading the "Working" label as the other indeterminate widget.
            sb.AppendLine(_download.Render(_downloadPercent, 100));
            sb.Append(_spinnerFrame).Append(' ')
                .Append(_labelStyle.Apply("Working")).Append("  ")
                .Append(_marqueeFrame); // MarqueeBar.Step already ends the line.

            // Inline sparkline of the whole visible history.
            sb.Append(_labelStyle.Apply("Signal")).Append("   ").Append(_spark.Render(_samples));
            sb.AppendFormat(CultureInfo.InvariantCulture, "  {0,3}%", (int) Math.Round(current));
            sb.AppendLine();
            sb.AppendLine();

            // Bar chart of the five most recent readings.
            sb.AppendLine(_labelStyle.Apply("Recent readings:"));
            sb.AppendLine(_barChart.Render(RecentBars()));
            sb.AppendLine();

            // The scrolling line graph.
            sb.AppendLine(_labelStyle.Apply("Signal over time:"));
            sb.Append(_lineGraph.Render(_samples));

            return sb.ToString();
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            // Any submitted line (ENTER) closes the dashboard and returns to the menu.
            ClearForm();
        }

        /// <summary>The five most recent readings as labelled bars (oldest to newest, newest labelled "now").</summary>
        private IEnumerable<BarChartValue> RecentBars()
        {
            const int shown = 5;
            var start = Math.Max(0, _samples.Count - shown);
            var bars = new List<BarChartValue>();
            for (var i = start; i < _samples.Count; i++)
            {
                var fromEnd = _samples.Count - 1 - i;
                var label = fromEnd == 0 ? "now" : "-" + fromEnd.ToString(CultureInfo.InvariantCulture);

                // Deliberately the two-argument form even though BarChartValue can now carry a style of its own: a
                // per-item style beats the ramp outright, so calling the newest reading out that way would cost it
                // the very heat color the chart exists to show. The per-item override is for charts whose colors are
                // categorical ("this series is errors and it is red"), which this one's are not.
                bars.Add(new BarChartValue(label, Math.Round(_samples[i])));
            }

            return bars;
        }

        /// <summary>A smooth synthetic signal in the range 0..100 so the controls have something lively to show.</summary>
        private static double Wave(int t)
        {
            var value = 50d + 35d * Math.Sin(t * 0.35d) + 12d * Math.Sin(t * 0.9d);
            return Math.Clamp(value, 0d, 100d);
        }

        /// <summary>
        ///     Word-wraps a line of prose to the console and styles it one row at a time. Wrapping first and styling
        ///     second, because an escape sequence has length but no width — wrapping styled text would count its bytes
        ///     as characters, and a style opened before the wrap would bleed across the newline the wrap inserts.
        /// </summary>
        /// <param name="style">The style each produced row is wrapped in.</param>
        /// <param name="text">The prose to wrap.</param>
        /// <param name="width">The widest a row may be.</param>
        /// <returns>The wrapped, styled rows, with no trailing newline.</returns>
        private static string Wrap(TextStyle style, string text, int width)
        {
            var sb = new StringBuilder();
            foreach (var line in text.WordWrap(width).Split(Environment.NewLine))
            {
                if (line.Length == 0)
                    continue;

                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(style.Apply(line));
            }

            return sb.ToString();
        }

        private static int SafeWindowWidth()
        {
            try
            {
                return Console.WindowWidth;
            }
            catch
            {
                return 80;
            }
        }

        private static int SafeWindowHeight()
        {
            try
            {
                return Console.WindowHeight;
            }
            catch
            {
                return 24;
            }
        }
    }
}
