using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Rows built out of styled runs, and drawn by the column.
    ///     <para>
    ///         The colour mode is pinned rather than detected, for the reason every other widget test here pins it:
    ///         <c>DetectColorMode</c> is process-cached and these run in the parallel collection, so a test that
    ///         reached for the environment would race the whole assembly.
    ///     </para>
    /// </summary>
    public class TextRowTests
    {
        private static TextRow Row()
        {
            return new TextRow {ColorMode = AnsiColorModeEnum.Palette256};
        }

        [Fact]
        public void ARowNobodyColouredIsExactlyThePlainTextItWasBuiltFrom()
        {
            // The invariant the whole library holds to: no escape, not even a reset, when nothing asked for one.
            var row = Row().Append("abc").Append("def");

            Assert.Equal("abcdef", row.Render());
            Assert.Equal(6, row.Width);
        }

        [Fact]
        public void ItDrawsARangeOfColumns()
        {
            var row = Row().Append("abc").Append("defgh");

            Assert.Equal("cde", row.Render(2, 3));
            Assert.Equal("abcdefgh", row.Render(0, 8));
        }

        [Fact]
        public void ASliceCutsThroughTheMiddleOfARunRatherThanRoundingToIt()
        {
            var row = Row().Append("aaaa", ConsoleColor.Red).Append("bbbb", ConsoleColor.Blue);

            var slice = row.Render(2, 4);

            // Two of each, and each half still carries its own colour.
            Assert.Equal("aabb", AnsiText.StripEscapes(slice));
            Assert.Contains(new TextStyle(ConsoleColor.Red).OpenSequence(AnsiColorModeEnum.Palette256), slice,
                StringComparison.Ordinal);
            Assert.Contains(new TextStyle(ConsoleColor.Blue).OpenSequence(AnsiColorModeEnum.Palette256), slice,
                StringComparison.Ordinal);
        }

        [Fact]
        public void ASliceIsAlwaysAsWideAsItLooks()
        {
            // The property an overlay depends on: what came back occupies exactly the columns that were asked for,
            // whatever colour happens to be in the way.
            var row = Row()
                .Append("ab", ConsoleColor.Red)
                .Append("cd")
                .Append("ef", ConsoleColor.Blue);

            for (var from = 0; from < row.Width; from++)
            {
                for (var count = 1; from + count <= row.Width; count++)
                    Assert.Equal(count, AnsiText.VisibleLength(row.Render(from, count)));
            }
        }

        [Fact]
        public void AnOverlaySplicedIntoARowLeavesItTheSameWidth()
        {
            // What this type is for, written out: draw the row up to a column, draw something else, draw the rest.
            var row = Row().Append("0123456789", ConsoleColor.Gray);

            const string panel = "[ok]";
            var spliced = row.Render(0, 3) + panel + row.Render(3 + panel.Length, row.Width - 3 - panel.Length);

            Assert.Equal(row.Width, AnsiText.VisibleLength(spliced));
            Assert.Equal("012[ok]789", AnsiText.StripEscapes(spliced));
        }

        [Fact]
        public void AskingForColumnsTheRowDoesNotHaveGivesOnlyWhatItHas()
        {
            var row = Row().Append("abc");

            Assert.Equal("abc", row.Render(0, 99));
            Assert.Equal("c", row.Render(2, 99));
            Assert.Equal(string.Empty, row.Render(9, 4));
            Assert.Equal(string.Empty, row.Render(0, 0));
            Assert.Equal(string.Empty, row.Render(0, -2));
        }

        [Fact]
        public void PaddingFillsToTheWidthAndThenStops()
        {
            var row = Row().Append("ab").PadTo(5);

            Assert.Equal("ab   ", row.Render());

            row.PadTo(3);
            Assert.Equal(5, row.Width);
        }

        [Fact]
        public void EmptyAppendsAddNothingAtAll()
        {
            var row = Row().Append((string) null, ConsoleColor.Red).Append(string.Empty, ConsoleColor.Red)
                .Append('x', 0, ConsoleColor.Red).Append("a");

            // Not merely zero width: an empty run that still resolved its style would emit a stray escape pair.
            Assert.Equal("a", row.Render());
            Assert.Equal(1, row.Width);
        }

        [Fact]
        public void NeighbouringRunsThatResolveTheSameAreDrawnAsOne()
        {
            var row = Row().Append("ab", ConsoleColor.Red).Append("cd", ConsoleColor.Red);

            var open = new TextStyle(ConsoleColor.Red).OpenSequence(AnsiColorModeEnum.Palette256);

            // One opening sequence for the pair, not one each. Counted absolutely rather than "fewer than before",
            // which is the same lesson the other per-cell widgets here learned.
            Assert.Equal(open + "abcd" + TextStyle.ResetSequence, row.Render());
        }

        [Fact]
        public void NeighbouringRunsThatResolveDifferentlyAreNotMerged()
        {
            var row = Row().Append("ab", ConsoleColor.Red).Append("cd", ConsoleColor.Blue);
            var rendered = row.Render();

            Assert.Equal("abcd", AnsiText.StripEscapes(rendered));
            Assert.Contains(new TextStyle(ConsoleColor.Blue).OpenSequence(AnsiColorModeEnum.Palette256), rendered,
                StringComparison.Ordinal);
        }

        [Fact]
        public void TwoColoursTheTerminalCannotTellApartAreDrawnAsOneRun()
        {
            // Grayscale has 26 sequences to choose from, so a great many distinct colours arrive at the same one.
            // Comparing the styles instead of their escapes would spend a reset and a re-open between two cells
            // that come out identical, which on a screen redrawn every frame is measurable.
            var row = new TextRow {ColorMode = AnsiColorModeEnum.Grayscale};

            row.Append("ab", new Rgb24(120, 120, 120)).Append("cd", new Rgb24(121, 121, 121));

            var rendered = row.Render();
            var open = new TextStyle(new Rgb24(120, 120, 120)).OpenSequence(AnsiColorModeEnum.Grayscale);

            Assert.Equal(open + "abcd" + TextStyle.ResetSequence, rendered);
        }

        [Fact]
        public void AtNoColourNothingIsEmittedAtAll()
        {
            var row = new TextRow {ColorMode = AnsiColorModeEnum.None};

            row.Append("ab", ConsoleColor.Red).Append("cd", new TextStyle(ConsoleColor.White, ConsoleColor.Blue,
                true, true));

            // NO_COLOR asked for no escapes, not for a subset of them.
            Assert.Equal("abcd", row.Render());
        }
    }
}
