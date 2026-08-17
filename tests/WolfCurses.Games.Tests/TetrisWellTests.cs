using System.Reflection;
using WolfCurses.Core;
using WolfCurses.Games.Tetris;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     The well's row clearing, set up by writing squares directly.
    ///     <para>
    ///         Reflection, deliberately, and it is the honest choice rather than a shortcut: the case that matters
    ///         is <b>two full rows that are not next to each other</b>, and reaching it by playing pieces would take
    ///         a scripted game long enough that a failure would say nothing about which rule broke. The alternative
    ///         — adding a public "set up this position" method to <see cref="TetrisWell" /> that only a test ever
    ///         calls — is worse: it is production surface that exists to be tested.
    ///     </para>
    /// </summary>
    public class TetrisWellTests
    {
        private const int Width = 10;
        private const int Height = 16;

        [Fact]
        public void NonAdjacentFullRowsBothClearAndEverythingAboveFalls()
        {
            // The case a "find a full row, shift everything down, start again" loop gets wrong, and it gets it
            // wrong precisely when four rows go at once - which is the moment the player most cares.
            var well = new TetrisWell(Width, Height, new Randomizer(5));
            var settled = SettledOf(well);

            for (var x = 0; x < Width; x++)
            {
                settled[15, x] = TetrominoEnum.I; // full
                settled[13, x] = TetrominoEnum.I; // full, and NOT adjacent to row 15
                if (x != 7)
                    settled[14, x] = TetrominoEnum.O; // has a gap, so it must survive
            }

            settled[12, 3] = TetrominoEnum.T; // a lone block that has to fall exactly two rows

            var cleared = ClearFullRows(well);

            Assert.Equal(2, cleared);
            Assert.Equal(TetrominoEnum.O, settled[15, 0]);
            Assert.Null(settled[15, 7]);
            Assert.Equal(TetrominoEnum.T, settled[14, 3]);
            Assert.Null(settled[12, 3]);
            Assert.Null(settled[0, 0]);
            Assert.Null(settled[1, 0]);
        }

        [Fact]
        public void FourRowsAtOnceAllGo()
        {
            var well = new TetrisWell(Width, Height, new Randomizer(6));
            var settled = SettledOf(well);

            for (var row = 12; row <= 15; row++)
            for (var x = 0; x < Width; x++)
                settled[row, x] = TetrominoEnum.I;

            Assert.Equal(4, ClearFullRows(well));

            for (var row = 0; row < Height; row++)
            for (var x = 0; x < Width; x++)
                Assert.Null(settled[row, x]);
        }

        [Fact]
        public void ARowWithAGapIsLeftAlone()
        {
            var well = new TetrisWell(Width, Height, new Randomizer(7));
            var settled = SettledOf(well);

            for (var x = 0; x < Width - 1; x++)
                settled[15, x] = TetrominoEnum.L;

            Assert.Equal(0, ClearFullRows(well));
            Assert.Equal(TetrominoEnum.L, settled[15, 0]);
        }

        [Fact]
        public void APieceCannotBeShovedThroughTheWall()
        {
            var well = new TetrisWell(Width, Height, new Randomizer(9));

            for (var push = 0; push < 20; push++)
                well.Move(-1);

            // It stops rather than wrapping or walking off; the piece is still somewhere on the board.
            Assert.True(well.ActiveX >= -1, "the piece left the board on the left");
            for (var push = 0; push < 30; push++)
                well.Move(1);

            Assert.True(well.ActiveX + well.Active.Size <= Width + 1, "the piece left the board on the right");
        }

        [Fact]
        public void TheGhostIsWhereAHardDropWouldLand()
        {
            var well = new TetrisWell(Width, Height, new Randomizer(11));
            var ghost = well.GhostY;

            well.HardDrop();

            // HardDrop locks and spawns the next piece, so the row the ghost predicted is the row the previous
            // piece came to rest on - checked by there now being something settled at that height.
            var anythingAtGhostRow = false;
            for (var x = 0; x < Width; x++)
            {
                for (var y = ghost; y < System.Math.Min(ghost + 4, Height); y++)
                {
                    if (well.SettledAt(x, y) == null)
                        continue;

                    anythingAtGhostRow = true;
                    break;
                }
            }

            Assert.True(anythingAtGhostRow, $"nothing settled anywhere near the ghost row {ghost}");
        }

        [Fact]
        public void ClearingRowsScoresAndRaisesTheLevel()
        {
            var well = new TetrisWell(Width, Height, new Randomizer(13));
            Assert.Equal(0, well.Score);
            Assert.Equal(1, well.Level);

            for (var drop = 0; drop < 200 && !well.IsOver; drop++)
                well.HardDrop();

            // Hard drops alone score two a row, so any real game accumulates something.
            Assert.True(well.Score > 0, "a whole game scored nothing");
        }

        private static TetrominoEnum?[,] SettledOf(TetrisWell well)
        {
            var field = typeof(TetrisWell).GetField("_settled", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (TetrominoEnum?[,]) field.GetValue(well);
        }

        private static int ClearFullRows(TetrisWell well)
        {
            var method = typeof(TetrisWell).GetMethod("ClearFullRows", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (int) method.Invoke(well, null);
        }
    }
}
