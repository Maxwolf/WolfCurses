using System;
using System.IO;
using WolfCurses.Apps.Spreadsheet;
using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     Reading and writing sheets, and the sample sheet the spreadsheet opens on.
    ///     <para>
    ///         The sample is checked by <b>working its own arithmetic out independently</b> rather than by quoting
    ///         the numbers in it. A formula whose range is off by a row still produces a total, and a test that only
    ///         asserted a total appeared would sleep through it; recomputing the answer from the cells beside it
    ///         will not.
    ///     </para>
    /// </summary>
    public class SheetLibraryTests
    {
        /// <summary>The sample sheet, loaded from where the build copies it.</summary>
        private static Sheet Sample()
        {
            var sheet = SheetLibrary.TryLoad(SheetLibrary.DefaultSheetPath, out var error);

            Assert.True(sheet != null,
                "the sample sheet did not load from " + SheetLibrary.DefaultSheetPath + ": " + error);

            return sheet;
        }

        /// <summary>Finds the row whose first cell says this, so nothing here has to name a row number.</summary>
        private static int RowSaying(Sheet sheet, string text)
        {
            for (var row = 0; row < sheet.UsedRowCount; row++)
            {
                if (string.Equals(sheet.GetText(row, 0), text, StringComparison.Ordinal))
                    return row;
            }

            Assert.Fail("no row in the sample begins with \"" + text + "\"");
            return -1;
        }

        [Fact]
        public void ARowWithOnlyItsFirstCellFilledBecomesABanner()
        {
            var sheet = SheetLibrary.Parse("A heading all by itself\nName,Value\nAlpha,1\n");

            // The rule is inferred rather than stated by the file, because a comma separated file has nowhere to
            // say that a cell is merged. Nothing else in a table has this shape.
            Assert.Single(sheet.Merges);
            Assert.Equal(0, sheet.Merges[0].Row);
            Assert.Equal(2, sheet.Merges[0].ColumnCount);
        }

        [Fact]
        public void AnOrdinaryRowIsNotMerged()
        {
            var sheet = SheetLibrary.Parse("Name,Value\nAlpha,1\n");

            Assert.Empty(sheet.Merges);
        }

        [Fact]
        public void ABlankRowIsNotABanner()
        {
            var sheet = SheetLibrary.Parse("Name,Value\n\nAlpha,1\n");

            Assert.Empty(sheet.Merges);
        }

        [Fact]
        public void ASheetOfOneColumnHasNothingToMergeAcross()
        {
            var sheet = SheetLibrary.Parse("Alpha\nBravo\n");

            Assert.Empty(sheet.Merges);
        }

        [Fact]
        public void ALoadedSheetIsNotModifiedUntilSomethingChangesIt()
        {
            var sheet = SheetLibrary.Parse("Name,Value\nAlpha,1\n");

            // Merging happens during the load, so this is really a test that the load says so afterwards.
            Assert.False(sheet.IsModified);

            sheet.SetText(0, 0, "Changed");
            Assert.True(sheet.IsModified);
        }

        [Fact]
        public void TheLineEndingIsRememberedRatherThanNormalized()
        {
            Assert.Equal("\r\n", SheetLibrary.Parse("a,b\r\nc,d\r\n").NewLine);
            Assert.Equal("\n", SheetLibrary.Parse("a,b\nc,d\n").NewLine);
        }

        [Fact]
        public void WhatIsWrittenReadsBackAsTheSameCells()
        {
            var sheet = new Sheet();

            sheet.SetText(0, 0, "Item");
            sheet.SetText(0, 1, "Cost");
            sheet.SetText(1, 0, "Wolf, Max said \"fine\"");
            sheet.SetText(1, 1, "=1+1");

            var written = DelimitedText.Write(sheet.Rows(), ',', "\n");
            var read = SheetLibrary.Parse(written);

            // The formula and not its answer, which is the whole point of storing what was typed.
            Assert.Equal("=1+1", read.GetText(1, 1));
            Assert.Equal("Wolf, Max said \"fine\"", read.GetText(1, 0));
            Assert.Equal(2, read.UsedRowCount);
        }

        [Fact]
        public void TheSampleSheetLoadsAndHasSomethingInIt()
        {
            var sheet = Sample();

            Assert.True(sheet.UsedRowCount > 40,
                "the sample has only " + sheet.UsedRowCount + " rows, which is not enough to scroll through");

            Assert.Equal(6, sheet.UsedColumnCount);
        }

        [Fact]
        public void TheSamplesInstructionsAreDrawnAsBanners()
        {
            var sheet = Sample();
            var title = RowSaying(sheet, "Maxwolf Financial Situation 2026");

            Assert.NotNull(sheet.MergeAt(title, 0));

            // And the table's own heading row is not one, or the whole table would be drawn as one wide cell.
            Assert.Null(sheet.MergeAt(RowSaying(sheet, "Month"), 0));
        }

        [Fact]
        public void EveryInstructionFitsAnEightyColumnScreen()
        {
            // The suite's floor is 80x24. A banner spans six twelve-wide columns less one for the gap, so anything
            // longer than seventy-one characters is silently cut off on the screen it was written for.
            var sheet = Sample();

            foreach (var merge in sheet.Merges)
            {
                var text = sheet.GetText(merge.Anchor);

                Assert.True(text.Length <= 71,
                    "this banner is " + text.Length + " characters and will be cut off: " + text);
            }
        }

        [Fact]
        public void NotOneCellInTheSampleIsAMistake()
        {
            var sheet = Sample();

            for (var row = 0; row < sheet.UsedRowCount; row++)
            {
                for (var column = 0; column < sheet.UsedColumnCount; column++)
                {
                    var value = sheet.GetValue(row, column);

                    Assert.False(value.IsError,
                        new CellAddress(row, column) + " reads " + value.Display() + " from \"" +
                        sheet.GetText(row, column) + "\"");
                }
            }
        }

        [Fact]
        public void EveryMonthsNetIsWhatIsLeftAfterTheSpending()
        {
            var sheet = Sample();
            var header = RowSaying(sheet, "Month");
            var total = RowSaying(sheet, "Total");

            Assert.True(total - header == 13, "expected twelve months between the heading and the total");

            for (var row = header + 1; row < total; row++)
            {
                var expected = sheet.GetValue(row, 1).Number - sheet.GetValue(row, 2).Number -
                               sheet.GetValue(row, 3).Number - sheet.GetValue(row, 4).Number;

                // Worked out here from the cells beside it rather than read out of the file, so a formula pointing
                // one row off fails this even though it still produces a number.
                Assert.Equal(expected, sheet.GetValue(row, 5).Number, 6);
            }
        }

        [Fact]
        public void TheTotalRowReallyTotalsTheColumnAboveIt()
        {
            var sheet = Sample();
            var header = RowSaying(sheet, "Month");
            var total = RowSaying(sheet, "Total");

            for (var column = 1; column <= 5; column++)
            {
                var expected = 0d;

                for (var row = header + 1; row < total; row++)
                    expected += sheet.GetValue(row, column).Number;

                Assert.Equal(expected, sheet.GetValue(total, column).Number, 6);
            }
        }

        [Fact]
        public void TheSummaryAtTheBottomAgreesWithTheTableAtTheTop()
        {
            var sheet = Sample();
            var total = RowSaying(sheet, "Total");

            // The last row of the sample points back at the Net total, which is the reference most likely to be
            // left behind when rows are inserted above it.
            Assert.Equal(sheet.GetValue(total, 5).Number,
                sheet.GetValue(RowSaying(sheet, "Kept after all that"), 1).Number, 6);
        }

        [Fact]
        public void TheQuotedFieldsInTheSampleSurviveBeingRead()
        {
            var sheet = Sample();
            var found = false;

            for (var row = 0; row < sheet.UsedRowCount && !found; row++)
            {
                for (var column = 0; column < sheet.UsedColumnCount; column++)
                {
                    // A field carrying both a quote and a comma, which is the shape that separates a real reader
                    // from a call to Split.
                    if (!sheet.GetText(row, column).Contains("27\" monitor, the fourth", StringComparison.Ordinal))
                        continue;

                    found = true;
                    break;
                }
            }

            Assert.True(found, "the sample's awkwardly quoted item did not survive being read");
        }

        [Fact]
        public void TheSampleIsWrittenBackOutAsTheSameCells()
        {
            var sheet = Sample();
            var reread = SheetLibrary.Parse(DelimitedText.Write(sheet.Rows(), ',', sheet.NewLine));

            Assert.Equal(sheet.UsedRowCount, reread.UsedRowCount);

            for (var row = 0; row < sheet.UsedRowCount; row++)
            {
                for (var column = 0; column < sheet.UsedColumnCount; column++)
                    Assert.Equal(sheet.GetText(row, column), reread.GetText(row, column));
            }
        }

        [Fact]
        public void AFileThatCannotBeReadSaysSoRatherThanThrowing()
        {
            Assert.Null(SheetLibrary.TryLoad(Path.Combine(SheetLibrary.Folder, "no-such-file.csv"), out var error));
            Assert.False(string.IsNullOrEmpty(error));

            Assert.Null(SheetLibrary.TryLoad(null, out _));
        }

        [Fact]
        public void TheOpenDialogStartsSomewhereThatExists()
        {
            Assert.True(Directory.Exists(SheetLibrary.BrowseFolder));
            Assert.Contains(".csv", SheetLibrary.Extensions);
        }

        [Fact]
        public void ChartingAColumnTakesItsLabelsFromTheColumnBeside()
        {
            var sheet = Sample();
            var header = RowSaying(sheet, "Month");
            var months = new CellRange(new CellAddress(header + 1, 1), new CellAddress(header + 12, 1));

            var chart = SheetChart.Render(sheet, months, SheetChartKindEnum.Bars, 78, 16);

            // The month names are in the column to the left of the figures, which is how anybody would have typed
            // it and is therefore where the labels come from.
            Assert.Contains("Jan", chart, StringComparison.Ordinal);
            Assert.Contains("Dec", chart, StringComparison.Ordinal);
        }

        [Fact]
        public void ChartingSomethingWithNoNumbersInItSaysSo()
        {
            var sheet = Sample();
            var title = new CellRange(new CellAddress(RowSaying(sheet, "Month"), 0));

            var chart = SheetChart.Render(sheet, title, SheetChartKindEnum.Bars, 78, 16);

            Assert.Contains("Nothing to chart", chart, StringComparison.Ordinal);
        }

        [Fact]
        public void ARectangleIsChartedByItsFirstColumnAndSaysThatItIs()
        {
            var range = new CellRange(new CellAddress(1, 1), new CellAddress(4, 3));

            Assert.Contains("first column only", SheetChart.Caption(range), StringComparison.Ordinal);
            Assert.DoesNotContain("first column only", SheetChart.Caption(new CellRange(new CellAddress(1, 1),
                new CellAddress(4, 1))), StringComparison.Ordinal);
        }
    }
}
