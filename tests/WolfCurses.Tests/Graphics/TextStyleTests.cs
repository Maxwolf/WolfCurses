using System;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins <see cref="TextStyle" />, the type the whole colorable-widget feature rests on, and in particular the
    ///     two invariants the feature was allowed to ship under.
    ///     <para>
    ///         First, <b>an empty style is a guaranteed no-op</b>: every method hands its input straight back, the
    ///         same reference, with no escape and no reset. That is what lets every widget grow style properties while
    ///         the pinned byte-for-byte tests in <c>Controls/</c> keep passing unedited. Second, <b>a resolved mode of
    ///         <see cref="AnsiColorModeEnum.None" /> emits nothing at all</b>, even for a style the caller explicitly
    ///         set and even for bold — someone honouring <c>NO_COLOR</c> asked for no escape sequences, not for a
    ///         subset of them.
    ///     </para>
    ///     <para>
    ///         Every test passes an explicit mode. <see cref="AnsiColorModeEnum.Auto" /> would consult
    ///         <see cref="AnsiConsole.DetectColorMode" />, which is process-cached and environment-derived — untestable
    ///         without mutating global state that the rest of this parallel assembly is reading.
    ///     </para>
    /// </summary>
    public class TextStyleTests
    {
        private const string CSI = "\x1b[";
        private const string RESET = "\x1b[0m";

        [Fact]
        public void None_IsTheDefaultValueSoAnUntouchedWidgetPropertyIsAlreadyTheNoOp()
        {
            Assert.Equal(default(TextStyle), TextStyle.None);
            Assert.True(TextStyle.None.IsEmpty);
            Assert.True(default(TextStyle).IsEmpty);
            Assert.False(TextStyle.None.Foreground.HasValue);
            Assert.False(TextStyle.None.Background.HasValue);
            Assert.False(TextStyle.None.Bold);
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        [InlineData(AnsiColorModeEnum.None)]
        public void Apply_EmptyStyle_ReturnsTheVeryStringItWasGiven(AnsiColorModeEnum mode)
        {
            // Assert.Same, not Assert.Equal: reference identity is the strongest possible statement that nothing was
            // appended, prepended or reallocated. This is invariant one.
            var text = string.Concat("hel", "lo");

            Assert.Same(text, TextStyle.None.Apply(text, mode));
        }

        [Fact]
        public void Apply_EmptyStyleWithAutoMode_StillShortCircuitsWithoutConsultingTheEnvironment()
        {
            // Deterministic despite the Auto default, because IsEmpty is tested before the mode is ever resolved.
            var text = string.Concat("hel", "lo");

            Assert.Same(text, TextStyle.None.Apply(text));
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void Apply_NonEmptyStyle_WrapsTheTextInOneOpenAndOneReset(AnsiColorModeEnum mode)
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Green));

            var result = style.Apply("hi", mode);

            // Ordinal on purpose: the culture-sensitive default treats ESC as a zero-weight ignorable character, so
            // these would pass on any string merely containing "[" and "hi[0m" - proving nothing about the escapes.
            Assert.StartsWith(CSI, result, StringComparison.Ordinal);
            Assert.EndsWith("hi" + RESET, result, StringComparison.Ordinal);
            Assert.Equal(style.OpenSequence(mode) + "hi" + RESET, result);
        }

        [Fact]
        public void Apply_NonEmptyStyleInTrueColor_IsExactlyOpenTextReset()
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Green));

            Assert.Equal(CSI + "92mhi" + RESET, style.Apply("hi", AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void Apply_ModeNone_ReturnsTheInputUnchangedEvenForAStyleThatWasExplicitlySet()
        {
            // Invariant two. Foreground, background and bold all set, and still not one byte of escape.
            var style = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(new Rgb24(0, 0, 255)), true);
            var text = string.Concat("lou", "d");

            Assert.Same(text, style.Apply(text, AnsiColorModeEnum.None));
            Assert.Equal(string.Empty, style.OpenSequence(AnsiColorModeEnum.None));
        }

        [Fact]
        public void Apply_NullOrEmptyText_IsReturnedUnchangedEvenWithAStyleSet()
        {
            // Widgets routinely draw zero-length runs — a progress bar at 0% has an empty filled run, a bar chart row
            // for a negative value has an empty bar. An open/close pair around nothing would land escapes between two
            // spaces that the pinned layout tests measure.
            var style = new TextStyle(new TextColor(ConsoleColor.Red));

            Assert.Null(style.Apply(null, AnsiColorModeEnum.TrueColor));
            Assert.Equal(string.Empty, style.Apply(string.Empty, AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void ApplyRun_NonPositiveCount_ProducesNothingAtAll()
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Red));

            Assert.Equal(string.Empty, style.Apply('#', 0, AnsiColorModeEnum.TrueColor));
            Assert.Equal(string.Empty, style.Apply('#', -3, AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void ApplyRun_BuildsTheRunAndStylesItOnce()
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Red));

            Assert.Equal(CSI + "91m###" + RESET, style.Apply('#', 3, AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void ApplyRun_EmptyStyle_IsThePlainRun()
        {
            Assert.Equal("###", TextStyle.None.Apply('#', 3, AnsiColorModeEnum.TrueColor));
            Assert.Equal("###", TextStyle.None.Apply('#', 3, AnsiColorModeEnum.None));
        }

        [Fact]
        public void OpenSequence_BoldForegroundAndBackground_IsOneEscapeWithSemicolonJoinedParameters()
        {
            // The whole reason TextColor returns bare parameter bodies. One escape, not three adjacent ones: fewer
            // bytes on the wire and one fewer thing for a terminal to mis-parse.
            var style = new TextStyle(new TextColor(new Rgb24(255, 0, 0)), new TextColor(new Rgb24(255, 255, 255)),
                true);

            Assert.Equal(CSI + "1;38;2;255;0;0;48;2;255;255;255m", style.OpenSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void OpenSequence_ContainsExactlyOneEscapeAndOneTerminator()
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Yellow), new TextColor(ConsoleColor.DarkBlue), true);

            var open = style.OpenSequence(AnsiColorModeEnum.TrueColor);

            Assert.Equal(1, CountOf(open, '\x1b'));
            Assert.Equal(1, CountOf(open, 'm'));
            Assert.EndsWith("m", open);
            Assert.Equal(CSI + "1;93;44m", open);
        }

        [Fact]
        public void OpenSequence_ParameterOrderIsBoldThenForegroundThenBackground()
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Green), new TextColor(ConsoleColor.Black), true);

            Assert.Equal(CSI + "1;92;40m", style.OpenSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void OpenSequence_ForegroundOnly_EmitsNoStrayLeadingSemicolon()
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Green));

            Assert.Equal(CSI + "92m", style.OpenSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void OpenSequence_BackgroundOnly_EmitsNoStrayLeadingSemicolon()
        {
            // Green's foreground code is the bright 92, so its background is 102 — not 42, which belongs to DarkGreen.
            var style = new TextStyle(background: new TextColor(ConsoleColor.Green));

            Assert.Equal(CSI + "102m", style.OpenSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void OpenSequence_EmptyStyle_IsEmptyInEveryMode()
        {
            foreach (AnsiColorModeEnum mode in Enum.GetValues<AnsiColorModeEnum>())
            {
                if (mode == AnsiColorModeEnum.Auto)
                    continue; // Auto reads the environment; covered by the empty-style short-circuit test above.

                Assert.Equal(string.Empty, TextStyle.None.OpenSequence(mode));
            }
        }

        [Theory]
        [InlineData(AnsiColorModeEnum.TrueColor)]
        [InlineData(AnsiColorModeEnum.Palette256)]
        [InlineData(AnsiColorModeEnum.Grayscale)]
        public void OpenSequence_BoldWithoutColor_IsJustTheBoldAttribute(AnsiColorModeEnum mode)
        {
            var style = new TextStyle(bold: true);

            Assert.False(style.IsEmpty);
            Assert.Equal(CSI + "1m", style.OpenSequence(mode));
        }

        [Fact]
        public void OpenSequence_BoldInModeNone_IsStillNothing()
        {
            // Bold is an attribute rather than a color, so it is tempting to let it through. It does not go through:
            // a caller who asked for no escapes gets no escapes.
            var style = new TextStyle(bold: true);

            Assert.Equal(string.Empty, style.OpenSequence(AnsiColorModeEnum.None));
        }

        [Fact]
        public void OpenSequence_Palette256_DowngradesExactColorsToIndicesAndLeavesNamesAlone()
        {
            var exact = new TextStyle(new TextColor(new Rgb24(255, 0, 0)));
            var named = new TextStyle(new TextColor(ConsoleColor.Red));

            Assert.Equal(CSI + "38;5;196m", exact.OpenSequence(AnsiColorModeEnum.Palette256));
            Assert.Equal(CSI + "91m", named.OpenSequence(AnsiColorModeEnum.Palette256));
        }

        [Fact]
        public void OpenSequence_Grayscale_GreysBothKindsIncludingNamedColors()
        {
            // The named color must not sneak color through a mode whose entire job is to forbid it.
            var exact = new TextStyle(new TextColor(new Rgb24(255, 0, 0)));
            var named = new TextStyle(new TextColor(ConsoleColor.Red));

            Assert.Equal(CSI + "38;5;239m", exact.OpenSequence(AnsiColorModeEnum.Grayscale));
            Assert.Equal(CSI + "38;5;239m", named.OpenSequence(AnsiColorModeEnum.Grayscale));
        }

        [Fact]
        public void ResetSequence_IsThePlainSgrResetEveryWidgetClosesWith()
        {
            // Must be plain SGR: ConsolePresenter.VisibleLength and Box's escape-stripping regex both understand
            // ESC[...m and nothing more exotic.
            Assert.Equal("\x1b[0m", TextStyle.ResetSequence);
        }

        [Fact]
        public void ImplicitConversion_FromConsoleColor_ReachesTextStyleDirectly()
        {
            // C# never chains two user-defined implicit conversions, so ConsoleColor -> TextColor -> TextStyle does
            // not happen on its own. The operator has to sit on TextStyle itself, or bar.FilledStyle = ConsoleColor.Green
            // simply fails to compile.
            TextStyle style = ConsoleColor.Green;

            Assert.Equal(new TextColor(ConsoleColor.Green), style.Foreground.Value);
            Assert.False(style.Background.HasValue);
            Assert.False(style.Bold);
        }

        [Fact]
        public void ImplicitConversion_FromRgb24_ReachesTextStyleDirectly()
        {
            TextStyle style = new Rgb24(1, 2, 3);

            Assert.Equal(new TextColor(new Rgb24(1, 2, 3)), style.Foreground.Value);
            Assert.False(style.Background.HasValue);
        }

        [Fact]
        public void ImplicitConversion_FromTextColor_ReachesTextStyle()
        {
            TextStyle style = new TextColor(ConsoleColor.Cyan);

            Assert.Equal(new TextColor(ConsoleColor.Cyan), style.Foreground.Value);
        }

        [Fact]
        public void ImplicitConversion_AlsoWorksThroughANullableStyleParameter()
        {
            // The shape BarChartValue's three-argument overload relies on: a user-defined conversion followed by the
            // standard TextStyle -> TextStyle? lifting.
            TextStyle? style = ConsoleColor.Red;

            Assert.True(style.HasValue);
            Assert.Equal(new TextColor(ConsoleColor.Red), style.Value.Foreground.Value);
        }

        [Fact]
        public void WithForeground_ReturnsACopyAndLeavesTheOriginalAlone()
        {
            var original = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(ConsoleColor.Blue), true);

            var changed = original.WithForeground(new TextColor(ConsoleColor.Green));

            Assert.Equal(new TextColor(ConsoleColor.Green), changed.Foreground.Value);
            Assert.Equal(new TextColor(ConsoleColor.Blue), changed.Background.Value);
            Assert.True(changed.Bold);
            Assert.Equal(new TextColor(ConsoleColor.Red), original.Foreground.Value);
        }

        [Fact]
        public void WithForeground_Null_ClearsTheForegroundWithoutTouchingTheRest()
        {
            // The ramp path leans on this: a ramp owns the foreground while the caller's background and bold ride along.
            var original = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(ConsoleColor.Blue), true);

            var changed = original.WithForeground(null);

            Assert.False(changed.Foreground.HasValue);
            Assert.Equal(new TextColor(ConsoleColor.Blue), changed.Background.Value);
            Assert.True(changed.Bold);
        }

        [Fact]
        public void WithBackgroundAndWithBold_AreAlsoNonMutating()
        {
            var original = new TextStyle(new TextColor(ConsoleColor.Red));

            var background = original.WithBackground(new TextColor(ConsoleColor.White));
            var bold = original.WithBold(true);

            Assert.Equal(new TextColor(ConsoleColor.White), background.Background.Value);
            Assert.Equal(new TextColor(ConsoleColor.Red), background.Foreground.Value);
            Assert.True(bold.Bold);
            Assert.Equal(new TextColor(ConsoleColor.Red), bold.Foreground.Value);
            Assert.False(original.Background.HasValue);
            Assert.False(original.Bold);
        }

        [Fact]
        public void IsEmpty_IsFalseAsSoonAsAnythingIsAskedFor()
        {
            Assert.False(new TextStyle(new TextColor(ConsoleColor.Red)).IsEmpty);
            Assert.False(new TextStyle(background: new TextColor(ConsoleColor.Red)).IsEmpty);
            Assert.False(new TextStyle(bold: true).IsEmpty);
            Assert.True(new TextStyle(null, null, false).IsEmpty);
        }

        [Fact]
        public void Equality_ComparesAllThreeFieldsAndHashesConsistently()
        {
            var a = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(new Rgb24(1, 2, 3)), true);
            var b = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(new Rgb24(1, 2, 3)), true);
            var differentBold = a.WithBold(false);
            var differentBackground = a.WithBackground(null);

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, differentBold);
            Assert.NotEqual(a, differentBackground);
        }

        [Fact]
        public void Equality_DistinguishesANamedForegroundFromAnIdenticallyShadedExactOne()
        {
            // Widgets coalesce runs by comparing styles for equality, so a false "same" here would silently drop a
            // real color change out of the middle of a bar.
            var named = new TextStyle(new TextColor(ConsoleColor.Red));
            var exact = new TextStyle(new TextColor(new Rgb24(255, 0, 0)));

            Assert.NotEqual(named, exact);
            Assert.NotEqual(named.OpenSequence(AnsiColorModeEnum.TrueColor),
                exact.OpenSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void Equality_AgainstAnUnrelatedObject_IsFalse()
        {
            var style = new TextStyle(new TextColor(ConsoleColor.Red));

            Assert.False(style.Equals("red"));
            Assert.False(style.Equals(null));
        }

        [Fact]
        public void ToString_DescribesTheStyleForFailureMessages()
        {
            Assert.Equal("none", TextStyle.None.ToString());
            Assert.Equal("bold fg=Red bg=#FFFFFF",
                new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(new Rgb24(255, 255, 255)), true)
                    .ToString());
        }

        /// <summary>Counts occurrences of a character, so escape-shape assertions read as one number.</summary>
        private static int CountOf(string text, char value)
        {
            var count = 0;
            foreach (var c in text)
            {
                if (c == value)
                    count++;
            }

            return count;
        }
    }
}
