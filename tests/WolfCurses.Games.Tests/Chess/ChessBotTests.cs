using System;
using WolfCurses.Games.Chess;
using Xunit;

namespace WolfCurses.Games.Tests.Chess
{
    /// <summary>
    ///     What the bot must actually do. Every test here decides right and wrong by asking the <i>rules</i> rather
    ///     than by naming the move the author thought was best — a mate is checked by playing whatever the bot chose
    ///     and asking whether the game is over — so these are statements about the bot rather than about opinions.
    /// </summary>
    public class ChessBotTests
    {
        /// <summary>The per-tick budget the dialog uses, so the tests exercise the slicing the game really runs.</summary>
        private static readonly TimeSpan _slice = TimeSpan.FromMilliseconds(15);

        /// <summary>Thinks to completion the way the dialog does, and refuses to loop forever doing it.</summary>
        private static ChessMove Decide(IChessBot bot, ChessGame game, TimeSpan? slice = null)
        {
            bot.Begin(game);

            var slices = 0;
            while (!bot.Think(slice ?? _slice))
            {
                // The livelock guard, and the reason it is an assertion rather than a comment: the first version of
                // this search abandoned an iteration whenever the budget ran out and retried it next tick, which
                // never terminates once one iteration is longer than one slice - and the deepest always is.
                Assert.True(++slices < 100_000, "the bot never finished thinking");
            }

            return bot.BestMove;
        }

        [Theory]
        [InlineData("k7/7R/1K6/8/8/8/8/8 w - - 0 1")]
        [InlineData("6k1/8/6K1/8/8/8/8/1Q6 w - - 0 1")]
        [InlineData("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1")]
        public void ItFindsMateInOne(string fen)
        {
            var game = new ChessGame(fen);

            var move = Decide(new WolfChessBot(), game);
            Assert.True(move.IsValid, "no move was chosen");
            game.Play(move);

            Assert.Equal(ChessResultEnum.Checkmate, game.Result);
        }

        [Fact]
        public void ItTakesAFreeQueen()
        {
            var game = new ChessGame("4k3/8/8/3q4/8/8/8/3RK3 w - - 0 1");

            var move = Decide(new WolfChessBot(), game);

            Assert.Equal(PieceKindEnum.Queen, game.Board[move.To].Kind);
        }

        [Fact]
        public void ItDoesNotHangItsOwnQueenToARecapture()
        {
            // The rook on b7 is defended by the king beside it, and the white queen can take it down the b-file.
            // Searched at DEPTH ONE deliberately, so the recapture falls one ply past where the search stops and
            // ONLY the quiescence search can see it.
            //
            // The bait has to be big enough to be taken. A defended PAWN is not: at depth one the tables can rate
            // some quiet queen move above a hundred centipawns and the bot declines the capture for reasons that
            // have nothing to do with the horizon, so the test passes with quiescence deleted - which is what a
            // first version of it did. A defended ROOK is +500 by material and cannot be out-scored by position,
            // so the only thing that can stop Qxb7 is seeing Kxb7.
            var game = new ChessGame("1k6/1r6/8/8/8/8/8/1Q2K3 w - - 0 1");

            var move = Decide(new WolfChessBot(1), game);

            var movedQueen = game.Board[move.From].Kind == PieceKindEnum.Queen;
            Assert.False(movedQueen && ChessBoard.SquareName(move.To) == "b7",
                "played Qxb7 at depth 1, so nothing is looking past the horizon at Kxb7");
        }

        [Theory]
        [InlineData("4k3/8/8/8/8/8/8/3QK3 w - - 0 1")] // king and queen
        [InlineData("4k3/8/8/8/8/8/8/3RK3 w - - 0 1")] // king and rook
        [InlineData("4k3/8/8/8/8/8/8/R3K2R w - - 0 1")] // king and two rooks
        public void ItMatesALoneKing(string fen)
        {
            // The test that caught the bot being unable to WIN a won game. Material and the piece-square tables
            // score every king-and-queen-versus-king position identically, so nothing distinguished the move that
            // mates from the move that shuffles: before the mop-up term all of these ended in threefold repetition
            // with the bot a queen up.
            var game = new ChessGame(fen);
            var bots = new IChessBot[] {new WolfChessBot(4), new WolfChessBot(4)};

            for (var ply = 0; ply < 80 && !game.IsOver; ply++)
            {
                var move = Decide(bots[(int) game.Board.SideToMove], game);
                Assert.True(move.IsValid, $"no move at ply {ply}");
                game.Play(move);
            }

            Assert.Equal(ChessResultEnum.Checkmate, game.Result);
        }

        [Theory]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
        [InlineData("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4")]
        [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]
        public void TheSliceSizeDoesNotChangeTheMoveItPlays(string fen)
        {
            // The property that makes slicing safe at all. The resumable unit is one ROOT MOVE and alpha is carried
            // across ticks, so a search chopped into a hundred pieces prunes identically to one run straight
            // through and therefore answers identically.
            //
            // DEPTH THREE, not four, and the depth assertion below is not decoration. The bot's one wall-clock
            // dependency is its three-second hard deadline, which exists so a pathological position cannot think
            // forever - and a deadline is *designed* to change the answer when it fires. At depth 4 in a Debug
            // build a move is around a second of work, so on a machine busy running the rest of the suite in
            // parallel the many-slices runs crossed the deadline and stopped a ply short while the single-slice
            // run did not: same move, different score, and a test that failed only under load. Depth 3 is about
            // sixty milliseconds, which cannot approach the deadline however contended the machine is, and
            // asserting the depth means that if it ever does the failure says so plainly instead of surfacing as
            // a mysterious score mismatch.
            const int depth = 3;

            var game = new ChessGame(fen);
            var reference = ChessMove.None;
            var referenceScore = 0;

            foreach (var milliseconds in new[] {1, 5, 15, 40, 5_000})
            {
                var bot = new WolfChessBot(depth);
                var move = Decide(bot, game, TimeSpan.FromMilliseconds(milliseconds));

                Assert.True(bot.CompletedDepth == depth,
                    $"a {milliseconds}ms slice only reached depth {bot.CompletedDepth}, so the hard deadline " +
                    "fired and this comparison is not the one the test means to make");

                if (!reference.IsValid)
                {
                    reference = move;
                    referenceScore = bot.LastScore;
                    continue;
                }

                Assert.True(move == reference,
                    $"a {milliseconds}ms slice chose {move} where a 1ms slice chose {reference}");

                // The SCORE as well as the move. A resumption that loses its alpha between slices can still stumble
                // onto the same move while having searched a different tree to reach it, and the move alone would
                // not notice - it is the score that says the search was the same search.
                Assert.True(bot.LastScore == referenceScore,
                    $"a {milliseconds}ms slice scored {bot.LastScore} at depth {bot.CompletedDepth} " +
                    $"({bot.NodesSearched:N0} nodes) where a 1ms slice scored {referenceScore}");
            }
        }

        [Fact]
        public void EveryMoveItPlaysIsLegal()
        {
            var game = new ChessGame();
            var white = new WolfChessBot(3);
            var black = new WolfChessBot(3);

            for (var ply = 0; ply < 40 && !game.IsOver; ply++)
            {
                var bot = game.Board.SideToMove == PieceColorEnum.White ? white : black;
                var move = Decide(bot, game);

                Assert.Contains(move, game.LegalMoves);
                game.Play(move);
            }
        }

        [Fact]
        public void ItAnswersImmediatelyWhenThereIsNothingToDecide()
        {
            // A finished position has no root moves at all, and a driver that assumes at least one would spin.
            var game = new ChessGame("R5k1/5ppp/8/8/8/8/8/6K1 b - - 0 1");
            Assert.Equal(ChessResultEnum.Checkmate, game.Result);

            var bot = new WolfChessBot();
            bot.Begin(game);

            Assert.True(bot.Think(_slice));
            Assert.False(bot.BestMove.IsValid);
        }

        [Fact]
        public void ADeeperSearchLooksAtMoreAndStillAnswers()
        {
            var game = new ChessGame("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4");

            var shallow = new WolfChessBot(2);
            var deep = new WolfChessBot(4);

            Assert.True(Decide(shallow, game).IsValid);
            Assert.True(Decide(deep, game).IsValid);

            Assert.Equal(2, shallow.CompletedDepth);
            Assert.Equal(4, deep.CompletedDepth);
            Assert.True(deep.NodesSearched > shallow.NodesSearched);
        }

        [Fact]
        public void ItDoesNotDisturbTheGameItIsThinkingAbout()
        {
            // The bot clones the position, because the caller is still drawing the real one between slices. A
            // search that worked on the live board would leave it subtly wrong after an odd number of unmakes.
            var game = new ChessGame();
            var before = game.Board.ToFen();

            Decide(new WolfChessBot(4), game);

            Assert.Equal(before, game.Board.ToFen());
        }
    }
}
