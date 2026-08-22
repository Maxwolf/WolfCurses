using System;
using System.Collections.Generic;
using System.Reflection;
using WolfCurses.Games.Tetris;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     Where the Next piece sits inside its fixed four-by-four box.
    ///     <para>
    ///         <b>Centring the box is not centring the piece.</b> A tetromino's box carries its own padding: five of
    ///         the seven fill only rows 0 and 1 of a three-row box, so centring the box leaves those five against
    ///         the top of the panel with two empty rows underneath while the O sits in the middle. That is
    ///         arithmetically centred and visibly not, which is the kind of thing a fixed-height panel exists to
    ///         avoid in the first place.
    ///     </para>
    ///     <para>
    ///         Asserted on the drawn ink rather than on the offsets, so the test cannot agree with the code by
    ///         restating its arithmetic.
    ///     </para>
    /// </summary>
    public class TetrisPreviewTests
    {
        /// <summary>How many cells across the preview box is; see <c>TetrisDialog.PreviewCells</c>.</summary>
        private const int Cells = 4;

        [Fact]
        public void EveryPieceIsCentredOnItsInkAndNotOnItsBox()
        {
            foreach (TetrominoEnum kind in Enum.GetValues<TetrominoEnum>())
            {
                var ink = InkOf(kind);

                Assert.True(ink.Count > 0, kind + " drew nothing at all");

                var left = int.MaxValue;
                var top = int.MaxValue;
                var right = -1;
                var bottom = -1;

                foreach (var (x, y) in ink)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }

                // The gap either side of the ink may differ by at most one, which is what "centred, odd cell to
                // the right and down" means. A box-centred version puts a three-row piece at rows 0-1 of four,
                // giving a top gap of 0 against a bottom gap of 2.
                var leftGap = left;
                var rightGap = Cells - 1 - right;
                var topGap = top;
                var bottomGap = Cells - 1 - bottom;

                Assert.True(Math.Abs(leftGap - rightGap) <= 1,
                    $"{kind} sits {leftGap} from the left and {rightGap} from the right");
                Assert.True(Math.Abs(topGap - bottomGap) <= 1,
                    $"{kind} sits {topGap} from the top and {bottomGap} from the bottom");
            }
        }

        [Fact]
        public void ThePreviewIsAlwaysTheSameSizeWhicheverPieceItIs()
        {
            // The panel's fixed height, asserted directly on the control rather than through a played game. Four
            // rows, eight visible columns, for every one of the seven.
            foreach (TetrominoEnum kind in Enum.GetValues<TetrominoEnum>())
            {
                var rows = Render(kind).Replace("\r\n", "\n").Split('\n');

                Assert.Equal(Cells, rows.Length);

                foreach (var row in rows)
                    Assert.Equal(Cells*2, AnsiText.VisibleLength(row));
            }
        }

        /// <summary>Which cells of the preview carry ink, read off what the real dialog draws.</summary>
        private static List<(int X, int Y)> InkOf(TetrominoEnum kind)
        {
            var rows = AnsiText.StripEscapes(Render(kind)).Replace("\r\n", "\n").Split('\n');
            var ink = new List<(int X, int Y)>();

            for (var y = 0; y < rows.Length; y++)
            for (var cell = 0; cell < Cells; cell++)
            {
                // Two columns per cell, because a character cell is about half as wide as it is tall.
                var column = cell*2;
                if (column < rows[y].Length && rows[y][column] != ' ')
                    ink.Add((cell, y));
            }

            return ink;
        }

        /// <summary>
        ///     Draws one piece through the dialog's own preview code, so what is measured is what ships. The method
        ///     is static and takes the grid, which is why no game, window or console is needed to ask it anything.
        /// </summary>
        /// <param name="kind">Which piece.</param>
        /// <returns>The drawn preview box.</returns>
        private static string Render(TetrominoEnum kind)
        {
            var method = typeof(TetrisDialog).GetMethod("DrawPreview",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var grid = new WolfCurses.Window.Control.TextGrid(Cells, Cells) {CellWidth = 2};
            return (string) method.Invoke(null, new object[] {grid, kind});
        }
    }
}
