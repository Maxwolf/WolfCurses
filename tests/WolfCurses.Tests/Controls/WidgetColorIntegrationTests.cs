using System;
using System.Linq;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The cross-cutting contract every colored widget owes the rest of the library: an escape sequence must be
    ///     decoration and nothing else.
    ///     <para>
    ///         The reason is <see cref="ConsolePresenter" />. It diffs frames row by row, overwrites each changed row
    ///         in place, and erases the leftover tail with "erase to end of line" — except on a row whose visible
    ///         width exactly fills the console, where with auto-wrap off the cursor ends <em>on</em> the last column
    ///         and the erase would blank the character just written. Both of those decisions are made from
    ///         <see cref="ConsolePresenter.VisibleLength" />. So if a widget's coloring changed how wide the
    ///         presenter thinks a row is, the symptom would not be a wrong color — it would be a row losing its last
    ///         character, or a stale tail never being cleaned up, several layers away from the widget that caused it.
    ///     </para>
    ///     <para>
    ///         Every test therefore renders a widget twice, once plain and once colored with the identical geometry,
    ///         and asserts that the colored rows strip back to the plain rows character for character and that the
    ///         presenter measures them at exactly their stripped length. Alongside that sit the two invariants the
    ///         whole feature rests on: an untouched widget emits no escape at all, and
    ///         <see cref="AnsiColorModeEnum.None" /> emits none either however loudly it was styled.
    ///     </para>
    /// </summary>
    public class WidgetColorIntegrationTests
    {
        // A char, not a string, and deliberately so: the string overloads of Assert.Contains/DoesNotContain default to
        // CurrentCulture, where ESC is a zero-weight ignorable character - so DoesNotContain("\x1b", s) reports a hit
        // at position 0 of every string on earth, and Contains("\x1b", s) passes vacuously. The char overloads compare
        // ordinally, which is the only comparison that means anything about an escape.
        private const char ESCAPE = '\x1b';

        private static readonly double[] _series = {3, 7, 1, 9, 4, 8, 2, 6, 5, 10, 0, 7};

        [Fact]
        public void AColoredProgressBarMeasuresExactlyAsWideAsItsPlainTwin()
        {
            AssertColorIsPurelyDecoration(
                PlainBar().Render(0.42),
                ColoredBar(AnsiColorModeEnum.TrueColor).Render(0.42));
        }

        [Fact]
        public void AColoredBarChartMeasuresExactlyAsWideAsItsPlainTwin()
        {
            AssertColorIsPurelyDecoration(
                PlainChart().Render(PlainItems()),
                ColoredChart(AnsiColorModeEnum.TrueColor).Render(ColoredItems()));
        }

        [Fact]
        public void AColoredSparklineMeasuresExactlyAsWideAsItsPlainTwin()
        {
            AssertColorIsPurelyDecoration(
                PlainSparkline().Render(_series),
                ColoredSparkline(AnsiColorModeEnum.TrueColor).Render(_series));
        }

        [Fact]
        public void AColoredLineGraphMeasuresExactlyAsWideAsItsPlainTwin()
        {
            AssertColorIsPurelyDecoration(
                PlainGraph().Render(_series),
                ColoredGraph(AnsiColorModeEnum.TrueColor).Render(_series));
        }

        [Fact]
        public void AColoredMarqueeBarMeasuresExactlyAsWideAsItsPlainTwin()
        {
            var plain = new MarqueeBar();
            var colored = ColoredMarquee(AnsiColorModeEnum.TrueColor);

            for (var frame = 0; frame < 7; frame++)
                AssertColorIsPurelyDecoration(plain.Step(), colored.Step());
        }

        [Fact]
        public void AColoredBoxMeasuresExactlyAsWideAsItsPlainTwin()
        {
            AssertColorIsPurelyDecoration(
                PlainPanel().Render("All systems nominal." + Text.NL + "Ready."),
                ColoredPanel(AnsiColorModeEnum.TrueColor).Render("All systems nominal." + Text.NL + "Ready."));
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void EscapesStayZeroWidthInEveryColorMode(AnsiColorModeEnum mode)
        {
            // Grayscale is the interesting one: it rewrites even a named color into an indexed "38;5;n" body, so the
            // sequences are a different length in every mode. None of that may reach the visible width.
            AssertColorIsPurelyDecoration(PlainBar().Render(0.42), ColoredBar(mode).Render(0.42));
            AssertColorIsPurelyDecoration(PlainChart().Render(PlainItems()), ColoredChart(mode).Render(ColoredItems()));
            AssertColorIsPurelyDecoration(PlainSparkline().Render(_series), ColoredSparkline(mode).Render(_series));
            AssertColorIsPurelyDecoration(PlainGraph().Render(_series), ColoredGraph(mode).Render(_series));
            AssertColorIsPurelyDecoration(PlainPanel().Render("Ready."), ColoredPanel(mode).Render("Ready."));
        }

        [Fact]
        public void NoWidgetLetsAStyleSurviveItsLineBreak()
        {
            // Rows are joined with Environment.NewLine and handed to the presenter as separate rows. A style still
            // open at the end of a row would be worn by everything the frame draws below it.
            AssertEveryRowEndsPlain(ColoredBar(AnsiColorModeEnum.TrueColor).Render(0.42));
            AssertEveryRowEndsPlain(ColoredChart(AnsiColorModeEnum.TrueColor).Render(ColoredItems()));
            AssertEveryRowEndsPlain(ColoredSparkline(AnsiColorModeEnum.TrueColor).Render(_series));
            AssertEveryRowEndsPlain(ColoredGraph(AnsiColorModeEnum.TrueColor).Render(_series));
            AssertEveryRowEndsPlain(ColoredMarquee(AnsiColorModeEnum.TrueColor).Step());
            AssertEveryRowEndsPlain(ColoredPanel(AnsiColorModeEnum.TrueColor).Render("Ready."));
        }

        [Fact]
        public void EveryWidgetGoesCompletelyPlainInNoneColorMode()
        {
            // The NO_COLOR stance, asserted through the per-instance mode rather than the process-wide environment:
            // a resolved mode of None drops explicitly-set styles instead of degrading them.
            var none = AnsiColorModeEnum.None;

            AssertIdenticalAndPlain(PlainBar().Render(0.42), ColoredBar(none).Render(0.42));
            AssertIdenticalAndPlain(PlainChart().Render(PlainItems()), ColoredChart(none).Render(ColoredItems()));
            AssertIdenticalAndPlain(PlainSparkline().Render(_series), ColoredSparkline(none).Render(_series));
            AssertIdenticalAndPlain(PlainGraph().Render(_series), ColoredGraph(none).Render(_series));
            AssertIdenticalAndPlain(new MarqueeBar().Step(), ColoredMarquee(none).Step());
            AssertIdenticalAndPlain(PlainPanel().Render("Ready."), ColoredPanel(none).Render("Ready."));
        }

        [Fact]
        public void EveryUntouchedWidgetEmitsNoEscapeWhateverTheEnvironmentSays()
        {
            // Nothing here pins a ColorMode, so every widget is in the state a consumer who never heard of this
            // feature leaves it in. That state must never consult the environment, or this test would pass on CI and
            // fail on the author's terminal.
            Assert.DoesNotContain(ESCAPE, new ProgressBar {Width = 12, Label = "Load"}.Render(0.42));
            Assert.DoesNotContain(ESCAPE, new BarChart {Width = 16, ShowTrack = true}.Render(PlainItems()));
            Assert.DoesNotContain(ESCAPE, new Sparkline().Render(_series));
            Assert.DoesNotContain(ESCAPE, new LineGraph {Width = 12, Height = 5, Fill = true}.Render(_series));
            Assert.DoesNotContain(ESCAPE, new MarqueeBar().Step());
            Assert.DoesNotContain(ESCAPE, new Box {Title = "Panel", Padding = 1}.Render("Ready."));
        }

        [Fact]
        public void ColoredWidgetsNestedInAPlainBoxStillFrameUpSquare()
        {
            // Box is the one widget that measures somebody else's output, and it does so escape-blind through its
            // own regex. That regex and ConsolePresenter.VisibleLength have to agree about these sequences, or a
            // dashboard framing its own colored gauges comes out ragged.
            var content = ColoredBar(AnsiColorModeEnum.TrueColor).Render(0.42) + Text.NL +
                          ColoredSparkline(AnsiColorModeEnum.TrueColor).Render(_series) + Text.NL +
                          ColoredChart(AnsiColorModeEnum.TrueColor).Render(ColoredItems());

            var framed = new Box {Title = "Live", Padding = 1}.Render(content);

            Assert.Equal(new Box {Title = "Live", Padding = 1}.Render(StripEscapes(content)), StripEscapes(framed));

            var widths = framed.Split(Text.NL).Select(line => ConsolePresenter.VisibleLength(line)).Distinct()
                .ToArray();
            Assert.Single(widths);
        }

        [Fact]
        public void ARampedProgressBarStillFillsExactlyAsManyCellsAsAPlainOne()
        {
            // A spread ramp colors cell by cell and coalesces equal neighbours into runs, which is exactly the shape
            // of code that quietly drops or duplicates a cell at a run boundary. The stripped bar is the check.
            var plain = PlainBar();
            var ramped = ColoredBar(AnsiColorModeEnum.TrueColor);

            for (var step = 0; step <= 100; step++)
            {
                var fraction = step / 100d;

                Assert.Equal(plain.Render(fraction), StripEscapes(ramped.Render(fraction)));
            }
        }

        [Fact]
        public void AStrippedFlagChartIsStillTheSameBlockOfBarsAsAnUncoloredOne()
        {
            // The showcase configuration: no labels, no values, no separator, equal magnitudes, one stepped ramp
            // stop per row. It has to be nothing more than the plain chart wearing colors.
            var stripes = Enumerable.Range(0, ColorRamp.PrideRainbow.Stops.Count)
                .Select(row => new BarChartValue(string.Empty, 1))
                .ToArray();

            var plain = new BarChart
            {
                Width = 24, ShowValues = false, Separator = string.Empty,
                ColorMode = AnsiColorModeEnum.TrueColor
            };
            var flag = new BarChart
            {
                Width = 24, ShowValues = false, Separator = string.Empty,
                ColorMode = AnsiColorModeEnum.TrueColor, Ramp = ColorRamp.PrideRainbow
            };

            AssertColorIsPurelyDecoration(plain.Render(stripes), flag.Render(stripes));
        }

        private static ProgressBar PlainBar()
        {
            return new ProgressBar {Width = 12, Label = "Load", ColorMode = AnsiColorModeEnum.TrueColor};
        }

        private static ProgressBar ColoredBar(AnsiColorModeEnum mode)
        {
            return new ProgressBar
            {
                Width = 12,
                Label = "Load",
                ColorMode = mode,
                FillRamp = ColorRamp.PrideRainbow,
                EmptyStyle = ConsoleColor.DarkGray,
                LabelStyle = ConsoleColor.White,
                PercentageStyle = ConsoleColor.Cyan,
                BracketStyle = ConsoleColor.DarkGray
            };
        }

        private static BarChart PlainChart()
        {
            return new BarChart {Width = 16, ShowTrack = true, ColorMode = AnsiColorModeEnum.TrueColor};
        }

        private static BarChart ColoredChart(AnsiColorModeEnum mode)
        {
            return new BarChart
            {
                Width = 16,
                ShowTrack = true,
                ColorMode = mode,
                Ramp = ColorRamp.Traffic,
                RampMode = ColorRampModeEnum.Level,
                LabelStyle = ConsoleColor.White,
                ValueStyle = ConsoleColor.Cyan,
                SeparatorStyle = ConsoleColor.DarkGray,
                TrackStyle = ConsoleColor.DarkGray
            };
        }

        private static BarChartValue[] PlainItems()
        {
            return new[]
            {
                new BarChartValue("Wood", 12),
                new BarChartValue("Iron", 5),
                new BarChartValue("Gold", 20)
            };
        }

        private static BarChartValue[] ColoredItems()
        {
            // The middle item deliberately keeps the two-argument constructor, so one row takes the ramp color and
            // its neighbours take their own — the precedence rule exercised inside a single render.
            return new[]
            {
                new BarChartValue("Wood", 12, ConsoleColor.DarkGreen),
                new BarChartValue("Iron", 5),
                new BarChartValue("Gold", 20, ConsoleColor.Yellow)
            };
        }

        private static Sparkline PlainSparkline()
        {
            return new Sparkline {ColorMode = AnsiColorModeEnum.TrueColor};
        }

        private static Sparkline ColoredSparkline(AnsiColorModeEnum mode)
        {
            return new Sparkline
            {
                ColorMode = mode,
                Style = ConsoleColor.White,
                SparklineColorRamp = ColorRamp.Heat
            };
        }

        private static LineGraph PlainGraph()
        {
            return new LineGraph
            {
                Width = 12, Height = 5, Fill = true, ColorMode = AnsiColorModeEnum.TrueColor
            };
        }

        private static LineGraph ColoredGraph(AnsiColorModeEnum mode)
        {
            return new LineGraph
            {
                Width = 12,
                Height = 5,
                Fill = true,
                ColorMode = mode,
                Ramp = ColorRamp.Traffic,
                LineStyle = ConsoleColor.White,
                AreaStyle = ConsoleColor.DarkGray,
                AxisStyle = ConsoleColor.DarkGray,
                ScaleStyle = ConsoleColor.Cyan
            };
        }

        private static MarqueeBar ColoredMarquee(AnsiColorModeEnum mode)
        {
            return new MarqueeBar
            {
                ColorMode = mode,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.DarkGray
            };
        }

        private static Box PlainPanel()
        {
            return new Box {Title = "Panel", Padding = 1, MinimumWidth = 22};
        }

        private static Box ColoredPanel(AnsiColorModeEnum mode)
        {
            return new Box
            {
                Title = "Panel",
                Padding = 1,
                MinimumWidth = 22,
                ColorMode = mode,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };
        }

        /// <summary>
        ///     Asserts a colored render differs from its plain twin only by escape sequences that the presenter
        ///     counts as nothing: same row count, every row stripping back to the plain row, and every row measuring
        ///     at exactly its stripped length.
        /// </summary>
        private static void AssertColorIsPurelyDecoration(string plain, string colored)
        {
            // Guards against the test passing for the wrong reason — a widget that quietly emitted nothing would
            // satisfy every assertion below.
            Assert.NotEqual(plain, colored);
            Assert.Contains(ESCAPE, colored);

            var plainRows = plain.Split(Text.NL);
            var coloredRows = colored.Split(Text.NL);

            Assert.Equal(plainRows.Length, coloredRows.Length);

            for (var row = 0; row < plainRows.Length; row++)
            {
                var stripped = StripEscapes(coloredRows[row]);

                Assert.Equal(plainRows[row], stripped);
                Assert.Equal(stripped.Length, ConsolePresenter.VisibleLength(coloredRows[row]));
            }
        }

        private static void AssertIdenticalAndPlain(string plain, string suppressed)
        {
            Assert.Equal(plain, suppressed);
            Assert.DoesNotContain(ESCAPE, suppressed);
        }

        private static void AssertEveryRowEndsPlain(string rendered)
        {
            Assert.All(rendered.Split(Text.NL), row => Assert.True(EndsInThePlainState(row),
                "A row left a style open across the line break: " + row));
        }

        private static bool EndsInThePlainState(string line)
        {
            var open = false;
            foreach (Match match in Regex.Matches(line, @"\x1b\[[0-9;]*m"))
                open = match.Value != TextStyle.ResetSequence;

            return !open;
        }

        private static string StripEscapes(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }
    }
}
