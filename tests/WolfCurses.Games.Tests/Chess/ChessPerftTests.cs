using WolfCurses.Games.Chess;
using Xunit;

namespace WolfCurses.Games.Tests.Chess
{
    /// <summary>
    ///     Perft: count every leaf of the move tree and compare against the published totals.
    ///     <para>
    ///         <b>This is the only test of a move generator that is worth anything.</b> Hand-written cases assert
    ///         what the author believed the rules were, which is exactly the belief most likely to be wrong. A perft
    ///         count moves if a rule is wrong <i>anywhere</i> in the tree — one castle through check, one en passant
    ///         that should have been illegal because it uncovered a rook along the rank, one under-promotion never
    ///         generated — and the reference numbers are agreed on by every engine, so a mismatch is unambiguous.
    ///     </para>
    ///     <para>
    ///         Depths are capped so the whole file runs in a few seconds. <c>dotnet run --project
    ///         example/WolfCurses.Games -- perft 5</c> goes deeper by hand when a change warrants it; the two share
    ///         <see cref="ChessPerft" />, so they cannot disagree about what they are counting.
    ///     </para>
    /// </summary>
    public class ChessPerftTests
    {
        [Theory]
        [InlineData(1, 20)]
        [InlineData(2, 400)]
        [InlineData(3, 8_902)]
        [InlineData(4, 197_281)]
        public void InitialPosition(int depth, long expected)
        {
            AssertPerft(ChessBoard.StartingFen, depth, expected);
        }

        [Theory]
        [InlineData(1, 48)]
        [InlineData(2, 2_039)]
        [InlineData(3, 97_862)]
        [InlineData(4, 4_085_603)]
        public void Kiwipete(int depth, long expected)
        {
            // The standard second position, and the one that catches almost everything: castling both ways for both
            // sides, pins, and a pawn that can capture en passant.
            AssertPerft("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", depth, expected);
        }

        [Theory]
        [InlineData(1, 14)]
        [InlineData(2, 191)]
        [InlineData(3, 2_812)]
        [InlineData(4, 43_238)]
        [InlineData(5, 674_624)]
        public void EndgameWithARankPinnedEnPassant(int depth, long expected)
        {
            // The position that exists to catch the en passant capture which is illegal because taking removes TWO
            // pawns from one rank and exposes the king to a rook along it. A generator that treats en passant as an
            // ordinary capture passes every other test and fails this one.
            AssertPerft("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", depth, expected);
        }

        [Theory]
        [InlineData(1, 6)]
        [InlineData(2, 264)]
        [InlineData(3, 9_467)]
        [InlineData(4, 422_333)]
        public void PromotionsAndPins(int depth, long expected)
        {
            AssertPerft("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", depth, expected);
        }

        [Theory]
        [InlineData(1, 44)]
        [InlineData(2, 1_486)]
        [InlineData(3, 62_379)]
        [InlineData(4, 2_103_487)]
        public void MirroredWithoutCastling(int depth, long expected)
        {
            AssertPerft("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", depth, expected);
        }

        [Theory]
        [InlineData(1, 46)]
        [InlineData(2, 2_079)]
        [InlineData(3, 89_890)]
        [InlineData(4, 3_894_594)]
        public void StevenEdwardsPosition(int depth, long expected)
        {
            AssertPerft("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", depth, expected);
        }

        [Fact]
        public void MakeAndUnmakeAreExactlyReversible()
        {
            // Separate from the counts, because a make/unmake that leaks state can still produce the right total
            // while corrupting the position - and then everything downstream of it is quietly wrong. A FEN that
            // survives a whole walk is the strongest cheap statement that nothing leaked: it covers the castling
            // rights, the en passant square and both clocks, none of which the node count can see.
            foreach (var fen in new[]
                     {
                         ChessBoard.StartingFen,
                         "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                         "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1"
                     })
            {
                var board = new ChessBoard(fen);
                ChessPerft.Count(board, 3);

                Assert.Equal(fen, board.ToFen());
            }
        }

        [Fact]
        public void DivideSplitsTheCountByFirstMove()
        {
            // How a mismatch is actually hunted down: compare the split against a known-good engine and the one
            // move whose subtree differs is the bug. Worth a test so the tool still works when it is needed.
            var board = new ChessBoard();
            var split = ChessPerft.Divide(board, 3);

            long total = 0;
            foreach (var (_, nodes) in split)
                total += nodes;

            Assert.Equal(20, split.Length);
            Assert.Equal(8_902, total);
        }

        private static void AssertPerft(string fen, int depth, long expected)
        {
            var board = new ChessBoard(fen);

            var counted = ChessPerft.Count(board, depth);

            Assert.True(counted == expected,
                $"{fen} at depth {depth}: counted {counted:N0}, expected {expected:N0}.");
        }
    }
}
