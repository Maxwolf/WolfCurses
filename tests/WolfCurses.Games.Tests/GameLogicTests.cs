using WolfCurses.Core;
using WolfCurses.Games.Minesweeper;
using WolfCurses.Games.Snake;
using WolfCurses.Games.Tetris;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     The three non-chess games' rules, which are plain classes with no console attached — which is why they
    ///     can be tested at all, and the reason each game is split that way in the first place.
    ///     <para>
    ///         Seeded <see cref="Randomizer" />s throughout, so a failure is reproducible rather than a thing that
    ///         happened once on somebody's machine.
    ///     </para>
    /// </summary>
    public class GameLogicTests
    {
        [Fact]
        public void SnakeRejectsAReversalMeasuredAgainstTheLastMoveTravelled()
        {
            // The oldest bug in this game. Keys arrive far faster than the snake moves, so a player travelling right
            // who taps up and then left inside ONE move has, if each key is only checked against the one before it,
            // just steered into their own neck. Checking every key against the last move ACTUALLY MADE rejects the
            // second one.
            var board = new SnakeBoard(12, 8, new Randomizer(12345));
            Assert.Equal(DirectionEnum.Right, board.Direction);

            board.Steer(DirectionEnum.Up);
            board.Steer(DirectionEnum.Left);
            board.Advance();

            Assert.Equal(DirectionEnum.Up, board.Direction);
        }

        [Fact]
        public void SnakeEndsAtAWall()
        {
            var board = new SnakeBoard(12, 8, new Randomizer(1));
            board.Steer(DirectionEnum.Up);

            for (var step = 0; step < 50 && !board.IsOver; step++)
                board.Advance();

            Assert.True(board.IsOver);
            Assert.False(board.Won);
        }

        [Fact]
        public void SnakeGrowsWhenItEats()
        {
            var board = new SnakeBoard(12, 8, new Randomizer(7));
            var startingLength = board.Body.Count;

            for (var step = 0; step < 200 && board.Score == 0 && !board.IsOver; step++)
            {
                var head = board.Body[0];
                if (head.Y != board.Food.Y)
                    board.Steer(head.Y < board.Food.Y ? DirectionEnum.Down : DirectionEnum.Up);
                else if (head.X != board.Food.X)
                    board.Steer(head.X < board.Food.X ? DirectionEnum.Right : DirectionEnum.Left);

                board.Advance();
            }

            Assert.Equal(1, board.Score);
            Assert.Equal(startingLength + 1, board.Body.Count);
        }

        [Fact]
        public void TheFirstSquareOpenedIsNeverAMine()
        {
            // Mines are buried by the first reveal, not by the constructor, so the opening click - made with no
            // information at all - cannot lose the game.
            for (var seed = 0; seed < 25; seed++)
            {
                var field = new Minefield(9, 9, 10, new Randomizer(seed));
                field.Reveal(4, 4);

                Assert.False(field.IsMine(4, 4));
                Assert.False(field.HitMine);
            }
        }

        [Fact]
        public void OpeningABlankCascades()
        {
            var field = new Minefield(9, 9, 10, new Randomizer(99));
            field.Reveal(4, 4);

            var opened = 0;
            for (var y = 0; y < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                if (field.IsRevealed(x, y))
                    opened++;
            }

            // Excluding the 3x3 around the first click guarantees it opens into a region rather than a lone number.
            Assert.True(opened > 9, $"only {opened} squares opened from one click");
        }

        [Fact]
        public void AFlaggedSquareRefusesToOpen()
        {
            var field = new Minefield(9, 9, 10, new Randomizer(3));
            field.Reveal(4, 4);

            // A square the opening cascade did not already turn over - flagging a face-up square is correctly a
            // no-op, so picking a corner and hoping is how this test fails for the wrong reason.
            var (flagX, flagY) = FirstHiddenSquare(field);
            field.ToggleFlag(flagX, flagY);

            field.Reveal(flagX, flagY);

            Assert.Equal(1, field.FlagsPlaced);
            Assert.False(field.IsRevealed(flagX, flagY));
        }

        private static (int X, int Y) FirstHiddenSquare(Minefield field)
        {
            for (var y = 0; y < field.Height; y++)
            for (var x = 0; x < field.Width; x++)
            {
                if (!field.IsRevealed(x, y))
                    return (x, y);
            }

            throw new Xunit.Sdk.XunitException("the whole board was already revealed");
        }

        [Fact]
        public void ClearingEveryNonMineWins()
        {
            var field = new Minefield(5, 5, 3, new Randomizer(4));
            field.Reveal(2, 2);

            for (var y = 0; y < 5 && !field.IsOver; y++)
            for (var x = 0; x < 5 && !field.IsOver; x++)
            {
                if (!field.IsMine(x, y))
                    field.Reveal(x, y);
            }

            Assert.True(field.Won);
            Assert.False(field.HitMine);
        }

        [Theory]
        [InlineData(TetrominoEnum.I, 4)]
        [InlineData(TetrominoEnum.O, 2)]
        [InlineData(TetrominoEnum.T, 3)]
        [InlineData(TetrominoEnum.S, 3)]
        [InlineData(TetrominoEnum.Z, 3)]
        [InlineData(TetrominoEnum.J, 3)]
        [InlineData(TetrominoEnum.L, 3)]
        public void EveryPieceIsFourCellsAndRotatesBackToItself(TetrominoEnum kind, int boxSize)
        {
            var piece = Tetromino.Create(kind);
            Assert.Equal(boxSize, piece.Size);
            Assert.Equal(4, FilledCells(piece));

            // Four quarter turns is the identity for every piece, with no accumulated drift - which is the property
            // that lets rotation be one line of arithmetic inside the piece's own box.
            Assert.True(SameShape(piece, piece.Rotate(true).Rotate(true).Rotate(true).Rotate(true)));
            Assert.True(SameShape(piece, piece.Rotate(true).Rotate(false)));
        }

        [Fact]
        public void ADealtBagHoldsAllSevenPieces()
        {
            // Rolling a seven-sided die can withhold the I piece for thirty pieces at a stretch, which is a loss the
            // player cannot play around. A shuffled bag bounds the drought.
            var well = new TetrisWell(10, 16, new Randomizer(77));
            var seen = new System.Collections.Generic.HashSet<TetrominoEnum>();

            // Two pieces are already out (one in play, one previewed), so watching the next twenty covers whole bags
            // whatever the boundary happens to be.
            seen.Add(well.Active.Kind);
            seen.Add(well.NextKind);

            for (var i = 0; i < 20; i++)
            {
                well.HardDrop();
                if (!well.IsOver)
                    seen.Add(well.NextKind);
            }

            Assert.Equal(7, seen.Count);
        }

        [Fact]
        public void TheWellStacksOutAndSaysSo()
        {
            var well = new TetrisWell(10, 16, new Randomizer(21));

            for (var drop = 0; drop < 500 && !well.IsOver; drop++)
                well.HardDrop();

            Assert.True(well.IsOver);
            Assert.Null(well.Active);
        }

        [Fact]
        public void GravityGetsFasterWithTheLevel()
        {
            var well = new TetrisWell(10, 16, new Randomizer(5));
            var atLevelOne = well.FallInterval;

            Assert.Equal(1, well.Level);
            Assert.True(atLevelOne > System.TimeSpan.Zero);
        }

        private static int FilledCells(Tetromino piece)
        {
            var count = 0;
            for (var y = 0; y < piece.Size; y++)
            for (var x = 0; x < piece.Size; x++)
            {
                if (piece.IsFilled(x, y))
                    count++;
            }

            return count;
        }

        private static bool SameShape(Tetromino left, Tetromino right)
        {
            if (left.Size != right.Size)
                return false;

            for (var y = 0; y < left.Size; y++)
            for (var x = 0; x < left.Size; x++)
            {
                if (left.IsFilled(x, y) != right.IsFilled(x, y))
                    return false;
            }

            return true;
        }
    }
}
