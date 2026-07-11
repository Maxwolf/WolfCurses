// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     A live dashboard that shows the standard progress and graph controls working together: a determinate
    ///     <see cref="ProgressBar" />, an indeterminate <see cref="MarqueeBar" />, an inline <see cref="Sparkline" />,
    ///     a <see cref="BarChart" /> of the most recent readings, and a scrolling <see cref="LineGraph" />. A synthetic
    ///     signal advances one step per simulation tick so everything animates; state is only mutated on the simulation
    ///     tick (not the many fast system ticks) so <see cref="OnRenderForm" /> stays a pure read of the current frame.
    ///     Pressing ENTER returns to the menu.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class ProgressGraphsDialog : Form<ExampleWindowInfo>
    {
        private readonly ProgressBar _download = new ProgressBar {Label = "Download ", Width = 24};
        private readonly MarqueeBar _marquee = new MarqueeBar();
        private readonly List<double> _samples = new List<double>();
        private readonly Sparkline _spark = new Sparkline();

        private BarChart _barChart;
        private int _capacity = 48;
        private int _downloadPercent;
        private LineGraph _lineGraph;
        private string _marqueeFrame = string.Empty;
        private int _phase;

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
            var graphHeight = Math.Clamp(SafeWindowHeight() - 18, 5, 12);
            _capacity = graphWidth;

            _lineGraph = new LineGraph
            {
                Width = graphWidth,
                Height = graphHeight,
                Minimum = 0,
                Maximum = 100,
                Fill = true
            };
            _barChart = new BarChart {Width = 24, ShowTrack = true};
            _spark.Minimum = 0;
            _spark.Maximum = 100;

            // Seed a full window of history so the graph is already populated on the first frame, then let it scroll.
            for (_phase = 0; _phase < _capacity; _phase++)
                _samples.Add(Wave(_phase));

            _marqueeFrame = _marquee.Step();

            ParentWindow.PromptText = "Press ENTER to return to the menu";
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
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            var current = _samples.Count > 0 ? _samples[_samples.Count - 1] : 0d;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Progress bars & graphs (live)");
            sb.AppendLine();

            // Determinate progress bar and the indeterminate marquee side by side conceptually.
            sb.AppendLine(_download.Render(_downloadPercent, 100));
            sb.Append("Working  ").Append(_marqueeFrame); // MarqueeBar.Step already ends the line.

            // Inline sparkline of the whole visible history.
            sb.Append("Signal   ").Append(_spark.Render(_samples));
            sb.AppendFormat(CultureInfo.InvariantCulture, "  {0,3}%", (int) Math.Round(current));
            sb.AppendLine();
            sb.AppendLine();

            // Bar chart of the five most recent readings.
            sb.AppendLine("Recent readings:");
            sb.AppendLine(_barChart.Render(RecentBars()));
            sb.AppendLine();

            // The scrolling line graph.
            sb.AppendLine("Signal over time:");
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
