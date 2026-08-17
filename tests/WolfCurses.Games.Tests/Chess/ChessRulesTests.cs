using System;
using System.Collections.Generic;
using WolfCurses.Games.Chess;
using Xunit;

namespace WolfCurses.Games.Tests.Chess
{
    /// <summary>
    ///     The parts of the rules perft cannot see. Perft counts moves, so a game that never noticed checkmate, wrote
    ///     every move as "Nd2" or called a bare-kings ending a win would pass every perft in the world.
    /// </summary>
    public class ChessRulesTests
    {
        /// <summary>Positions used repeatedly below, worth naming once.</summary>
        private static readonly string[] _positions =
        {
            ChessBoard.StartingFen,
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
            "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1"
        };

        [Fact]
        public void ScholarsMateIsCheckmateAndWhiteWins()
        {
            var game = Play(new ChessGame(), "e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7");

            Assert.Equal(ChessResultEnum.Checkmate, game.Result);
            Assert.Equal(PieceColorEnum.White, game.Winner);
            Assert.Equal("Qxf7#", game.Notation[^1]);
        }

        [Fact]
        public void NoMoveAndNoCheckIsStalemate()
        {
            // The only difference between the best result in chess and a draw is whether the king happens to be
            // attacked while having nowhere to go.
            var game = new ChessGame("5k2/5P2/5K2/8/8/8/8/8 b - - 0 1");

            Assert.Equal(ChessResultEnum.Stalemate, game.Result);
        }

        [Fact]
        public void APositionLoadedAlreadyFinishedKnowsIt()
        {
            // A game that only decided its result after a move would call a loaded mate "in progress" and let the
            // player move out of it.
            var game = new ChessGame("6k1/5ppp/8/8/8/8/8/R5K1 b - - 0 1");
            Assert.Equal(ChessResultEnum.InProgress, game.Result);

            var mated = new ChessGame("R5k1/5ppp/8/8/8/8/8/6K1 b - - 0 1");
            Assert.Equal(ChessResultEnum.Checkmate, mated.Result);
        }

        [Fact]
        public void TwoKnightsReachingOneSquareAreDisambiguatedByFile()
        {
            var game = new ChessGame("4k3/8/8/8/8/5N2/8/1N2K3 w - - 0 1");
            var names = NamesOfLegalMoves(game);

            Assert.Contains("Nbd2", names);
            Assert.Contains("Nfd2", names);
        }

        [Fact]
        public void EveryMoveInAPositionHasADifferentName()
        {
            // Stronger than any single disambiguation case: if two legal moves are written the same way, the
            // notation is lossy and one of them cannot be played by name.
            foreach (var fen in _positions)
            {
                var game = new ChessGame(fen);
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var move in game.LegalMoves)
                    Assert.True(seen.Add(game.ToSan(move)), $"\"{game.ToSan(move)}\" names two moves in {fen}");
            }
        }

        [Fact]
        public void EveryMoveParsesBackFromItsOwnName()
        {
            foreach (var fen in _positions)
            {
                var game = new ChessGame(fen);

                foreach (var move in game.LegalMoves)
                {
                    Assert.True(game.TryParseMove(game.ToSan(move), out var bySan) && bySan == move,
                        $"{move} does not survive being written as {game.ToSan(move)} in {fen}");

                    Assert.True(game.TryParseMove(move.ToString(), out var byCoords) && byCoords == move,
                        $"{move} does not survive being written as coordinates in {fen}");
                }
            }
        }

        [Fact]
        public void ShufflingKnightsBackAndForthDrawsByRepetition()
        {
            var game = Play(new ChessGame(), "Nf3", "Nf6", "Ng1", "Ng8", "Nf3", "Nf6", "Ng1", "Ng8");

            Assert.Equal(ChessResultEnum.ThreefoldRepetition, game.Result);
        }

        [Fact]
        public void TheHundredthQuietPlyDraws()
        {
            var game = Play(new ChessGame("4k3/8/8/8/8/8/8/R3K3 w - - 99 60"), "Ra2");

            Assert.Equal(ChessResultEnum.FiftyMoveRule, game.Result);
        }

        [Theory]
        [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1")] // bare kings
        [InlineData("4k3/8/8/8/8/8/8/3BK3 w - - 0 1")] // king and bishop
        [InlineData("4k3/8/8/8/8/8/8/3NK3 w - - 0 1")] // king and knight
        [InlineData("5bk1/8/8/8/8/8/8/2B1K3 w - - 0 1")] // bishops on one colour
        public void TheseAreDeadDraws(string fen)
        {
            Assert.Equal(ChessResultEnum.InsufficientMaterial, new ChessGame(fen).Result);
        }

        [Theory]
        [InlineData("4k3/8/8/8/8/8/8/2NNK3 w - - 0 1")] // two knights: not forced, but not dead
        [InlineData("5bk1/8/8/8/8/8/8/3BK3 w - - 0 1")] // bishops on opposite colours
        [InlineData("4k3/8/8/8/8/8/7P/4K3 w - - 0 1")] // a pawn is always enough to keep playing
        public void TheseAreNot(string fen)
        {
            Assert.NotEqual(ChessResultEnum.InsufficientMaterial, new ChessGame(fen).Result);
        }

        [Theory]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
        [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]
        [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1")]
        [InlineData("rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2")]
        public void APositionRoundTripsThroughFen(string fen)
        {
            Assert.Equal(fen, new ChessBoard(fen).ToFen());
        }

        [Fact]
        public void CastlingRightsDieWhenTheRookIsCapturedAndWhenItMoves()
        {
            // Rxa8 kills TWO rights at once, which is what makes it the move worth testing: Black loses the
            // queen's side because the rook standing there was captured — a right lost by the side that did not
            // move, which is the clause everybody forgets — and White loses its own queen's side because that is
            // the rook that left a1. Perft catches both, but only as a number; here they are the rule, stated.
            var game = new ChessGame("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Assert.True(game.TryParseMove("Rxa8", out var capture));

            game.Play(capture);

            Assert.Equal(CastlingRightsEnum.WhiteKingSide | CastlingRightsEnum.BlackKingSide,
                game.Board.CastlingRights);
        }

        [Fact]
        public void CastlingIsWrittenTheWayEveryoneWritesIt()
        {
            var game = new ChessGame("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var names = NamesOfLegalMoves(game);

            Assert.Contains("O-O", names);
            Assert.Contains("O-O-O", names);
        }

        [Fact]
        public void PromotionOffersAllFourPiecesAndNamesThemApart()
        {
            var game = new ChessGame("8/P6k/8/8/8/8/8/7K w - - 0 1");
            var names = NamesOfLegalMoves(game);

            Assert.Contains("a8=Q", names);
            Assert.Contains("a8=R", names);
            Assert.Contains("a8=B", names);
            Assert.Contains("a8=N", names);
        }

        private static ChessGame Play(ChessGame game, params string[] moves)
        {
            foreach (var san in moves)
            {
                Assert.True(game.TryParseMove(san, out var move), $"could not parse \"{san}\"");
                game.Play(move);
            }

            return game;
        }

        private static List<string> NamesOfLegalMoves(ChessGame game)
        {
            var names = new List<string>();
            foreach (var move in game.LegalMoves)
                names.Add(game.ToSan(move));

            return names;
        }
    }
}
