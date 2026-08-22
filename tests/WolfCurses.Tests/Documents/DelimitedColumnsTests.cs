using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     The header row of a delimited file, read by name.
    ///     <para>
    ///         The tests that earn this its place are the two ragged ones: a column the file has not got, and a row
    ///         that stops before reaching the column asked for. Those are the shapes a hand-edited file arrives in,
    ///         and the <c>row[3]</c> a caller writes instead gets the first wrong and throws on the second.
    ///     </para>
    /// </summary>
    public class DelimitedColumnsTests
    {
        private static DelimitedColumns Header(params string[] names) => new(names);

        [Fact]
        public void ItFindsAColumnByName()
        {
            var columns = Header("Name", "Phone", "Notes");

            Assert.Equal(3, columns.Count);
            Assert.Equal(0, columns.IndexOf("Name"));
            Assert.Equal(1, columns.IndexOf("Phone"));
            Assert.Equal(2, columns.IndexOf("Notes"));
        }

        [Fact]
        public void AColumnTheFileHasNotGotIsMinusOneRatherThanAThrow()
        {
            var columns = Header("Name", "Phone");

            Assert.Equal(-1, columns.IndexOf("Email"));
            Assert.False(columns.Has("Email"));
            Assert.Equal(-1, columns.IndexOf(null));
            Assert.Equal(-1, columns.IndexOf("   "));
        }

        [Fact]
        public void ReadingIsByNameSoTheColumnsMayBeInAnyOrder()
        {
            // The whole reason this type exists: the same reader against a file whose columns were moved.
            var written = Header("Name", "Phone", "Notes");
            var edited = Header("Notes", "Name", "Phone");

            var writtenRow = new[] {"Maxwolf", "555-0100", "Large"};
            var editedRow = new[] {"Large", "Maxwolf", "555-0100"};

            foreach (var name in new[] {"Name", "Phone", "Notes"})
                Assert.Equal(written.Value(writtenRow, name), edited.Value(editedRow, name));
        }

        [Fact]
        public void AMissingColumnReadsAsEmptyRatherThanFailingTheWholeRow()
        {
            var columns = Header("Name", "Phone");
            var row = new[] {"Maxwolf", "555-0100"};

            Assert.Equal(string.Empty, columns.Value(row, "Email"));
            Assert.Equal("Maxwolf", columns.Value(row, "Name"));
        }

        [Fact]
        public void ARowThatStopsShortReadsAsEmptyRatherThanThrowing()
        {
            var columns = Header("Name", "Phone", "Notes");

            // Two fields against a three-column header, which is what a hand-edited row looks like.
            var row = new[] {"Maxwolf", "555-0100"};

            Assert.Equal("Maxwolf", columns.Value(row, "Name"));
            Assert.Equal("555-0100", columns.Value(row, "Phone"));
            Assert.Equal(string.Empty, columns.Value(row, "Notes"));
        }

        [Fact]
        public void ARowWithMoreFieldsThanTheHeaderKeepsTheOnesTheHeaderNames()
        {
            var columns = Header("Name", "Phone");
            var row = new[] {"Maxwolf", "555-0100", "something nobody declared"};

            Assert.Equal("Maxwolf", columns.Value(row, "Name"));
            Assert.Equal("555-0100", columns.Value(row, "Phone"));
        }

        [Fact]
        public void NamesAreMatchedWithoutCaseAndWithTheEndsTrimmed()
        {
            // "Name, Phone" written with a space after the comma is the ordinary way a person types a header.
            var columns = Header(" Name ", "PHONE");

            Assert.Equal(0, columns.IndexOf("name"));
            Assert.Equal(1, columns.IndexOf(" phone"));
            Assert.Equal("Name", columns.Names[0]);
        }

        [Fact]
        public void TheDataIsNotTrimmedEvenThoughTheNamesAre()
        {
            // The asymmetry is the point: a name is a key and a value may legitimately begin with a space.
            var columns = Header(" Name ");

            Assert.Equal("  Maxwolf  ", columns.Value(new[] {"  Maxwolf  "}, "Name"));
        }

        [Fact]
        public void ARepeatedNameKeepsItsFirstColumn()
        {
            var columns = Header("Name", "Phone", "Phone");

            Assert.Equal(1, columns.IndexOf("Phone"));
            Assert.Equal("555-0100", columns.Value(new[] {"Maxwolf", "555-0100", "555-9999"}, "Phone"));
        }

        [Fact]
        public void ABlankColumnNameIsKeptInTheOrderButNamesNothing()
        {
            var columns = Header("Name", "", "Phone");

            Assert.Equal(3, columns.Count);
            Assert.Equal(string.Empty, columns.Names[1]);
            Assert.Equal(2, columns.IndexOf("Phone"));
        }

        [Fact]
        public void HasAllIsHowAReaderDecidesWhetherTheFirstRowIsAHeaderAtAll()
        {
            var header = Header("Name", "Phone", "Notes");
            var firstRecordOfAHeaderlessFile = Header("Maxwolf", "555-0100", "Large");

            Assert.True(header.HasAll("Name", "Notes"));
            Assert.False(firstRecordOfAHeaderlessFile.HasAll("Name", "Notes"));

            // Nothing asked for is vacuously true, which keeps a caller with no required columns from special casing.
            Assert.True(firstRecordOfAHeaderlessFile.HasAll());
        }

        [Fact]
        public void NoHeaderAtAllIsAnEmptySetRatherThanAThrow()
        {
            var columns = new DelimitedColumns(null);

            Assert.Equal(0, columns.Count);
            Assert.Empty(columns.Names);
            Assert.False(columns.Has("Name"));
            Assert.Equal(string.Empty, columns.Value(new[] {"Maxwolf"}, "Name"));
        }

        [Fact]
        public void ANullRowReadsAsEmptyRatherThanThrowing()
        {
            Assert.Equal(string.Empty, Header("Name").Value(null, "Name"));
        }

        [Fact]
        public void ItReadsWhatDelimitedTextGivesBack()
        {
            // Read through the real reader, since that is the only way it is ever going to be used.
            var rows = DelimitedText.Read("Name,Phone\r\nMaxwolf,\"555, ext 100\"\r\n");
            var columns = new DelimitedColumns(rows[0]);

            Assert.Equal("Maxwolf", columns.Value(rows[1], "Name"));
            Assert.Equal("555, ext 100", columns.Value(rows[1], "Phone"));
        }
    }
}
