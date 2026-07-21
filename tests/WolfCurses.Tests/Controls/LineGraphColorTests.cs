using System;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins the color half of <see cref="LineGraph" /> — the most escape-hostile widget in the library, because
    ///     <see cref="LineGraphTests" /> measures raw character positions (<c>line[1]</c>, <c>line.Length</c>,
    ///     <c>StartsWith("100")</c>) with no notion of visible width. So the first thing asserted here is that an
    ///     untouched graph emits nothing at all in <em>every</em> color mode, and the second is that a resolved mode of
    ///     <see cref="AnsiColorModeEnum.None" /> does the same however loudly the styles were set.
    ///     <para>
    ///         The rest pins what colored output actually looks like: the plot area's per-cell style grid coalescing
    ///         neighbours into one run, blank cells and gap columns emitting nothing, the four chrome styles applying
    ///         independently of the plot, and — the reason a style grid exists at all — style written at the instant
    ///         the character is, so the area fill that a connecting segment overwrites is colored as the segment.
    ///     </para>
    ///     <para>
    ///         Every test pins a concrete <see cref="LineGraph.ColorMode" /> instead of touching <c>NO_COLOR</c> and
    ///         the process-wide detection cache, which this assembly's parallel collections are all reading at once.
    ///     </para>
    /// </summary>
    public class LineGraphColorTests
    {
        /// <summary>The sequence that closes any style the widget opened.</summary>
        private const string RESET = "\x1b[0m";

        private static LineGraph Bare()
        {
            return new LineGraph
            {
                Width = 3,
                Height = 3,
                ShowAxis = false,
                ShowScale = false,
                Connected = false,
                Fill = false,
                PointChar = '*'
            };
        }

        private static LineGraph Framed()
        {
            return new LineGraph
            {
                Width = 3,
                Height = 3,
                ShowAxis = true,
                ShowScale = true,
                Connected = false,
                Fill = false,
                PointChar = '*'
            };
        }

        private static LineGraph Detailed()
        {
            return new LineGraph
            {
                Width = 12,
                Height = 6,
                ShowAxis = true,
                ShowScale = true,
                Connected = true,
                Fill = true,
                PointChar = '*',
                AreaChar = '.'
            };
        }

        private static LineGraph DetailedStyled(AnsiColorModeEnum mode)
        {
            var graph = Detailed();
            graph.ColorMode = mode;
            graph.Ramp = ColorRamp.Traffic;
            graph.LineStyle = ConsoleColor.White;
            graph.AreaStyle = ConsoleColor.DarkGray;
            graph.AxisStyle = ConsoleColor.DarkCyan;
            graph.ScaleStyle = ConsoleColor.Yellow;
            return graph;
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.Auto)]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        [InlineData(AnsiColorModeEnum.None)]
        public void Render_DefaultStyles_AreByteIdenticalInEveryColorMode(AnsiColorModeEnum mode)
        {
            var bare = Bare();
            bare.ColorMode = mode;
            var framed = Framed();
            framed.ColorMode = mode;

            var plain =
                "  *" + Text.NL +
                " * " + Text.NL +
                "*  ";
            var chrome =
                "2│  *" + Text.NL +
                " │ * " + Text.NL +
                "0│*  " + Text.NL +
                " └───";

            // The literals LineGraphTests pins, produced whatever the mode says: nothing below even resolves a color
            // mode until some style is non-empty, so the whole apparatus is unreachable from a default graph.
            Assert.Equal(plain, bare.Render(new double[] {0, 1, 2}));
            Assert.Equal(chrome, framed.Render(new double[] {0, 1, 2}));
            // The char overload compares ordinally. The string overloads of Contains/DoesNotContain default to
            // CurrentCulture, where ESC is a zero-weight ignorable character - so DoesNotContain("\x1b", s) reports a
            // hit at position 0 of every string. Never search for an escape as a string.
            Assert.DoesNotContain('\x1b', bare.Render(new double[] {0, 1, 2}));
            Assert.DoesNotContain('\x1b', framed.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_ModeNone_EmitsNoEscapesEvenWithEveryStyleAndARampSet()
        {
            var series = new double[] {3, 1, 4, 1, 5, 9, 2, 6};

            var result = DetailedStyled(AnsiColorModeEnum.None).Render(series);

            // Not a subset of the escapes and not a bare reset: the styled graph is indistinguishable from the
            // untouched one, which is the same promise NO_COLOR gets everywhere else in the library.
            Assert.Equal(Detailed().Render(series), result);
            Assert.DoesNotContain('\x1b', result);
        }

        [Fact]
        public void Render_Colored_StrippedOfEscapes_IsTheUncoloredBlock()
        {
            var series = new double[] {3, 1, 4, 1, 5, 9, 2, 6};

            var stripped = StripEscapes(DetailedStyled(AnsiColorModeEnum.TrueColor).Render(series));

            // Escapes are inserted between cells and never instead of one — including the trailing spaces of a blank
            // row, which nothing is allowed to trim.
            Assert.Equal(Detailed().Render(series), stripped);
        }

        [Fact]
        public void Render_Ramp_ColorsEachColumnByItsOwnValue()
        {
            var graph = Bare();
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.Ramp = ColorRamp.Monochrome;

            // Fractions 0 / 0.5 / 1 over the auto window 0..2, so bottom-left is the ramp's first stop and top-right
            // its last. Blank cells carry no style at all, so they cost no escape.
            var expected =
                "  " + "\x1b[38;2;255;255;255m" + "*" + RESET + Text.NL +
                " " + "\x1b[38;2;128;128;128m" + "*" + RESET + " " + Text.NL +
                "\x1b[38;2;0;0;0m" + "*" + RESET + "  ";

            Assert.Equal(expected, graph.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_FlatSeries_ColorsTheWholeRowAsOneRun()
        {
            var graph = Bare();
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.Ramp = ColorRamp.Monochrome;

            // A degenerate scale has no fraction to offer so it samples the ramp's middle — the same 0.5 that has
            // always put a flat series on the middle row. Three identical neighbours share one open and one reset.
            var expected =
                "   " + Text.NL +
                "\x1b[38;2;128;128;128m" + "***" + RESET + Text.NL +
                "   ";

            Assert.Equal(expected, graph.Render(new double[] {5, 5, 5}));
        }

        [Fact]
        public void Render_LineStyle_CoalescesAdjacentSegmentCellsIntoOneRun()
        {
            var graph = Bare();
            graph.Connected = true;
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.LineStyle = ConsoleColor.Green;

            // Points and the vertical segments joining them share LineStyle, so a run of them is one escape rather
            // than one per cell — and the run closes before the row is handed to the newline join.
            var expected =
                "  " + "\x1b[92m" + "*" + RESET + Text.NL +
                " " + "\x1b[92m" + "**" + RESET + Text.NL +
                "\x1b[92m" + "**" + RESET + " ";

            Assert.Equal(expected, graph.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_AreaStyle_ColorsTheFillAndLeavesThePointsPlain()
        {
            var graph = Bare();
            graph.Fill = true;
            graph.AreaChar = '.';
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.AreaStyle = ConsoleColor.DarkGray;

            // LineStyle is left empty, which interns to "no style": the points stay plain while the wash beneath them
            // is colored, proving style is recorded per cell rather than per row.
            var expected =
                "  *" + Text.NL +
                " *" + "\x1b[90m" + "." + RESET + Text.NL +
                "*" + "\x1b[90m" + ".." + RESET;

            Assert.Equal(expected, graph.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_AreaFillOverwrittenByASegment_IsColoredAsTheSegment()
        {
            var graph = Bare();
            graph.Fill = true;
            graph.Connected = true;
            graph.AreaChar = '.';
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.LineStyle = ConsoleColor.Green;
            graph.AreaStyle = ConsoleColor.DarkGray;

            // Row 1 column 1 is written by the fill and then overwritten by the connecting segment. A style grid built
            // from what the code *intended* to draw would color it as area; it must be colored as the line, because
            // that is the glyph that actually survived.
            var lines = graph.Render(new double[] {0, 1, 2}).Split(Text.NL);

            Assert.Equal(" " + "\x1b[92m" + "**" + RESET, lines[1]);
            Assert.Equal("\x1b[92m" + "**" + RESET + "\x1b[90m" + "." + RESET, lines[2]);
        }

        [Fact]
        public void Render_AxisAndScaleStyles_ApplyWithoutTouchingThePlotArea()
        {
            var graph = Framed();
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.AxisStyle = ConsoleColor.DarkGray;
            graph.ScaleStyle = ConsoleColor.Yellow;

            // With no plot style and no ramp the plot area never grows a style grid, so it comes out exactly as the
            // uncolored widget drew it. The gutter's right-aligning blanks stay outside ScaleStyle — the padding is
            // layout, not label — and the corner plus the bottom rule go out as one run because they read as one line.
            var expected =
                "\x1b[93m2" + RESET + "\x1b[90m│" + RESET + "  *" + Text.NL +
                " " + "\x1b[90m│" + RESET + " * " + Text.NL +
                "\x1b[93m0" + RESET + "\x1b[90m│" + RESET + "*  " + Text.NL +
                " " + "\x1b[90m└───" + RESET;

            Assert.Equal(expected, graph.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_RampAndLineStyle_ComposeSoTheRampTakesOnlyTheForeground()
        {
            var graph = Bare();
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.Ramp = ColorRamp.Monochrome;
            graph.LineStyle = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(ConsoleColor.Black), true);

            // The ramp wins the foreground and the caller's bold and background ride along, all in one escape with the
            // parameters joined by ';' — bold first, then foreground, then background.
            var expected =
                "   " + Text.NL +
                "\x1b[1;38;2;128;128;128;40m" + "***" + RESET + Text.NL +
                "   ";

            Assert.Equal(expected, graph.Render(new double[] {5, 5, 5}));
        }

        [Fact]
        public void Render_NonFiniteSample_StillGapsAndTheBlankRowCarriesNoEscape()
        {
            var graph = Bare();
            graph.Connected = true;
            graph.Minimum = 0;
            graph.Maximum = 2;
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.Ramp = ColorRamp.Monochrome;

            var lines = graph.Render(new[] {0d, double.NaN, 2d}).Split(Text.NL);
            var visible = StripEscapes(graph.Render(new[] {0d, double.NaN, 2d})).Split(Text.NL);

            // The gap column is skipped before any style is resolved, so it holds no palette index and the row that is
            // nothing but gap is literally three spaces — no open, no reset, nothing to confuse a raw index assertion.
            Assert.Equal("   ", lines[1]);

            Assert.Equal(3, visible.Length);
            Assert.All(visible, line => Assert.Equal(' ', line[1]));
            Assert.Equal('*', visible[2][0]);
            Assert.Equal('*', visible[0][2]);
        }

        [Fact]
        public void Render_BothBoundsPinnedInverted_ColorFromTheReconciledWindow()
        {
            var graph = Bare();
            graph.Connected = true;
            graph.Minimum = 10;
            graph.Maximum = 0;
            graph.ColorMode = AnsiColorModeEnum.TrueColor;
            graph.Ramp = ColorRamp.Monochrome;

            var result = graph.Render(new double[] {0, 5, 10});

            var expected =
                "  *" + Text.NL +
                " **" + Text.NL +
                "** ";

            Assert.Equal(expected, StripEscapes(result));

            // The ramp must sample the *reconciled* window, not the inverted one: an un-swapped min>max gives a
            // negative range, which reports 0.5 for every value and would paint the whole line one mid gray.
            Assert.Contains("\x1b[38;2;0;0;0m", result, StringComparison.Ordinal);
            Assert.Contains("\x1b[38;2;255;255;255m", result, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_MaximumPinnedBelowAllData_KeepsScaleValidWhenColored()
        {
            var graph = new LineGraph
            {
                Width = 5,
                Height = 5,
                ShowAxis = true,
                ShowScale = true,
                Maximum = 100,
                PointChar = '*',
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.Traffic,
                ScaleStyle = ConsoleColor.Yellow,
                AxisStyle = ConsoleColor.DarkGray
            };

            // Strip first, deliberately. SGR parameter bodies are digit strings, so a substring search for "120" over
            // colored output can match an escape rather than a scale label and fail for a reason that has nothing to
            // do with the regression this pins.
            var visible = StripEscapes(graph.Render(new double[] {120, 150, 130, 140, 160}));
            var lines = visible.Split(Text.NL);

            Assert.Equal(6, lines.Length);
            Assert.DoesNotContain("120", visible, StringComparison.Ordinal);
            Assert.StartsWith("100", lines[0], StringComparison.Ordinal);
            Assert.StartsWith("100", lines[4], StringComparison.Ordinal);
            Assert.Contains('*', visible);
        }

        [Fact]
        public void Render_Empty_KeepsItsLayoutWhenColored()
        {
            var graph = DetailedStyled(AnsiColorModeEnum.TrueColor);

            var visible = StripEscapes(graph.Render(Array.Empty<double>()));

            // Nothing is plotted, so nothing carries a plot style; the chrome is still styled and the block is still
            // Height rows plus the axis, so a caller's surrounding layout does not move.
            Assert.Equal(Detailed().Render(Array.Empty<double>()), visible);
            Assert.Equal(7, visible.Split(Text.NL).Length);
            Assert.DoesNotContain('*', visible);
        }

        [Fact]
        public void Render_Palette256_UsesIndexedEscapesRatherThanTrueColorTriples()
        {
            var graph = Bare();
            graph.ColorMode = AnsiColorModeEnum.Palette256;
            graph.Ramp = ColorRamp.Monochrome;

            var result = graph.Render(new double[] {0, 1, 2});

            Assert.Contains("\x1b[38;5;", result, StringComparison.Ordinal);
            Assert.DoesNotContain("38;2;", result, StringComparison.Ordinal);
            Assert.Equal("  *" + Text.NL + " * " + Text.NL + "*  ", StripEscapes(result));
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(5, 0)]
        public void Render_NonPositiveDimensions_ThrowBeforeAnyColorWork(int width, int height)
        {
            var graph = DetailedStyled(AnsiColorModeEnum.TrueColor);
            graph.Width = width;
            graph.Height = height;

            // The dimension guards are still the first statements in Render: a ramp or style grid prepared above them
            // would change which exception a caller sees, or throw a different one first.
            Assert.Throws<ArgumentOutOfRangeException>(() => graph.Render(new double[] {1, 2, 3}));
        }

        private static string StripEscapes(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }
    }
}
