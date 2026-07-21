using System;
using System.Collections.Generic;
using System.Linq;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins the rule the three cell-by-cell widgets — <see cref="ProgressBar" /> in
    ///     <see cref="ColorRampModeEnum.Spread" />, <see cref="Sparkline" /> with a ramp, and
    ///     <see cref="LineGraph" />'s plot area — coalesce runs by: <b>a run ends only where the escape sequence that
    ///     reaches the terminal changes.</b>
    ///     <para>
    ///         They originally compared the <em>source</em> value instead — a raw <see cref="Rgb24" /> or a
    ///         <see cref="TextStyle" /> — which is a different question in every indexed mode. Quantization happens
    ///         downstream in <see cref="TextColor" />: <see cref="AnsiColorModeEnum.Grayscale" /> has 26 possible
    ///         sequences in all and <see cref="AnsiColorModeEnum.Palette256" /> has 256, so a smooth ramp across
    ///         sixty columns hands out sixty distinct colors that arrive as a couple of dozen distinct escapes. The
    ///         picture was correct either way, which is exactly why no byte-exact test caught it: the cost was a
    ///         reset and a re-open between neighbouring cells the terminal draws identically, on a string the example
    ///         dashboard rebuilds inside <c>OnRenderForm</c>.
    ///     </para>
    ///     <para>
    ///         Every test here pins a concrete <c>ColorMode</c> rather than touching <c>NO_COLOR</c> and the
    ///         process-wide detection cache, which the rest of this parallel assembly is reading at the same time.
    ///     </para>
    /// </summary>
    public class WidgetRunCoalescingTests
    {
        private static readonly AnsiColorModeEnum[] _indexedModes =
            {AnsiColorModeEnum.Palette256, AnsiColorModeEnum.Grayscale};

        private static double[] Ramp60()
        {
            return Enumerable.Range(0, 60).Select(i => (double) i).ToArray();
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void LineGraph_NeverClosesARunOnlyToReopenTheSameEscape(AnsiColorModeEnum mode)
        {
            var graph = new LineGraph
            {
                Width = 60,
                Height = 10,
                Fill = true,
                Connected = true,
                Ramp = ColorRamp.Rainbow,
                ColorMode = mode
            };

            var rendered = graph.Render(Ramp60());

            // The regression measured 147 of these in Grayscale and 181 in Palette256 — visually invisible, and
            // about 2 KB per frame of escapes that changed nothing.
            Assert.Equal(0, AnsiRuns.CountRedundantRuns(rendered));
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void Sparkline_NeverClosesARunOnlyToReopenTheSameEscape(AnsiColorModeEnum mode)
        {
            var spark = new Sparkline {SparklineColorRamp = ColorRamp.Rainbow, ColorMode = mode};

            Assert.Equal(0, AnsiRuns.CountRedundantRuns(spark.Render(Ramp60())));
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void ProgressBarSpread_NeverClosesARunOnlyToReopenTheSameEscape(AnsiColorModeEnum mode)
        {
            var bar = new ProgressBar
            {
                Width = 40,
                ShowPercentage = false,
                ShowBrackets = false,
                FillRamp = ColorRamp.Rainbow,
                RampMode = ColorRampModeEnum.Spread,
                ColorMode = mode
            };

            Assert.Equal(0, AnsiRuns.CountRedundantRuns(bar.Render(1.0)));
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void QuantizedModesActuallyMergeRunsRatherThanMerelyNotSplittingThem(AnsiColorModeEnum mode)
        {
            // The counter above would pass on output that emitted no escapes at all, so this is the other half:
            // an indexed mode has strictly fewer sequences to say than true color does, and the widgets must spend
            // strictly fewer. If they matched true color's count, they would be splitting on the pre-quantization
            // value again and the redundancy check would just be measuring a different arrangement of the same bytes.
            var series = Ramp60();

            var trueColorEscapes = EscapeCount(series, AnsiColorModeEnum.TrueColor);
            var indexedEscapes = EscapeCount(series, mode);

            Assert.True(indexedEscapes < trueColorEscapes,
                $"{mode} spent {indexedEscapes} escapes where TrueColor spent {trueColorEscapes}.");
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void CoalescingChangesOnlyTheEscapes_NeverTheGlyphs(AnsiColorModeEnum mode)
        {
            // Merging runs must be invisible: strip the escapes and the picture is the uncolored widget's, right down
            // to the trailing spaces of a blank plot row.
            var series = Ramp60();

            var graph = new LineGraph {Width = 60, Height = 10, Fill = true, Connected = true};
            var plain = graph.Render(series);
            graph.Ramp = ColorRamp.Rainbow;
            graph.ColorMode = mode;

            Assert.Equal(plain, AnsiRuns.Strip(graph.Render(series)));

            var spark = new Sparkline();
            var plainSpark = spark.Render(series);
            spark.SparklineColorRamp = ColorRamp.Rainbow;
            spark.ColorMode = mode;

            Assert.Equal(plainSpark, AnsiRuns.Strip(spark.Render(series)));

            var bar = new ProgressBar {Width = 40, FillRamp = ColorRamp.Rainbow, ColorMode = mode};
            var plainBar = new ProgressBar {Width = 40}.Render(1.0);

            Assert.Equal(plainBar, AnsiRuns.Strip(bar.Render(1.0)));
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void EveryOpenSequenceEmittedIsDifferentFromTheOneBeforeIt(AnsiColorModeEnum mode)
        {
            // The same invariant said the other way round, over the whole block rather than per adjacency: the opens
            // a widget emits, read in order, must never repeat the previous one back to back. This catches a coalescer
            // that merged only some of the quantized neighbours.
            var graph = new LineGraph
            {
                Width = 60,
                Height = 10,
                Connected = true,
                Ramp = ColorRamp.Heat,
                ColorMode = mode
            };

            foreach (var line in graph.Render(Ramp60()).Split(Environment.NewLine))
            {
                var opens = AnsiRuns.Escapes(line)
                    .Where(e => !string.Equals(e, "\x1b[0m", StringComparison.Ordinal))
                    .ToList();

                for (var i = 1; i < opens.Count; i++)
                    Assert.NotEqual(opens[i - 1], opens[i]);
            }
        }

        [Fact]
        public void TheRedundancyCounterItselfFindsAHandBuiltRedundantPair()
        {
            // The counter is doing the work in every assertion above, so it gets its own proof that it is not simply
            // returning zero: a close immediately followed by the identical open is one redundant pair, while the same
            // open separated by a glyph is two honest runs, and a run closed at a line break is not redundancy at all.
            Assert.Equal(1, AnsiRuns.CountRedundantRuns("\x1b[31ma\x1b[0m\x1b[31mb\x1b[0m"));
            Assert.Equal(0, AnsiRuns.CountRedundantRuns("\x1b[31ma\x1b[0mx\x1b[31mb\x1b[0m"));
            Assert.Equal(0, AnsiRuns.CountRedundantRuns("\x1b[31ma\x1b[0m" + Text.NL + "\x1b[31mb\x1b[0m"));
            Assert.Equal(0, AnsiRuns.CountRedundantRuns("plain text"));
        }

        [Fact]
        public void IndexedModesStillCoalesceWhenTheStyleCarriesMoreThanAColor()
        {
            // A ramp laid over a bold, backgrounded style: the escape is a multi-parameter body, so a coalescer that
            // compared only the foreground fragment (or the Rgb24 behind it) would still split. It is the whole
            // resolved sequence that decides.
            var spark = new Sparkline
            {
                SparklineColorRamp = ColorRamp.Rainbow,
                ColorMode = AnsiColorModeEnum.Grayscale,
                Style = new TextStyle(null, new TextColor(ConsoleColor.Black), true)
            };

            var rendered = spark.Render(Ramp60());

            Assert.Equal(0, AnsiRuns.CountRedundantRuns(rendered));
            Assert.Contains("1;38;5;", rendered, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(IndexedModes))]
        public void ADefaultWidgetStillEmitsNothingAtAllInEveryIndexedMode(AnsiColorModeEnum mode)
        {
            // The coalescing rework touched the styled paths only; the uncolored path has to remain a path that never
            // asks a style anything, in every mode, or the whole byte-identical-defaults invariant goes with it.
            var graph = new LineGraph {Width = 10, Height = 4, ColorMode = mode};
            var spark = new Sparkline {ColorMode = mode};
            var bar = new ProgressBar {Width = 10, ColorMode = mode};

            Assert.DoesNotContain('\x1b', graph.Render(Ramp60()));
            Assert.DoesNotContain('\x1b', spark.Render(Ramp60()));
            Assert.DoesNotContain('\x1b', bar.Render(0.5));
        }

        public static IEnumerable<object[]> IndexedModes()
        {
            foreach (var mode in _indexedModes)
                yield return new object[] {mode};
        }

        private static int EscapeCount(double[] series, AnsiColorModeEnum mode)
        {
            var graph = new LineGraph
            {
                Width = 60,
                Height = 10,
                Fill = true,
                Connected = true,
                Ramp = ColorRamp.Rainbow,
                ColorMode = mode
            };

            return AnsiRuns.Escapes(graph.Render(series)).Count;
        }
    }
}
