using WolfCurses.Games.Minesweeper;
using Xunit;

namespace WolfCurses.Games.Tests.Minesweeper
{
    /// <summary>
    ///     Which of the original three boards a terminal gets. A pure function of the terminal's size, which is why
    ///     it takes the size rather than asking for it.
    /// </summary>
    public class MinesweeperBoardSizeTests
    {
        [Fact]
        public void ASmallTerminalGetsTheSmallBoard()
        {
            // Not the beginner board: a square is two rows tall, because a box has a line above its contents and
            // one below, so even nine by nine wants a terminal about thirty rows deep. Eighty by twenty-four gets
            // the short board that exists for it.
            var face = MinesweeperDialog.ChooseBoard(true, 80, 24);

            Assert.Equal(9, face.BoardWidth);
            Assert.Equal(6, face.BoardHeight);
            Assert.True(MinesweeperDialog.MinesFor(face) > 0);
        }

        [Fact]
        public void ABigTerminalGetsTheBigBoard()
        {
            // The honest answer to "the play area feels small": nine by nine is the right size for a window, and a
            // terminal is usually a great deal larger than a window was.
            var face = MinesweeperDialog.ChooseBoard(false, 200, 60);

            Assert.Equal(30, face.BoardWidth);
            Assert.Equal(16, face.BoardHeight);
            Assert.Equal(99, MinesweeperDialog.MinesFor(face));
        }

        [Fact]
        public void ATerminalInBetweenGetsTheBoardInBetween()
        {
            var face = MinesweeperDialog.ChooseBoard(false, 60, 30);

            Assert.Equal(9, face.BoardWidth);
            Assert.Equal(9, face.BoardHeight);
            Assert.Equal(10, MinesweeperDialog.MinesFor(face));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WhateverIsChosenFitsTheTerminalItWasChosenFor(bool labelled)
        {
            // Swept rather than sampled, because the interesting sizes are the ones on either side of a boundary and
            // nobody guesses where those are. The smallest board is the floor: below that there is nothing to offer
            // and a cramped board beats no board.
            for (var columns = 30; columns <= 200; columns += 7)
            for (var rows = 14; rows <= 60; rows += 3)
            {
                var face = MinesweeperDialog.ChooseBoard(labelled, columns, rows);

                if (face.BoardWidth == 9 && face.BoardHeight == 6)
                    continue;

                Assert.True(face.Columns <= columns,
                    $"a {face.BoardWidth}x{face.BoardHeight} board wants {face.Columns} of {columns} columns");
                Assert.True(face.Rows <= rows,
                    $"a {face.BoardWidth}x{face.BoardHeight} board wants {face.Rows} of {rows} rows");
            }
        }

        [Fact]
        public void ABoardThatCannotNameItsOwnColumnsIsNeverOfferedToATypist()
        {
            // Thirty columns runs off the end of the alphabet, and nobody is typing "AD7". A terminal with a pointer
            // is welcome to it, since a click needs no name at all.
            var typed = MinesweeperDialog.ChooseBoard(true, 200, 60);
            var clicked = MinesweeperDialog.ChooseBoard(false, 200, 60);

            Assert.True(typed.BoardWidth <= MinesweeperFace.WidestLabelledBoard);
            Assert.True(clicked.BoardWidth > MinesweeperFace.WidestLabelledBoard);
        }

        [Fact]
        public void ATerminalTooSmallForAnythingStillGetsABoard()
        {
            // A cramped board beats a blank screen and an exception, and the presenter will clip whatever does not
            // fit rather than fall over.
            var face = MinesweeperDialog.ChooseBoard(true, 10, 5);

            Assert.Equal(9, face.BoardWidth);
            Assert.Equal(6, face.BoardHeight);
        }

        [Fact]
        public void TheCoordinateGutterIsPaidForOutOfTheBoardSize()
        {
            // The gutter is not free: it costs a row and three columns, so a terminal that is only just big enough
            // for a board with a pointer is not big enough for the same board with coordinates on it.
            Assert.True(MinesweeperFace.ColumnsFor(16, true) > MinesweeperFace.ColumnsFor(16, false));
            Assert.True(MinesweeperFace.RowsFor(16, true) > MinesweeperFace.RowsFor(16, false));
        }
    }
}
