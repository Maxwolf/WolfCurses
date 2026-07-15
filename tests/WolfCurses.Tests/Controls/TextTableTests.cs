using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    public class TextTableTests
    {
        private sealed class Row
        {
            public string Name { get; set; }

            public int Age { get; set; }
        }

        private static readonly Row[] _rows =
        {
            new() { Name = "Alice", Age = 30 },
            new() { Name = "Bob", Age = 9 }
        };

        [Fact]
        public void ToStringTable_WithHeaders_RendersHeaderRowSeparatorAndValues()
        {
            var table = _rows.ToStringTable(
                new[] { "Name", "Age" },
                r => r.Name, r => r.Age);

            var lines = Text.Norm(table).Split('\n');
            Assert.Equal(" | Name  | Age | ", lines[0]);
            Assert.Equal(" |-------------| ", lines[1]);
            Assert.Equal(" | Alice | 30  | ", lines[2]);
            Assert.Equal(" | Bob   | 9   | ", lines[3]);
        }

        [Fact]
        public void ToStringTable_NullCell_RendersNullLiteral()
        {
            var rows = new[] { new Row { Name = null, Age = 1 } };

            var table = rows.ToStringTable(new[] { "Name", "Age" }, r => r.Name, r => r.Age);

            Assert.Contains("| null |", table);
        }

        [Fact]
        public void ToStringTable_ExpressionOverload_DerivesHeadersFromPropertyNames()
        {
            var table = _rows.ToStringTable(r => r.Name, r => r.Age);

            Assert.Contains("| Name ", table);
            Assert.Contains("| Age ", table);
            Assert.Contains("| Alice ", table);
        }

        [Fact]
        public void ToStringTable_TwoDimensionalArray_PadsColumnsToWidestCell()
        {
            var cells = new[,]
            {
                { "H", "Header2" },
                { "LongValue", "x" }
            };

            var lines = Text.Norm(cells.ToStringTable()).Split('\n');
            Assert.Equal(" | H         | Header2 | ", lines[0]);
            Assert.Equal(" | LongValue | x       | ", lines[2]);
        }

        [Fact]
        public void ToStringTable_EmptyValues_StillRendersHeaderAndSeparator()
        {
            var table = System.Array.Empty<Row>().ToStringTable(new[] { "Name" }, r => r.Name);

            var lines = Text.Norm(table).Split('\n');
            Assert.Equal(" | Name | ", lines[0]);
            Assert.StartsWith(" |----", lines[1]);
        }
    }
}
