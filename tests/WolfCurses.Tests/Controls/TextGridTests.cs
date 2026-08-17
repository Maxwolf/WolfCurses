using System;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The grid of styled characters, and the window onto it.
    ///     <para>
    ///         <see cref="TextGrid.ColorMode" /> is pinned on the grid rather than through the environment in every
    ///         test here, for the reason the other widget tests give: <c>AnsiConsole.DetectColorMode</c> is process
    ///         cached, so a test that set <c>NO_COLOR</c> and reset the cache would race every other test in this
    ///         assembly, which runs in the parallel default collection.
    ///     </para>
    /// </summary>
    public class TextGridTests
    {
        private const char Escape = (char) 27;

        // ------------------------------------------------------------ nobody coloured it

        [Fact]
        public void AGridNobodyColouredEmitsNoEscapesAtAll()
        {
            // The compatibility invariant every widget in this library keeps, and the reason a game can adopt this
            // type without its pinned screens moving: an untouched grid is exactly the characters it holds. Left at
            // Auto deliberately - the point is that an unstyled grid does not even ask what the terminal can do.
            var grid = new TextGrid(4, 2);
            grid.Set(1, 0, 'x');
            grid.Set(3, 1, 'y');

            var rendered = grid.Render();

            Assert.DoesNotContain(Escape, rendered);
            Assert.Equal(" x  " + Environment.NewLine + "   y", rendered);
        }

        [Fact]
        public void AResolvedModeOfNoneEmitsNothingEvenForCellsThatWereStyled()
        {
            // Somebody who set NO_COLOR asked for no escape sequences, not for a subset of them.
            var grid = new TextGrid(3, 1) {ColorMode = AnsiColorModeEnum.None};
            grid.Set(0, 0, 'a', ConsoleColor.Red);
            grid.Set(1, 0, 'b', new TextStyle(ConsoleColor.Blue, bold: true));

            var rendered = grid.Render();

            Assert.DoesNotContain(Escape, rendered);
            Assert.Equal("ab ", rendered);
        }

        // ------------------------------------------------------------ the rectangle

        [Fact]
        public void CellWidthRepeatsEveryCellIncludingTheBlankOnes()
        {
            var grid = new TextGrid(3, 2) {CellWidth = 2};
            grid.Set(0, 0, '#');

            Assert.Equal("##    " + Environment.NewLine + "      ", grid.Render());
        }

        [Fact]
        public void AWindowHangingOffTheEdgeComesBackTheSizeItAskedFor()
        {
            // The invariant the box around a scrolling view depends on. Ask for four by two from a corner and four by
            // two is what arrives, padded with blanks - a renderer that stopped at the edge instead would resize its
            // frame on every step near a wall.
            var grid = new TextGrid(2, 2);
            grid.Fill('#');

            var rendered = grid.Render(-1, -1, 4, 3);
            var lines = rendered.Split(Environment.NewLine);

            Assert.Equal(3, lines.Length);
            Assert.All(lines, line => Assert.Equal(4, line.Length));
            Assert.Equal("    ", lines[0]);
            Assert.Equal(" ## ", lines[1]);
            Assert.Equal(" ## ", lines[2]);
        }

        [Fact]
        public void AStyledRowIsStillExactlyAsManyColumnsWideAsItWasAskedFor()
        {
            // Length and width are different numbers once colour is involved, and this is the one that matters to
            // anything laying the grid out beside something else.
            var grid = new TextGrid(5, 1) {CellWidth = 2, ColorMode = AnsiColorModeEnum.TrueColor};
            for (var x = 0; x < 5; x++)
                grid.Set(x, 0, '#', new TextStyle(new TextColor(new Rgb24((byte) (x*40), 0, 0))));

            var rendered = grid.Render(0, 0, 5, 1);

            Assert.Equal(10, AnsiText.VisibleLength(rendered));
            Assert.True(rendered.Length > 10, "the row carried no colour at all");
        }

        [Fact]
        public void RenderingNothingIsAnEmptyStringRatherThanARowOfNothing()
        {
            var grid = new TextGrid(4, 4);

            Assert.Equal(string.Empty, grid.Render(0, 0, 0, 3));
            Assert.Equal(string.Empty, grid.Render(0, 0, 3, 0));
        }

        // ------------------------------------------------------------ runs

        [Fact]
        public void NeighboursWhoseColoursQuantizeTogetherShareOneEscape()
        {
            // THE lesson this type inherited. Quantization happens downstream in TextColor, so two distinct colours
            // can reach the terminal as byte-identical sequences; breaking the run between them is invisible and
            // costs a reset plus an open per cell on a string that is rebuilt every frame.
            var first = new TextStyle(new TextColor(new Rgb24(100, 100, 100)));
            var second = new TextStyle(new TextColor(new Rgb24(102, 102, 102)));

            Assert.NotEqual(first, second);
            Assert.Equal(first.OpenSequence(AnsiColorModeEnum.Grayscale),
                second.OpenSequence(AnsiColorModeEnum.Grayscale));

            var grid = new TextGrid(2, 1) {ColorMode = AnsiColorModeEnum.Grayscale};
            grid.Set(0, 0, 'a', first);
            grid.Set(1, 0, 'b', second);

            var rendered = grid.Render();

            Assert.Equal(0, AnsiRuns.CountRedundantRuns(rendered));
            Assert.Equal(2, AnsiRuns.Escapes(rendered).Count);
        }

        [Fact]
        public void NeighboursWhoseColoursActuallyDifferDoNotShareOne()
        {
            // The other half, or "coalesce everything" would pass the test above.
            var grid = new TextGrid(2, 1) {ColorMode = AnsiColorModeEnum.TrueColor};
            grid.Set(0, 0, 'a', new TextStyle(new TextColor(new Rgb24(255, 0, 0))));
            grid.Set(1, 0, 'b', new TextStyle(new TextColor(new Rgb24(0, 255, 0))));

            var escapes = AnsiRuns.Escapes(grid.Render());

            Assert.Equal(4, escapes.Count);
            Assert.NotEqual(escapes[0], escapes[2]);
        }

        [Fact]
        public void AStyleNeverSurvivesIntoTheNextRow()
        {
            // An escape has length but no width. A style left open across the newline colours cells nobody styled,
            // and is counted as columns by anything measuring the row below it.
            var grid = new TextGrid(2, 2) {ColorMode = AnsiColorModeEnum.TrueColor};
            grid.Fill('#', new TextStyle(new TextColor(new Rgb24(10, 20, 30))));

            var lines = grid.Render().Split(Environment.NewLine);

            Assert.Equal(2, lines.Length);
            Assert.All(lines, line => Assert.EndsWith(TextStyle.ResetSequence, line, StringComparison.Ordinal));
            Assert.All(lines, line => Assert.Equal(2, AnsiText.VisibleLength(line)));
        }

        [Fact]
        public void TheBlanksOutsideTheGridAreNotDraggedIntoTheirNeighboursColour()
        {
            // A window that runs off the edge reads unstyled blanks, so the run has to close at the boundary rather
            // than painting the void with whatever the last real cell was wearing.
            var grid = new TextGrid(1, 1) {ColorMode = AnsiColorModeEnum.TrueColor};
            grid.Set(0, 0, '#', new TextStyle(new TextColor(new Rgb24(255, 255, 255)), new TextColor(new Rgb24(9, 9, 9))));

            var rendered = grid.Render(0, 0, 3, 1);

            Assert.Equal(3, AnsiText.VisibleLength(rendered));
            Assert.EndsWith(TextStyle.ResetSequence + "  ", rendered, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------ the camera

        [Theory]
        [InlineData(0, 10, 40, 0)] // hard against the near edge
        [InlineData(5, 10, 40, 0)] // still clamped: centring on 5 would start at -1
        [InlineData(20, 10, 40, 15)] // free in the middle
        [InlineData(39, 10, 40, 30)] // hard against the far edge
        [InlineData(20, 40, 40, 0)] // the window is the whole world
        [InlineData(20, 99, 40, 0)] // and bigger than it
        public void TheCameraStaysInsideTheWorld(int focus, int visible, int total, int expected)
        {
            Assert.Equal(expected, TextGrid.CenterOrigin(focus, visible, total));
        }

        [Fact]
        public void TheCameraClampsRatherThanWrappingAtTheFarEdge()
        {
            // Walking into the last cell must not scroll the view past the end of the world and show a band of
            // nothing - the focus drifts off centre instead, which is what every player expects.
            const int total = 20;
            const int visible = 7;

            for (var focus = 0; focus < total; focus++)
            {
                var origin = TextGrid.CenterOrigin(focus, visible, total);

                Assert.InRange(origin, 0, total - visible);
                Assert.InRange(focus, origin, origin + visible - 1);
            }
        }

        // ------------------------------------------------------------ writing

        [Fact]
        public void WritesOffTheGridAreDroppedRatherThanThrown()
        {
            // The same bargain PixelBuffer strikes. Callers plot from world coordinates, where being off the edge is
            // an ordinary event rather than a mistake.
            var grid = new TextGrid(2, 2);

            var thrown = Record.Exception(() =>
            {
                grid.Set(-1, 0, 'x');
                grid.Set(0, -1, 'x');
                grid.Set(2, 0, 'x', ConsoleColor.Red);
                grid.Set(0, 2, 'x', ConsoleColor.Red);
            });

            Assert.Null(thrown);
            Assert.Equal("  " + Environment.NewLine + "  ", grid.Render());
        }

        [Fact]
        public void FillPaintsThePartOfARectangleThatFits()
        {
            var grid = new TextGrid(4, 3);
            grid.Fill(-2, -1, 4, 3, '#');

            Assert.Equal(
                "##  " + Environment.NewLine +
                "##  " + Environment.NewLine +
                "    ", grid.Render());
        }

        [Fact]
        public void ARectangleEntirelyOffTheGridPaintsNothing()
        {
            var grid = new TextGrid(2, 2);
            grid.Fill('.');
            grid.Fill(9, 9, 4, 4, '#');
            grid.Fill(-9, 0, 4, 4, '#');

            Assert.Equal(".." + Environment.NewLine + "..", grid.Render());
        }

        [Fact]
        public void TextIsWrittenOneCharacterPerCell()
        {
            var grid = new TextGrid(8, 2);
            grid.DrawText(2, 1, "hi");

            Assert.Equal("        " + Environment.NewLine + "  hi    ", grid.Render());
        }

        [Fact]
        public void TextIsClippedAtBothEndsRatherThanThrowing()
        {
            var grid = new TextGrid(4, 1);
            grid.DrawText(-2, 0, "abcdefgh");

            Assert.Equal("cdef", grid.Render());
        }

        [Fact]
        public void TextEntirelyOffTheGridWritesNothing()
        {
            var grid = new TextGrid(4, 2);
            grid.DrawText(0, -1, "nope");
            grid.DrawText(0, 2, "nope");
            grid.DrawText(4, 0, "nope");
            grid.DrawText(-4, 0, "nope");
            grid.DrawText(0, 0, null);
            grid.DrawText(0, 0, string.Empty);

            Assert.Equal("    " + Environment.NewLine + "    ", grid.Render());
        }

        [Fact]
        public void TextTakesTheStyleItWasGivenAndNothingElseDoes()
        {
            var grid = new TextGrid(6, 1) {ColorMode = AnsiColorModeEnum.TrueColor};
            grid.DrawText(2, 0, "ab", new TextStyle(new TextColor(new Rgb24(1, 2, 3))));

            var rendered = grid.Render();

            Assert.Equal(6, AnsiText.VisibleLength(rendered));
            Assert.Equal("  ab  ", AnsiText.StripEscapes(rendered));
            Assert.Equal(2, AnsiRuns.Escapes(rendered).Count);
        }

        [Fact]
        public void TextIsSpacedOutByCellWidthRatherThanDoubledUp()
        {
            // Cells, not columns. Doubling every character would spell "hhii", which is not a word - so a caller who
            // wants text at its natural width puts it beside the grid rather than in it.
            var grid = new TextGrid(4, 1) {CellWidth = 2};
            grid.DrawText(0, 0, "hi");

            Assert.Equal("hhii    ", grid.Render());
        }

        [Fact]
        public void ReadingOffTheGridAnswersBlankRatherThanThrowing()
        {
            var grid = new TextGrid(2, 2) {Blank = '~'};

            Assert.Equal('~', grid.GlyphAt(-1, 0));
            Assert.Equal('~', grid.GlyphAt(0, 5));
            Assert.Equal(TextStyle.None, grid.StyleAt(-1, -1));
            Assert.False(grid.Contains(2, 0));
            Assert.True(grid.Contains(1, 1));
        }

        [Fact]
        public void AGridStartsFullOfItsOwnBlank()
        {
            var grid = new TextGrid(3, 1);

            Assert.Equal("   ", grid.Render());
            Assert.Equal(' ', grid.GlyphAt(0, 0));
        }

        [Fact]
        public void ClearingTakesTheColourOffAsWellAsTheCharacters()
        {
            var grid = new TextGrid(2, 1) {ColorMode = AnsiColorModeEnum.TrueColor};
            grid.Set(0, 0, '#', new TextStyle(new TextColor(new Rgb24(1, 2, 3))));
            grid.Clear();

            Assert.Equal(TextStyle.None, grid.StyleAt(0, 0));
            Assert.DoesNotContain(Escape, grid.Render());
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(-4, 4)]
        public void AGridWithNoAreaIsRefused(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextGrid(width, height));
        }
    }
}
