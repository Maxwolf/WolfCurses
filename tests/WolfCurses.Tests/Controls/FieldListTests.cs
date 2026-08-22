using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     A record drawn as labelled fields.
    ///     <para>
    ///         The test that earns the control its place is the one asserting that a click on any row of a
    ///         multi-line field picks that field, with the rows read off <c>Render</c>'s own output rather than
    ///         counted out again here. A field is not a row, so the naive hit test is wrong for everything under
    ///         the first field that wraps, and it is wrong invisibly.
    ///     </para>
    /// </summary>
    public class FieldListTests
    {
        /// <summary>A card with a note long enough to need every row it was given.</summary>
        private static FieldList Card() => new(
            new FieldListEntry("Name", "Maxwolf"),
            new FieldListEntry("Phone", "555-0100"),
            new FieldListEntry("Notes", "Answers to a whistle. Do not park nearby during the moult.", 3))
        {
            Width = 30,
            Row = 4,
            Column = 2
        };

        /// <summary>A rendered row with the escapes taken out.</summary>
        private static string Plain(FieldList list, int row) => AnsiText.StripEscapes(list.Render()[row]);

        [Fact]
        public void ItReservesTheRowsAFieldAskedForWhetherItNeedsThemOrNot()
        {
            var list = Card();

            // One, one and three, so nothing below a field moves as its value is typed into.
            Assert.Equal(5, list.Height);
            Assert.Equal(5, list.Render().Count);

            list.Entries[2].Value = string.Empty;
            Assert.Equal(5, list.Render().Count);
        }

        [Fact]
        public void EveryRowOfAFieldAnswersWithThatField()
        {
            var list = Card();

            Assert.Equal(0, list.FieldAt(4, 2));
            Assert.Equal(1, list.FieldAt(5, 2));

            // The three rows of the note, all of them the note. The naive row-minus-origin says 2, 3 and 4.
            Assert.Equal(2, list.FieldAt(6, 2));
            Assert.Equal(2, list.FieldAt(7, 2));
            Assert.Equal(2, list.FieldAt(8, 2));
        }

        [Fact]
        public void WhereAFieldIsDrawnIsWhereAClickOnItLands()
        {
            var list = Card();
            var rows = list.Render();

            // Both halves read off the control: the row it says the field is on must draw that field's label, and
            // a click there must come back with the same field.
            for (var i = 0; i < list.Entries.Count; i++)
            {
                var row = list.RowOf(i);

                Assert.StartsWith(list.Entries[i].Label,
                    AnsiText.StripEscapes(rows[row - list.Row]), StringComparison.Ordinal);

                Assert.Equal(i, list.FieldAt(row, list.Column));
            }
        }

        [Fact]
        public void OffTheListIsMinusOneInEveryDirection()
        {
            var list = Card();

            Assert.Equal(-1, list.FieldAt(3, 2));
            Assert.Equal(-1, list.FieldAt(9, 2));
            Assert.Equal(-1, list.FieldAt(4, 1));
            Assert.Equal(-1, list.FieldAt(4, 2 + list.Width));
            Assert.Equal(-1, list.RowOf(-1));
            Assert.Equal(-1, list.RowOf(3));
        }

        [Fact]
        public void EveryRowIsExactlyAsWideAsTheListSaysItIs()
        {
            var list = Card();

            foreach (var row in list.Render())
                Assert.Equal(30, AnsiText.VisibleLength(row));

            // Including when there is nothing in the field at all.
            list.Entries[0].Value = string.Empty;
            Assert.Equal(30, AnsiText.VisibleLength(list.Render()[0]));
        }

        [Fact]
        public void TheLabelColumnIsMeasuredOnceForTheWholeList()
        {
            var list = new FieldList(
                new FieldListEntry("A", "one"),
                new FieldListEntry("Something long", "two")) {Width = 40};

            Assert.Equal("Something long".Length, list.LabelWidth);

            // Both values therefore start in the same column, which is the whole reason it is measured per list.
            var rows = list.Render();
            Assert.Equal(
                AnsiText.StripEscapes(rows[0]).IndexOf("one", StringComparison.Ordinal),
                AnsiText.StripEscapes(rows[1]).IndexOf("two", StringComparison.Ordinal));
        }

        [Fact]
        public void AMinimumLabelWidthPinsTheColumnSoItDoesNotMoveWhenTheFieldsDo()
        {
            var list = new FieldList(new FieldListEntry("A", "one")) {Width = 40, MinimumLabelWidth = 12};

            Assert.Equal(12, list.LabelWidth);
            Assert.Equal(40 - 12 - 1, list.ValueWidth);
        }

        [Fact]
        public void TheLabelNeverEatsSoMuchThatNoValueFits()
        {
            var list = new FieldList(new FieldListEntry("A label far wider than the list", "x")) {Width = 10};

            Assert.Equal(1, list.ValueWidth);
            Assert.Equal(10, AnsiText.VisibleLength(list.Render()[0]));
        }

        [Fact]
        public void AValueWrapsIntoTheRowsItWasGiven()
        {
            var list = Card();

            var first = Plain(list, 2);
            var second = Plain(list, 3);

            Assert.Contains("Answers", first, StringComparison.Ordinal);
            Assert.DoesNotContain("Answers", second, StringComparison.Ordinal);

            // The continuation lines leave the label column blank rather than repeating the label.
            Assert.Equal(new string(' ', list.LabelWidth), second.Substring(0, list.LabelWidth));
        }

        [Fact]
        public void LineBreaksAlreadyInAValueAreKept()
        {
            // A note holding several lines is exactly the case a card index has, and the case a CSV field can carry.
            var list = new FieldList(new FieldListEntry("Notes", "first\nsecond", 3)) {Width = 30};

            Assert.Contains("first", Plain(list, 0), StringComparison.Ordinal);
            Assert.Contains("second", Plain(list, 1), StringComparison.Ordinal);
        }

        [Fact]
        public void AValueTooLongForItsRowsSaysSoRatherThanEndingMidWord()
        {
            var list = new FieldList(
                new FieldListEntry("Notes", "one two three four five six seven eight nine ten", 1)) {Width = 20};

            Assert.EndsWith("…", Plain(list, 0), StringComparison.Ordinal);

            // And a caller that wants nothing said gets nothing said.
            list.Overflow = '\0';
            Assert.DoesNotContain("…", Plain(list, 0), StringComparison.Ordinal);
        }

        [Fact]
        public void AValueThatFitsExactlyIsNotMarked()
        {
            var list = new FieldList(new FieldListEntry("N", "abc")) {Width = 10};

            Assert.DoesNotContain("…", Plain(list, 0), StringComparison.Ordinal);
        }

        [Fact]
        public void NothingIsPickedOutUntilSomethingIsChosen()
        {
            var list = Card();

            Assert.Equal(-1, list.Selected);
            Assert.Null(list.SelectedEntry);

            list.Selected = 1;
            Assert.Equal("Phone", list.SelectedEntry.Label);
        }

        [Fact]
        public void ASelectedFieldIsPaintedLabelAndValueAlike()
        {
            var list = Card();
            list.ColorMode = AnsiColorModeEnum.Palette256;
            list.SelectedStyle = new TextStyle(ConsoleColor.Black, ConsoleColor.Gray);
            list.Selected = 1;

            var row = list.Render()[1];
            var open = list.SelectedStyle.OpenSequence(AnsiColorModeEnum.Palette256);

            // One run for the whole row: half a highlighted row reads as a drawing fault rather than a selection.
            Assert.StartsWith(open, row, StringComparison.Ordinal);
            Assert.Equal(1, CountOf(row, open));
        }

        [Fact]
        public void AListNobodyColouredEmitsNothingAtAll()
        {
            var list = Card();

            foreach (var row in list.Render())
                Assert.DoesNotContain('\x1b', row);
        }

        [Fact]
        public void AnEmptyListDrawsNothingAndPointsAtNothing()
        {
            var list = new FieldList();

            Assert.Equal(0, list.Height);
            Assert.Empty(list.Render());
            Assert.Equal(-1, list.FieldAt(0, 0));
        }

        /// <summary>How many times one string occurs in another, without overlaps.</summary>
        private static int CountOf(string haystack, string needle)
        {
            var count = 0;

            for (var at = haystack.IndexOf(needle, StringComparison.Ordinal);
                 at >= 0;
                 at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
                count++;

            return count;
        }
    }
}
