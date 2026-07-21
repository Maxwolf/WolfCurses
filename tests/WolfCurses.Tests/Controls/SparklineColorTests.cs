using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins the color half of <see cref="Sparkline" />. Two promises carry the whole compatibility story and are
    ///     asserted here from both directions: an untouched sparkline emits byte-for-byte what it emitted before it
    ///     knew what a color was (in <em>every</em> color mode, since a default style must not even consult one), and a
    ///     resolved mode of <see cref="AnsiColorModeEnum.None" /> emits nothing at all however loudly the styles were
    ///     set — the <c>NO_COLOR</c> stance the rest of the library takes.
    ///     <para>
    ///         Every test pins a concrete <see cref="Sparkline.ColorMode" /> rather than reaching for <c>NO_COLOR</c>
    ///         and <see cref="AnsiConsole.ResetColorModeCache" />: the detection cache is process-wide and this
    ///         assembly runs its collections in parallel, so a test that moved it would be racing every other widget
    ///         test in the run. That per-instance property existing at all is largely why.
    ///     </para>
    ///     <para>
    ///         Note the property name: the glyph ramp has been <see cref="Sparkline.Ramp" /> (a string) since 2026-07-11,
    ///         so the color ramp had to be <see cref="Sparkline.SparklineColorRamp" />. Do not "tidy" that.
    ///     </para>
    /// </summary>
    public class SparklineColorTests
    {
        /// <summary>The sequence that closes any style the widget opened.</summary>
        private const string RESET = "\x1b[0m";

        [Theory]
        [InlineData(AnsiColorModeEnum.Auto)]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        [InlineData(AnsiColorModeEnum.None)]
        public void Render_DefaultStyles_AreByteIdenticalInEveryColorMode(AnsiColorModeEnum mode)
        {
            var spark = new Sparkline {ColorMode = mode};

            var result = spark.Render(new double[] {0, 1, 2, 3, 4, 5, 6, 7});

            // The exact literal SparklineTests pins, produced whatever the mode says: an empty style is the identity
            // function on the text, so the color mode never gets a chance to matter.
            Assert.Equal(Sparkline.DefaultRamp, result);

            // The char overload compares ordinally. The string overloads of Contains/DoesNotContain default to
            // CurrentCulture, where ESC is a zero-weight ignorable character - so DoesNotContain("\x1b", s) reports a
            // hit at position 0 of every string. Never search for an escape as a string.
            Assert.DoesNotContain('\x1b', result);
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.Auto)]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.None)]
        public void Render_DefaultStyles_KeepEveryPinnedDegenerateCase(AnsiColorModeEnum mode)
        {
            Assert.Equal("▁", new Sparkline {ColorMode = mode}.Render(new double[] {42}));
            Assert.Equal("▁▁▁", new Sparkline {ColorMode = mode}.Render(new double[] {3, 3, 3}));
            Assert.Equal("▁▁█", new Sparkline {ColorMode = mode}.Render(new[] {0d, double.NaN, 10d}));
            Assert.Equal(".:#", new Sparkline {ColorMode = mode, Ramp = ".:#"}.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_ModeNone_EmitsNoEscapesEvenWithAStyleAndARampSet()
        {
            var spark = new Sparkline
            {
                ColorMode = AnsiColorModeEnum.None,
                Style = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(ConsoleColor.White), true),
                SparklineColorRamp = ColorRamp.PrideRainbow
            };

            var result = spark.Render(new double[] {0, 1, 2, 3, 4, 5, 6, 7});

            // Not "a subset of the escapes" and not a bare reset: somebody who asked for no escape sequences gets
            // none, so the line is indistinguishable from the uncolored one.
            Assert.Equal(Sparkline.DefaultRamp, result);
            Assert.DoesNotContain('\x1b', result);
        }

        [Fact]
        public void Render_Style_WrapsTheWholeLineInExactlyOneRun()
        {
            var spark = new Sparkline {ColorMode = AnsiColorModeEnum.TrueColor, Style = ConsoleColor.Green};

            // A named color stays named even in true color — the terminal's own theme decides the shade — so this is
            // the aixterm bright-green code and not a 38;2 triple.
            Assert.Equal("\x1b[92m" + Sparkline.DefaultRamp + RESET,
                spark.Render(new double[] {0, 1, 2, 3, 4, 5, 6, 7}));
        }

        [Fact]
        public void Render_ColorRamp_ColorsEachGlyphByItsOwnValue()
        {
            var spark = new Sparkline
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                SparklineColorRamp = ColorRamp.Traffic,
                Minimum = 0,
                Maximum = 10
            };

            // Fractions 0 / 0.5 / 1 pick the three Traffic stops exactly, and the glyph heights (levels 0, 4, 7) come
            // from the same fractions — color and height can never disagree.
            var expected =
                "\x1b[38;2;0;200;83m" + "▁" + RESET +
                "\x1b[38;2;255;214;0m" + "▅" + RESET +
                "\x1b[38;2;213;0;0m" + "█" + RESET;

            Assert.Equal(expected, spark.Render(new double[] {0, 5, 10}));
        }

        [Fact]
        public void Render_ColorRamp_CoalescesAdjacentEqualColorsIntoOneRun()
        {
            var spark = new Sparkline
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                SparklineColorRamp = ColorRamp.Monochrome,
                Minimum = 0,
                Maximum = 5
            };

            // Four glyphs, two colors, two runs. Re-wrapping every glyph would be four times the escapes saying the
            // same thing, and the reset for a run is emitted immediately before the next open rather than trailing it.
            var expected =
                "\x1b[38;2;0;0;0m" + "▁▁" + RESET +
                "\x1b[38;2;255;255;255m" + "██" + RESET;

            Assert.Equal(expected, spark.Render(new double[] {0, 0, 5, 5}));
        }

        [Fact]
        public void Render_NonFiniteSample_TakesTheRampsFirstColorAndJoinsTheRunBesideIt()
        {
            var spark = new Sparkline {ColorMode = AnsiColorModeEnum.TrueColor, SparklineColorRamp = ColorRamp.Monochrome};

            // The NaN already draws as the lowest glyph, so it takes Sample(0) — the only color that agrees with the
            // height it is being drawn at — and therefore coalesces with the genuine zero next to it.
            var expected =
                "\x1b[38;2;0;0;0m" + "▁▁" + RESET +
                "\x1b[38;2;255;255;255m" + "█" + RESET;

            Assert.Equal(expected, spark.Render(new[] {0d, double.NaN, 10d}));
        }

        [Fact]
        public void Render_ColorRamp_OverridesOnlyTheForegroundAndKeepsBoldAndBackground()
        {
            var spark = new Sparkline
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                Style = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(ConsoleColor.Black), true),
                SparklineColorRamp = ColorRamp.Monochrome
            };

            // One flat sample: fraction 0 -> the ramp's black foreground, while the caller's bold and black background
            // ride along. All three parameters go out in one escape, bold first.
            Assert.Equal("\x1b[1;38;2;0;0;0;40m" + "▁" + RESET, spark.Render(new double[] {42}));
        }

        [Fact]
        public void Render_Colored_StrippedOfEscapes_IsTheUncoloredLine()
        {
            var series = new double[] {3, 1, 4, 1, 5, 9, 2, 6};
            var colored = new Sparkline
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                Style = ConsoleColor.Cyan,
                SparklineColorRamp = ColorRamp.PrideTrans
            };

            // The property that makes color safe to add to a layout widget: escapes are inserted between glyphs and
            // never instead of one, so the visible line is untouched.
            Assert.Equal(new Sparkline().Render(series), StripEscapes(colored.Render(series)));
        }

        [Fact]
        public void Render_Grayscale_DowngradesANamedColorRatherThanLettingItThrough()
        {
            var green = new Sparkline {Style = ConsoleColor.Green};

            green.ColorMode = AnsiColorModeEnum.TrueColor;
            var trueColor = green.Render(new double[] {1});

            green.ColorMode = AnsiColorModeEnum.Grayscale;
            var grayscale = green.Render(new double[] {1});

            // Grayscale means "the palette restricted to gray shades", so the named code must not survive it — a
            // widget that hard-coded true color instead of passing its resolved mode down would produce 92 twice.
            Assert.Equal("\x1b[92m▁" + RESET, trueColor);
            Assert.StartsWith("\x1b[38;5;", grayscale, StringComparison.Ordinal);
            Assert.DoesNotContain("\x1b[92m", grayscale, StringComparison.Ordinal);
            Assert.DoesNotContain("38;2;", grayscale);
            Assert.Equal("▁", StripEscapes(grayscale));
        }

        [Fact]
        public void Render_Palette256_UsesIndexedEscapesRatherThanTrueColorTriples()
        {
            var spark = new Sparkline
            {
                ColorMode = AnsiColorModeEnum.Palette256,
                SparklineColorRamp = ColorRamp.Monochrome,
                Minimum = 0,
                Maximum = 1
            };

            var result = spark.Render(new double[] {0, 1});

            Assert.Contains("\x1b[38;5;", result, StringComparison.Ordinal);
            Assert.DoesNotContain("38;2;", result, StringComparison.Ordinal);
            Assert.Equal("▁█", StripEscapes(result));
        }

        [Fact]
        public void Render_EmptyOrNullSeries_StaysEmptyEvenWhenStyled()
        {
            var spark = new Sparkline
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                Style = ConsoleColor.Red,
                SparklineColorRamp = ColorRamp.PrideRainbow
            };

            // The early returns sit above every scrap of color work, so a styled sparkline with nothing to draw does
            // not emit a lonely open/reset pair into the caller's layout.
            Assert.Equal(string.Empty, spark.Render(new double[0]));
            Assert.Equal(string.Empty, spark.Render(null));
        }

        private static string StripEscapes(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }
    }
}
