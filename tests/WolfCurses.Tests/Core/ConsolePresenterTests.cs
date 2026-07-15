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

        private static readonly string SyncBegin = Esc + "[?2026h";
        private static readonly string SyncEnd = Esc + "[?2026l";
        private static readonly string WrapOff = Esc + "[?7l";
        private static readonly string WrapOn = Esc + "[?7h";
        private static readonly string SgrReset = Esc + "[0m";
        private static readonly string ResetEraseLine = Esc + "[0m" + Esc + "[K";
        private static readonly string ResetEraseBelow = Esc + "[0m" + Esc + "[J";

        [Fact]
        public void BuildAnsiUpdate_FullRedraw_WritesEveryRowInPlace()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA", "BBB"}, null, 80, 25);

            Assert.Contains($"{Esc}[1;1HAAA{ResetEraseLine}", update);
            Assert.Contains($"{Esc}[2;1HBBB{ResetEraseLine}", update);
        }

        [Fact]
        public void BuildAnsiUpdate_FullRedraw_ErasesEverythingBelowManagedRows()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA", "BBB"}, null, 80, 25);

            Assert.Contains($"{Esc}[3;1H{ResetEraseBelow}", update);
        }

        [Fact]
        public void BuildAnsiUpdate_FullRedraw_OneRowConsole_DoesNotEraseBelow()
        {
            // On a one-row console "below" would clamp back onto the row just drawn and erase it.
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA"}, null, 80, 1);

            Assert.DoesNotContain(Esc + "[J", update);
        }

        [Fact]
        public void BuildAnsiUpdate_Diff_RewritesOnlyChangedRows()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {"AAA", "XXX"},
                new[] {"AAA", "BBB"}, 80, 25);

            Assert.Contains($"{Esc}[2;1HXXX{ResetEraseLine}", update);
            Assert.DoesNotContain(Esc + "[1;1H", update);
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
                $"{SyncBegin}{WrapOff}{Esc}[2;1HXXX{ResetEraseLine}{Esc}[2;4H{WrapOn}{SyncEnd}",
                update);
        }

        [Fact]
        public void BuildAnsiUpdate_Diff_DoesNotEraseBelow()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {"AAA", "XXX"},
                new[] {"AAA", "BBB"}, 80, 25);

            Assert.DoesNotContain(Esc + "[J", update);
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
            Assert.Contains($"{Esc}[2;1H{ResetEraseLine}", update);
        }

        [Fact]
        public void BuildAnsiUpdate_RowFillingTheWholeLine_IsNotFollowedByErase()
        {
            // With auto-wrap off the cursor ends such a row ON the last column, and erase-to-end-of-line includes
            // the cursor cell — an erase here would blank the just-written rightmost character (e.g. the right edge
            // of a console-wide image). The color reset must still be emitted.
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"ABC"}, null, 3, 25);

            Assert.Contains($"{Esc}[1;1HABC{SgrReset}", update);
            Assert.DoesNotContain(Esc + "[K", update);
        }

        [Fact]
        public void BuildAnsiUpdate_RowWiderThanTheConsole_IsNotFollowedByErase()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"ABCDE"}, null, 3, 25);

            Assert.DoesNotContain(Esc + "[K", update);
        }

        [Fact]
        public void BuildAnsiUpdate_FullWidthDecision_IgnoresEscapeSequences()
        {
            // Three visible cells on a three-column console: full-width, so no erase — no matter how many
            // zero-width color escapes surround the glyphs.
            var colored = $"{Esc}[38;2;1;2;3mAB{Esc}[48;2;4;5;6mC{Esc}[0m";
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {colored}, null, 3, 25);

            Assert.DoesNotContain(Esc + "[K", update);
        }

        [Fact]
        public void BuildAnsiUpdate_WrapsUpdateInSynchronizedOutputAndWrapGuards()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"AAA"}, null, 80, 25);

            Assert.StartsWith(SyncBegin + WrapOff, update);
            Assert.EndsWith(WrapOn + SyncEnd, update);
        }

        [Fact]
        public void BuildAnsiUpdate_ParksCursorAtEndOfLastContentRow()
        {
            // Last non-empty row is "Hi" on row 2, so the cursor parks at row 2, column 3 — where the input
            // prompt would echo the next typed character.
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {"Hello", "Hi", ""}, null, 80, 25);

            Assert.EndsWith($"{Esc}[2;3H{WrapOn}{SyncEnd}", update);
        }

        [Fact]
        public void BuildAnsiUpdate_ParkColumnIgnoresEscapeSequences()
        {
            var colored = $"{Esc}[38;2;10;20;30mAB{Esc}[0m";
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {colored}, null, 80, 25);

            // Two visible characters, so the park column is 3 no matter how long the escape codes are.
            Assert.EndsWith($"{Esc}[1;3H{WrapOn}{SyncEnd}", update);
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
    }
}
