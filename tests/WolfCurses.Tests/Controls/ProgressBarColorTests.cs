using System;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Covers the coloring half of <see cref="ProgressBar" />, whose whole design constraint is that it must be
    ///     invisible until asked for. The two invariants pinned first are the ones the rest of the feature is built on:
    ///     an untouched bar emits byte-for-byte what it emitted before color existed (so <see cref="ProgressBarTests" />
    ///     keeps passing unedited), and a resolved <see cref="AnsiColorModeEnum.None" /> emits nothing at all even when
    ///     every style is explicitly set.
    ///     <para>
    ///         Every colored case pins a concrete <see cref="ProgressBar.ColorMode" /> rather than touching
    ///         <c>NO_COLOR</c> and <see cref="AnsiConsole.ResetColorModeCache" />. That is exactly why the property
    ///         exists: the detected mode is cached process-wide and these tests run in the parallel default collection,
    ///         so a test that mutated the environment would be racing every other test in the assembly.
    ///     </para>
    /// </summary>
    public class ProgressBarColorTests
    {
        private const char FILLED = '█';
        private const char EMPTY = '░';
        private const string RESET = "\x1b[0m";

        [Fact]
        public void Render_AllDefaultsAndAutoMode_IsByteIdenticalToThePreColorRendering()
        {
            // The compatibility contract. Note the color mode is left at its Auto default on purpose: the bar must
            // short-circuit before it ever asks the environment, so this passes on a true-color terminal too.
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(FILLED, 5) + new string(EMPTY, 5) + "  50%", bar.Render(0.5));
            Assert.Equal(new string(EMPTY, 10) + "   0%", bar.Render(0.0));
            Assert.Equal(new string(FILLED, 10) + " 100%", bar.Render(1.0));
            // The char overload compares ordinally. Never assert against an escape as a *string*: the string overloads
            // default to CurrentCulture, where ESC is a zero-weight ignorable character, so DoesNotContain("\x1b", s)
            // reports a hit at position 0 of every string.
            Assert.DoesNotContain('\x1b', bar.Render(0.5));

            var labelled = new ProgressBar {Width = 10, Label = "Load"};

            Assert.Equal("Load [" + new string(FILLED, 10) + "] 100%", labelled.Render(1.0));
        }

        [Fact]
        public void Render_EveryStyleSetButColorModeNone_EmitsNotOneEscape()
        {
            // Someone who set NO_COLOR asked for no escape sequences, not for a cheaper subset of them, so the
            // styles are dropped outright rather than degraded — and the output has to land on the plain path
            // exactly, not merely on something that looks like it.
            var plain = new ProgressBar {Width = 10, Label = "Load"};
            var muted = new ProgressBar
            {
                Width = 10,
                Label = "Load",
                ColorMode = AnsiColorModeEnum.None,
                FilledStyle = ConsoleColor.Green,
                EmptyStyle = ConsoleColor.DarkGray,
                LabelStyle = ConsoleColor.White,
                PercentageStyle = ConsoleColor.Yellow,
                BracketStyle = ConsoleColor.Gray,
                FillRamp = ColorRamp.PrideRainbow
            };

            Assert.Equal(plain.Render(0.5), muted.Render(0.5));
            Assert.DoesNotContain('\x1b', muted.Render(0.5));
        }

        [Fact]
        public void Render_SpreadRamp_GivesEachCellTheRampColorForItsPositionAcrossTheWidth()
        {
            // A six-cell bar under a six-stop stepped ramp is the flag case: one stripe per cell, in order, with no
            // stop skipped or repeated. That property is what SampleIndex's end-inclusive arithmetic buys, and it is
            // the whole reason a bar chart or a progress bar can render a flag at all.
            var bar = new ProgressBar
            {
                Width = 6,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                FillRamp = ColorRamp.PrideRainbow
            };

            var expected =
                "\x1b[38;2;228;3;3m" + FILLED + RESET +
                "\x1b[38;2;255;140;0m" + FILLED + RESET +
                "\x1b[38;2;255;237;0m" + FILLED + RESET +
                "\x1b[38;2;0;128;38m" + FILLED + RESET +
                "\x1b[38;2;0;77;255m" + FILLED + RESET +
                "\x1b[38;2;117;7;135m" + FILLED + RESET;

            Assert.Equal(expected, bar.Render(1.0));
        }

        [Fact]
        public void Render_SpreadRampOnAOneCellBar_TakesTheRampStartInsteadOfDividingByZero()
        {
            // A one-cell bar has no span between its first and last cell, and the obvious i / (Width - 1) hands
            // Sample a NaN that it would swallow silently. The degenerate case has to say what it means.
            var bar = new ProgressBar
            {
                Width = 1,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                FillRamp = ColorRamp.Rainbow
            };

            Assert.Equal("\x1b[38;2;255;0;0m" + FILLED + RESET, bar.Render(1.0));
        }

        [Fact]
        public void Render_LevelRamp_PaintsTheWholeFilledRunOneColorChosenByTheValue()
        {
            var bar = new ProgressBar
            {
                Width = 10,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                RampMode = ColorRampModeEnum.Level,
                FillRamp = ColorRamp.Stepped(
                    new Rgb24(255, 0, 0),
                    new Rgb24(255, 255, 0),
                    new Rgb24(0, 255, 0))
            };

            // Half way along a three-band stepped ramp is the middle band, and the entire run takes it — a
            // traffic light, not a gradient. The unfilled track stays uncolored because nothing styled it.
            Assert.Equal("\x1b[38;2;255;255;0m" + new string(FILLED, 5) + RESET + new string(EMPTY, 5),
                bar.Render(0.5));

            Assert.Equal("\x1b[38;2;0;255;0m" + new string(FILLED, 10) + RESET, bar.Render(1.0));
        }

        [Fact]
        public void Render_SpreadRamp_CoalescesNeighbouringCellsThatCameOutTheSameColor()
        {
            // Ten cells under a two-stop stepped ramp is two runs of five, so two open sequences and two resets.
            // Without coalescing it would be twenty escapes on a string rebuilt every frame, which is the whole
            // point of counting them here rather than merely asserting the colors appear.
            var bar = new ProgressBar
            {
                Width = 10,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                FillRamp = ColorRamp.Stepped(new Rgb24(255, 0, 0), new Rgb24(0, 0, 255))
            };

            var rendered = bar.Render(1.0);

            Assert.Equal(4, CountEscapes(rendered));
            Assert.Equal(
                "\x1b[38;2;255;0;0m" + new string(FILLED, 5) + RESET +
                "\x1b[38;2;0;0;255m" + new string(FILLED, 5) + RESET,
                rendered);
        }

        [Fact]
        public void Render_LevelRamp_IsAlwaysASingleRunHoweverWideTheBar()
        {
            var bar = new ProgressBar
            {
                Width = 40,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                RampMode = ColorRampModeEnum.Level,
                FillRamp = ColorRamp.Rainbow
            };

            Assert.Equal(2, CountEscapes(bar.Render(1.0)));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.02)]
        [InlineData(0.5)]
        [InlineData(0.95)]
        [InlineData(0.999)]
        [InlineData(1.0)]
        public void Render_Colored_StrippedOfEscapes_IsExactlyTheUncoloredRendering(double fraction)
        {
            // Color must be strictly additive: every glyph, every separating space and the exact percentage field
            // width survive it. This is the assertion that catches a style swallowing a space or a zero-length run
            // sneaking an open/reset pair between two characters that a layout test measures.
            var plain = new ProgressBar {Width = 12, Label = "Load"};
            var colored = new ProgressBar
            {
                Width = 12,
                Label = "Load",
                ColorMode = AnsiColorModeEnum.TrueColor,
                FilledStyle = ConsoleColor.Green,
                EmptyStyle = ConsoleColor.DarkGray,
                LabelStyle = ConsoleColor.White,
                PercentageStyle = ConsoleColor.Yellow,
                BracketStyle = ConsoleColor.Gray,
                FillRamp = ColorRamp.PrideRainbow
            };

            Assert.Equal(plain.Render(fraction), StripEscapes(colored.Render(fraction)));
        }

        [Fact]
        public void Render_ZeroPercentWithAStyledFill_EmitsNothingForTheEmptyFilledRun()
        {
            // A run of zero cells gets no open and no reset. An open/close pair around nothing is invisible on a
            // terminal and fatal to any test that measures the string.
            var bar = new ProgressBar
            {
                Width = 8,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                FilledStyle = ConsoleColor.Green,
                FillRamp = ColorRamp.Rainbow
            };

            Assert.Equal(new string(EMPTY, 8), bar.Render(0.0));
        }

        [Fact]
        public void Render_FullBarWithAStyledTrack_EmitsNothingForTheEmptyTrack()
        {
            var bar = new ProgressBar
            {
                Width = 8,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                EmptyStyle = ConsoleColor.DarkGray
            };

            Assert.Equal(new string(FILLED, 8), bar.Render(1.0));
        }

        [Theory]
        [InlineData(ConsoleColor.Blue, 94)]
        [InlineData(ConsoleColor.Red, 91)]
        [InlineData(ConsoleColor.DarkBlue, 34)]
        [InlineData(ConsoleColor.DarkRed, 31)]
        [InlineData(ConsoleColor.Cyan, 96)]
        [InlineData(ConsoleColor.DarkYellow, 33)]
        public void Render_NamedColors_UseTheAnsiCodeAndNotTheConsoleColorNumber(ConsoleColor color, int code)
        {
            // ConsoleColor is ordered blue-green-red (a DOS legacy) and ANSI is ordered red-green-blue, so blue and
            // red are swapped and so are cyan and yellow. Anything computed from the enum's numeric value produces a
            // bar that is red while claiming to be blue, and it looks entirely plausible in a screenshot.
            var bar = new ProgressBar
            {
                Width = 2,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                FilledStyle = color
            };

            Assert.Equal("\x1b[" + code + "m" + new string(FILLED, 2) + RESET, bar.Render(1.0));
        }

        [Fact]
        public void Render_LabelAndPercentageSeparatorSpaces_StayOutsideTheirStyles()
        {
            // The spaces separate two fields and belong to neither, so a background-colored label must not hang a
            // stray block off its end. Both are pinned because both are part of the byte contract of the plain path.
            var labelled = new ProgressBar
            {
                Width = 2,
                ShowBrackets = false,
                ShowPercentage = false,
                Label = "Load",
                ColorMode = AnsiColorModeEnum.TrueColor,
                LabelStyle = ConsoleColor.Green
            };

            Assert.Equal("\x1b[92mLoad" + RESET + " " + new string(FILLED, 2), labelled.Render(1.0));

            var percented = new ProgressBar
            {
                Width = 2,
                ShowBrackets = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                PercentageStyle = ConsoleColor.Yellow
            };

            Assert.Equal(new string(FILLED, 2) + " \x1b[93m100%" + RESET, percented.Render(1.0));
        }

        [Fact]
        public void Render_Brackets_AreStyledIndividuallyAndCloseAgain()
        {
            var bar = new ProgressBar
            {
                Width = 2,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BracketStyle = ConsoleColor.Gray
            };

            Assert.Equal("\x1b[37m[" + RESET + new string(FILLED, 2) + "\x1b[37m]" + RESET, bar.Render(1.0));
        }

        [Fact]
        public void Render_FillRamp_OverridesOnlyTheForegroundOfFilledStyle()
        {
            // A ramp laid over a styled fill is a composition, not a contest: the ramp owns the foreground and the
            // background and weight the caller asked for ride along with it, in one escape rather than two.
            var bar = new ProgressBar
            {
                Width = 4,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                FilledStyle = new TextStyle(new TextColor(new Rgb24(1, 2, 3)),
                    new TextColor(new Rgb24(10, 20, 30)), true),
                FillRamp = ColorRamp.Stepped(new Rgb24(200, 100, 50))
            };

            Assert.Equal("\x1b[1;38;2;200;100;50;48;2;10;20;30m" + new string(FILLED, 4) + RESET, bar.Render(1.0));
        }

        [Fact]
        public void Render_Grayscale_NeverEmitsATrueColorTriple()
        {
            // Grayscale means the palette restricted to gray shades, so an exact ramp color is downgraded through
            // the 256-color gray ramp rather than being allowed to sneak real color past the mode.
            var bar = new ProgressBar
            {
                Width = 8,
                ShowBrackets = false,
                ShowPercentage = false,
                ColorMode = AnsiColorModeEnum.Grayscale,
                FillRamp = ColorRamp.PrideRainbow
            };

            var rendered = bar.Render(1.0);

            Assert.DoesNotContain("38;2;", rendered);
            Assert.Contains("38;5;", rendered);
        }

        [Fact]
        public void Render_EveryOpenedStyleIsClosed()
        {
            // Structural rather than literal: whatever the fields end up being, the bar never leaves a style hanging
            // for whatever the window draws next, and never emits a reset for a style it did not open.
            var bar = new ProgressBar
            {
                Width = 12,
                Label = "Load",
                ColorMode = AnsiColorModeEnum.TrueColor,
                FilledStyle = ConsoleColor.Green,
                EmptyStyle = ConsoleColor.DarkGray,
                LabelStyle = ConsoleColor.White,
                PercentageStyle = ConsoleColor.Yellow,
                BracketStyle = ConsoleColor.Gray,
                FillRamp = ColorRamp.PrideRainbow
            };

            var rendered = bar.Render(0.5);
            var resets = Regex.Matches(rendered, @"\x1b\[0m").Count;

            Assert.True(resets > 0);
            Assert.Equal(resets, CountEscapes(rendered) - resets);
            Assert.EndsWith(RESET, rendered, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_WidthLessThanOne_ThrowsBeforeAnyRampWork()
        {
            // The width guard is the first statement in Render and has to stay there: a ramp precomputed above it
            // would divide by a zero span and change which exception a caller sees.
            var bar = new ProgressBar
            {
                Width = 0,
                ColorMode = AnsiColorModeEnum.TrueColor,
                FilledStyle = ConsoleColor.Green,
                FillRamp = ColorRamp.Rainbow
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => bar.Render(0.5));
        }

        private static string StripEscapes(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        private static int CountEscapes(string text)
        {
            return Regex.Matches(text, @"\x1b\[[0-9;]*m").Count;
        }
    }
}
