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

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     A live dashboard that shows the standard progress and graph controls working together: a determinate
    ///     <see cref="ProgressBar" />, an indeterminate <see cref="MarqueeBar" /> with a <see cref="SpinningPixel" />
    ///     spinner beside it, an inline <see cref="Sparkline" />, a <see cref="BarChart" /> of the most recent
    ///     readings with a <see cref="ColumnChart" /> of those same readings standing beside it, and a scrolling
    ///     <see cref="LineGraph" />. A synthetic signal advances one step per simulation tick so everything animates;
    ///     state is only mutated on the simulation tick (not the many fast system ticks) so
    ///     <see cref="OnRenderForm" /> stays a pure read of the current frame. Pressing ENTER returns to the menu.
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
    [ParentWindow(typeof (DemoWindow))]
    public sealed class ProgressGraphsDialog : Form<DemoWindowInfo>
    {
        /// <summary>
        ///     How many of the latest samples the two "recent readings" charts draw. Shared rather than written
        ///     twice, because it is also the <see cref="ColumnChart.Height" />: the column chart is only free of
        ///     rows while it is exactly as tall as the bar chart it sits next to.
        /// </summary>
        private const int RecentReadings = 5;

        /// <summary>Blank columns left between the bar chart and the column chart beside it.</summary>
        private const int ChartGap = 3;

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

        // The same five readings the bar chart draws, stood up instead of laid out. Minimum pinned at zero with
        // Maximum left null is what keeps the two honest about the same numbers: BarChart always scales against the
        // largest value shown, so a column chart allowed to find its own floor as well would sit the lowest reading
        // flat on the baseline and disagree with the bar beside it about that very reading.
        //
        // ColumnColorRamp, not Ramp, and the bar chart it stands next to is exactly why that is easy to get
        // wrong: BarChart.Ramp IS the ColorRamp, while ColumnChart.Ramp is the GLYPH ramp (the eight block
        // heights). The types differ so a straight swap will not compile, but the reading "Ramp is where the color
        // goes" is right for one of the two widgets on this row and wrong for the other. The mode and the ramp are
        // the bar chart's own, so one reading is one color in both charts and the eye can pair them off.
        private readonly ColumnChart _columnChart = new ColumnChart
        {
            Height = RecentReadings,
            ColumnWidth = 2,
            Gap = 1,
            Minimum = 0,
            ColumnColorRamp = _signalRamp,
            RampMode = ColorRampModeEnum.Level
        };

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
            var graphWidth = Math.Clamp(AnsiConsole.SafeWindowWidth() - 14, 24, 64);
            var graphHeight = Math.Clamp(AnsiConsole.SafeWindowHeight() - 19, 5, 12);
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
                Math.Max(20, AnsiConsole.SafeWindowWidth() - 2)));
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

            // The five most recent readings, drawn twice: once lying down and once standing up.
            sb.AppendLine(_labelStyle.Apply("Recent readings:"));
            sb.AppendLine(RecentCharts(RecentValues()));
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

        /// <summary>
        ///     The two "recent readings" charts side by side: the horizontal <see cref="BarChart" /> and, over the
        ///     identical numbers, the vertical <see cref="ColumnChart" />.
        ///     <para>
        ///         The pairing is the lesson rather than the decoration. <see cref="ColumnChart" />'s own doc says
        ///         "it is <see cref="Sparkline" /> with a height", which is true and still leaves a reader asking
        ///         which of the two shapes they want; drawing both over the same five values is what makes that
        ///         question answerable by looking. The bar chart names every reading and prints it, and gives it a
        ///         length that can be compared exactly against the one two rows away, paying a whole row per reading
        ///         for all of that. The column chart gives up the labels and the numbers and buys a profile read in
        ///         one glance, at a fixed height however many readings there are. Neither is the better widget,
        ///         which is why they both ship.
        ///     </para>
        ///     <para>
        ///         Joining them costs zero extra rows, and rows are the binding constraint on this screen: at a
        ///         24-row console the form already composes about 22 rows against a 22-row budget, so anything
        ///         stacked below pushes the line graph off the bottom instead.
        ///     </para>
        /// </summary>
        /// <param name="values">The readings both charts draw, oldest first.</param>
        /// <returns>The joined block, or the bar chart alone when the console is too narrow for both.</returns>
        private string RecentCharts(IReadOnlyList<double> values)
        {
            var bars = _barChart.Render(RecentBars(values));

            // The joined block is wider than the bar chart alone, and an over-wide row is the trap this file was
            // already bitten by once (see the wrapped prose above): ConsolePresenter turns auto-wrap off for the
            // frame, so a row running past the last column is truncated rather than reflowed and the column chart
            // would simply lose its right-hand bars with nothing on screen saying why. Both widths are measured
            // rather than guessed - the bar chart's off the rows it just rendered, since its value column grows a
            // digit as the signal climbs, and the column chart's from WidthFor, which is the widget's own
            // arithmetic rather than a copy of it living here and drifting.
            var needed = BlockWidth(bars) + ChartGap + _columnChart.WidthFor(values.Count);
            if (needed > AnsiConsole.SafeWindowWidth())
                return bars;

            var columns = string.Join(Environment.NewLine, _columnChart.Render(values));
            return TextColumns.Join(bars, columns, ChartGap);
        }

        /// <summary>
        ///     The widest visible row of a rendered block. Measured with <see cref="AnsiText.VisibleLength" /> and
        ///     never <c>string.Length</c>, because every row here carries color: an escape sequence has length but
        ///     no width, so counting characters would report a styled bar chart as hundreds of columns wide and this
        ///     screen would fall back to one chart on a console with room for two.
        /// </summary>
        /// <param name="block">The rendered block, rows separated by the platform newline.</param>
        /// <returns>The width in screen columns.</returns>
        private static int BlockWidth(string block)
        {
            var width = 0;
            foreach (var row in block.Split(Environment.NewLine))
                width = Math.Max(width, AnsiText.VisibleLength(row));

            return width;
        }

        /// <summary>The five most recent readings, rounded, oldest first.</summary>
        /// <returns>The readings both of the "recent readings" charts are drawn from.</returns>
        private IReadOnlyList<double> RecentValues()
        {
            var start = Math.Max(0, _samples.Count - RecentReadings);
            var values = new List<double>(RecentReadings);
            for (var i = start; i < _samples.Count; i++)
                values.Add(Math.Round(_samples[i]));

            return values;
        }

        /// <summary>The recent readings as labelled bars (oldest to newest, newest labelled "now").</summary>
        /// <param name="values">The readings, oldest first.</param>
        /// <returns>The labelled bars.</returns>
        private static IEnumerable<BarChartValue> RecentBars(IReadOnlyList<double> values)
        {
            var bars = new List<BarChartValue>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var fromEnd = values.Count - 1 - i;
                var label = fromEnd == 0 ? "now" : "-" + fromEnd.ToString(CultureInfo.InvariantCulture);

                // Deliberately the two-argument form even though BarChartValue can now carry a style of its own: a
                // per-item style beats the ramp outright, so calling the newest reading out that way would cost it
                // the very heat color the chart exists to show. The per-item override is for charts whose colors are
                // categorical ("this series is errors and it is red"), which this one's are not.
                bars.Add(new BarChartValue(label, values[i]));
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


    }
}
