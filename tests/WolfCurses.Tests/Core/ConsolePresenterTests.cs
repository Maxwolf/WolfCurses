using System;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     Exercises the pure string-composition core of <see cref="ConsolePresenter" /> — the part that decides which
    ///     rows to rewrite and with which escape sequences — without touching a real console.
    /// </summary>
    public class ConsolePresenterTests
    {
        private const char Esc = (char) 27;
        private const char Bel = (char) 7;

        private static readonly string _syncBegin = Esc + "[?2026h";
        private static readonly string _syncEnd = Esc + "[?2026l";
        private static readonly string _wrapOff = Esc + "[?7l";
        private static readonly string _wrapOn = Esc + "[?7h";
        private static readonly string _sgrReset = Esc + "[0m";
        private static readonly string _resetEraseLine = Esc + "[0m" + Esc + "[K";
        private static readonly string _resetEraseBelow = Esc + "[0m" + Esc + "[J";

        [Fact]
        public void BuildAnsiUpdate_FullRedraw_WritesEveryRowInPlace()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA", "BBB"}, null, 80, 25);

            // StringComparison.Ordinal on every string needle in this file, on purpose: the default culture-sensitive
            // search uses ICU collation, under which ESC is a zero-weight "ignorable" character. It is silently
            // dropped from the needle, so "ESC[K" really searches for the literal text "[K" — an assertion that can
            // neither notice a missing escape nor tell a real sequence from bracket text. Do not "tidy" these away.
            Assert.Contains($"{Esc}[1;1HAAA{_resetEraseLine}", update, StringComparison.Ordinal);
            Assert.Contains($"{Esc}[2;1HBBB{_resetEraseLine}", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_FullRedraw_ErasesEverythingBelowManagedRows()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA", "BBB"}, null, 80, 25);

            Assert.Contains($"{Esc}[3;1H{_resetEraseBelow}", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_FullRedraw_OneRowConsole_DoesNotEraseBelow()
        {
            // On a one-row console "below" would clamp back onto the row just drawn and erase it.
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA"}, null, 80, 1);

            Assert.DoesNotContain(Esc + "[J", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_Diff_RewritesOnlyChangedRows()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {"AAA", "XXX"},
                new[] {"AAA", "BBB"}, 80, 25);

            Assert.Contains($"{Esc}[2;1HXXX{_resetEraseLine}", update, StringComparison.Ordinal);
            Assert.DoesNotContain(Esc + "[1;1H", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_Diff_EmitsExactlyTheExpectedPayload()
        {
            // Pins the complete protocol for a one-row change: synchronized-update begin, auto-wrap off, the changed
            // row rewritten in place and its tail erased, the cursor parked after the last content ("XXX", so row 2
            // column 4), auto-wrap back on, synchronized-update end — and nothing else.
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {"AAA", "XXX"},
                new[] {"AAA", "BBB"}, 80, 25);

            Assert.Equal(
                $"{_syncBegin}{_wrapOff}{Esc}[2;1HXXX{_resetEraseLine}{Esc}[2;4H{_wrapOn}{_syncEnd}",
                update);
        }

        [Fact]
        public void BuildAnsiUpdate_Diff_DoesNotEraseBelow()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {"AAA", "XXX"},
                new[] {"AAA", "BBB"}, 80, 25);

            Assert.DoesNotContain(Esc + "[J", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_NoChanges_ReturnsEmptyString()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {"AAA", "BBB"},
                new[] {"AAA", "BBB"}, 80, 25);

            Assert.Equal(string.Empty, update);
        }

        [Fact]
        public void BuildAnsiUpdate_RowThatBecameEmpty_IsErased()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {"AAA", ""},
                new[] {"AAA", "BBB"}, 80, 25);

            // The row is "written" as nothing and then erased to the end of the line, wiping the old content.
            Assert.Contains($"{Esc}[2;1H{_resetEraseLine}", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_RowFillingTheWholeLine_IsNotFollowedByErase()
        {
            // With auto-wrap off the cursor ends such a row ON the last column, and erase-to-end-of-line includes
            // the cursor cell — an erase here would blank the just-written rightmost character (e.g. the right edge
            // of a console-wide image). The color reset must still be emitted.
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"ABC"}, null, 3, 25);

            Assert.Contains($"{Esc}[1;1HABC{_sgrReset}", update, StringComparison.Ordinal);
            Assert.DoesNotContain(Esc + "[K", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_RowWiderThanTheConsole_IsNotFollowedByErase()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"ABCDE"}, null, 3, 25);

            Assert.DoesNotContain(Esc + "[K", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_FullWidthDecision_IgnoresEscapeSequences()
        {
            // Three visible cells on a three-column console: full-width, so no erase — no matter how many
            // zero-width color escapes surround the glyphs.
            var colored = $"{Esc}[38;2;1;2;3mAB{Esc}[48;2;4;5;6mC{Esc}[0m";
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {colored}, null, 3, 25);

            Assert.DoesNotContain(Esc + "[K", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_WrapsUpdateInSynchronizedOutputAndWrapGuards()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA"}, null, 80, 25);

            Assert.StartsWith(_syncBegin + _wrapOff, update, StringComparison.Ordinal);
            Assert.EndsWith(_wrapOn + _syncEnd, update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_ParksCursorAtEndOfLastContentRow()
        {
            // Last non-empty row is "Hi" on row 2, so the cursor parks at row 2, column 3 — where the input
            // prompt would echo the next typed character.
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"Hello", "Hi", ""}, null, 80, 25);

            Assert.EndsWith($"{Esc}[2;3H{_wrapOn}{_syncEnd}", update, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAnsiUpdate_ParkColumnIgnoresEscapeSequences()
        {
            var colored = $"{Esc}[38;2;10;20;30mAB{Esc}[0m";
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {colored}, null, 80, 25);

            // Two visible characters, so the park column is 3 no matter how long the escape codes are.
            Assert.EndsWith($"{Esc}[1;3H{_wrapOn}{_syncEnd}", update, StringComparison.Ordinal);
        }

        [Fact]
        public void SplitLines_UnderstandsBothLfAndCrLf()
        {
            var lines = ConsolePresenter.SplitLines("AAA\r\nBBB\nCCC", 4);

            Assert.Equal(new[] {"AAA", "BBB", "CCC", ""}, lines);
        }

        [Fact]
        public void SplitLines_DropsRowsThatWouldNotFitOnScreen()
        {
            var lines = ConsolePresenter.SplitLines("AAA\nBBB\nCCC", 2);

            Assert.Equal(new[] {"AAA", "BBB"}, lines);
        }

        [Fact]
        public void SplitLines_EmptyContent_YieldsAllEmptyRows()
        {
            var lines = ConsolePresenter.SplitLines(string.Empty, 3);

            Assert.Equal(new[] {"", "", ""}, lines);
        }

        [Fact]
        public void ParkPosition_EmptyFrame_ParksAtHome()
        {
            Assert.Equal((1, 1), ConsolePresenter.ParkPosition(new[] {"", ""}));
        }

        [Fact]
        public void ParkPosition_SkipsTrailingEmptyRows()
        {
            Assert.Equal((2, 3), ConsolePresenter.ParkPosition(new[] {"AAAA", "BB", "", ""}));
        }

        [Fact]
        public void VisibleLength_PlainText_IsStringLength()
        {
            Assert.Equal(5, ConsolePresenter.VisibleLength("Hello"));
        }

        [Fact]
        public void VisibleLength_CsiSequencesAreZeroWidth()
        {
            // A truecolor half-block cell exactly as AnsiImageRenderer emits it: one visible glyph.
            Assert.Equal(1, ConsolePresenter.VisibleLength($"{Esc}[38;2;1;2;3m{Esc}[48;2;4;5;6m▀{Esc}[0m"));
        }

        [Fact]
        public void VisibleLength_OscSequenceTerminatedByBel_IsZeroWidth()
        {
            // An OSC 8 hyperlink wrapping two visible characters.
            var line = $"{Esc}]8;;https://example.com{Bel}AB{Esc}]8;;{Bel}";

            Assert.Equal(2, ConsolePresenter.VisibleLength(line));
        }

        [Fact]
        public void VisibleLength_OscSequenceTerminatedByStringTerminator_IsZeroWidth()
        {
            var line = $"{Esc}]0;window title{Esc}\\AB";

            Assert.Equal(2, ConsolePresenter.VisibleLength(line));
        }

        [Fact]
        public void VisibleLength_CharsetDesignationEscape_IsZeroWidth()
        {
            // "ESC ( B" selects the ASCII charset: three bytes, zero cells.
            Assert.Equal(2, ConsolePresenter.VisibleLength($"{Esc}(BAB"));
        }

        [Fact]
        public void VisibleLength_BareEscapeIsZeroWidth()
        {
            // ESC followed by a final byte is a two-byte sequence ("ESC 7" saves the cursor): zero cells, and the
            // characters after it count normally.
            Assert.Equal(2, ConsolePresenter.VisibleLength($"{Esc}7AB"));
        }

        [Fact]
        public void VisibleLength_UnterminatedSequenceAtEndOfLine_DoesNotThrow()
        {
            Assert.Equal(0, ConsolePresenter.VisibleLength($"{Esc}[38;2;1"));
            Assert.Equal(0, ConsolePresenter.VisibleLength($"{Esc}]8;;https://example"));
            Assert.Equal(0, ConsolePresenter.VisibleLength($"{Esc}"));
        }

        [Fact]
        public void StripEscapes_RemovesSgrLeavingOnlyTheVisibleText()
        {
            // A colored widget cell as it would arrive at the legacy (VT-incapable) presenter: an SGR open, the
            // glyphs, and the reset. On such a console the escapes cannot render and must not be printed.
            var colored = $"{Esc}[1;38;2;255;0;0mHELLO{Esc}[0m";

            Assert.Equal("HELLO", ConsolePresenter.StripEscapes(colored));
        }

        [Fact]
        public void StripEscapes_LeavesPlainTextUntouched()
        {
            Assert.Equal("no escapes here", ConsolePresenter.StripEscapes("no escapes here"));
            Assert.Equal(string.Empty, ConsolePresenter.StripEscapes(string.Empty));
        }

        [Theory]
        [InlineData("plain text")]
        [InlineData("bar")] // pure text
        [InlineData("")]
        public void StripEscapes_OfPlainText_HasSameLengthAsInput(string line)
        {
            Assert.Equal(line.Length, ConsolePresenter.StripEscapes(line).Length);
        }

        [Fact]
        public void StripEscapes_LengthAlwaysEqualsVisibleLength()
        {
            // The whole reason StripEscapes and VisibleLength share SkipEscape: the stripped string's length must be
            // the visible width, so PresentLegacy's post-strip line.Length arithmetic is correct for every escape
            // shape. If a future edit diverges the two walks, this pins the failure.
            var cases = new[]
            {
                "plain",
                $"{Esc}[31mred{Esc}[0m",
                $"{Esc}[1;38;5;120mmid{Esc}[0mtail",
                $"before{Esc}[38;2;0;128;255mafter",     // unterminated-run then reset-less
                $"{Esc}]8;;https://example.com{Esc}\\link{Esc}]8;;{Esc}\\", // OSC 8 hyperlink around "link"
                $"{Esc}(Bcharset",                        // short charset-designation escape
                $"{Esc}7cursor",                          // two-byte ESC sequence
                $"trailing{Esc}",                         // bare trailing ESC
                $"{Esc}[38;2;1",                          // unterminated CSI at end
                $"{Esc}[31m",                             // only an escape, no visible text
            };

            foreach (var line in cases)
                Assert.Equal(ConsolePresenter.VisibleLength(line), ConsolePresenter.StripEscapes(line).Length);
        }
    }
}
