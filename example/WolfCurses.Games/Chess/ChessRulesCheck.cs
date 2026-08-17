// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Checks the parts of the rules that <see cref="ChessPerft" /> cannot see.
    ///     <para>
    ///         Perft counts moves, so it proves the generator exactly and proves nothing else: a game that never
    ///         noticed checkmate, wrote every move as "Nd2", or called a bare-kings ending a win would pass every
    ///         perft in the world. These are the other half — the notation, the result, and the three draws that
    ///         are not stalemate. Reachable as <c>dotnet run --project example/WolfCurses.Games -- rules</c>.
    ///     </para>
    /// </summary>
    public static class ChessRulesCheck
    {
        /// <summary>Runs every check and reports.</summary>
        /// <returns>True when everything held.</returns>
        public static bool RunAll()
        {
            var ok = true;

            ok &= Check("Scholar's mate is checkmate, White wins", () =>
            {
                var game = new ChessGame();
                foreach (var san in new[] {"e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7"})
                {
                    if (!game.TryParseMove(san, out var move))
                        return $"could not parse \"{san}\"";

                    game.Play(move);
                }

                if (game.Result != ChessResultEnum.Checkmate)
                    return $"result was {game.Result}";
                if (game.Winner != PieceColorEnum.White)
                    return $"winner was {game.Winner}";

                return game.Notation[^1] == "Qxf7#" ? null : $"last move written as {game.Notation[^1]}";
            });

            ok &= Check("A king with nowhere to go and no check is stalemate", () =>
            {
                var game = new ChessGame("5k2/5P2/5K2/8/8/8/8/8 b - - 0 1");
                return game.Result == ChessResultEnum.Stalemate ? null : $"result was {game.Result}";
            });

            ok &= Check("Two knights reaching one square are disambiguated by file", () =>
            {
                var game = new ChessGame("4k3/8/8/8/8/5N2/8/1N2K3 w - - 0 1");
                var names = new List<string>();
                foreach (var move in game.LegalMoves)
                    names.Add(game.ToSan(move));

                if (!names.Contains("Nbd2"))
                    return "Nbd2 was not offered";

                return names.Contains("Nfd2") ? null : "Nfd2 was not offered";
            });

            ok &= Check("Every move in a position has a different name", () =>
            {
                // The direct test of disambiguation, and stronger than any single case: if two legal moves are
                // written the same way, the notation is lossy and one of them is unplayable by name.
                foreach (var fen in new[]
                         {
                             ChessBoard.StartingFen,
                             "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                             "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                             "4k3/8/8/8/8/8/8/QQ2K2Q w - - 0 1"
                         })
                {
                    var game = new ChessGame(fen);
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var move in game.LegalMoves)
                    {
                        var san = game.ToSan(move);
                        if (!seen.Add(san))
                            return $"\"{san}\" names two different moves in {fen}";
                    }
                }

                return null;
            });

            ok &= Check("Every move parses back to itself from its own name", () =>
            {
                foreach (var fen in new[]
                         {
                             ChessBoard.StartingFen,
                             "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                             "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                             "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1"
                         })
                {
                    var game = new ChessGame(fen);
                    foreach (var move in game.LegalMoves)
                    {
                        if (!game.TryParseMove(game.ToSan(move), out var byName) || byName != move)
                            return $"{move} does not survive being written as {game.ToSan(move)} in {fen}";

                        if (!game.TryParseMove(move.ToString(), out var byCoords) || byCoords != move)
                            return $"{move} does not survive being written as coordinates in {fen}";
                    }
                }

                return null;
            });

            ok &= Check("Shuffling knights back and forth draws by repetition", () =>
            {
                var game = new ChessGame();
                foreach (var san in new[] {"Nf3", "Nf6", "Ng1", "Ng8", "Nf3", "Nf6", "Ng1", "Ng8"})
                {
                    if (!game.TryParseMove(san, out var move))
                        return $"could not parse \"{san}\"";

                    game.Play(move);
                }

                return game.Result == ChessResultEnum.ThreefoldRepetition ? null : $"result was {game.Result}";
            });

            ok &= Check("The hundredth quiet ply draws", () =>
            {
                var game = new ChessGame("4k3/8/8/8/8/8/8/R3K3 w - - 99 60");
                if (!game.TryParseMove("Ra2", out var move))
                    return "could not parse \"Ra2\"";

                game.Play(move);
                return game.Result == ChessResultEnum.FiftyMoveRule ? null : $"result was {game.Result}";
            });

            ok &= Check("Insufficient material is exactly the four drawn endings", () =>
            {
                var drawn = new[]
                {
                    "4k3/8/8/8/8/8/8/4K3 w - - 0 1", // bare kings
                    "4k3/8/8/8/8/8/8/3BK3 w - - 0 1", // king and bishop
                    "4k3/8/8/8/8/8/8/3NK3 w - - 0 1", // king and knight
                    "5bk1/8/8/8/8/8/8/2B1K3 w - - 0 1" // bishops on squares of one colour
                };

                foreach (var fen in drawn)
                {
                    var game = new ChessGame(fen);
                    if (game.Result != ChessResultEnum.InsufficientMaterial)
                        return $"{fen} was called {game.Result}";
                }

                var playable = new[]
                {
                    "4k3/8/8/8/8/8/8/2NNK3 w - - 0 1", // two knights: not forced, but not dead
                    "5bk1/8/8/8/8/8/8/3BK3 w - - 0 1", // bishops on opposite colours
                    "4k3/8/8/8/8/8/7P/4K3 w - - 0 1" // a pawn is always enough to keep playing
                };

                foreach (var fen in playable)
                {
                    var game = new ChessGame(fen);
                    if (game.Result == ChessResultEnum.InsufficientMaterial)
                        return $"{fen} was wrongly called a dead draw";
                }

                return null;
            });

            ok &= Check("A position round-trips through FEN", () =>
            {
                foreach (var fen in new[]
                         {
                             ChessBoard.StartingFen,
                             "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                             "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                             "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2"
                         })
                {
                    var written = new ChessBoard(fen).ToFen();
                    if (!string.Equals(written, fen, StringComparison.Ordinal))
                        return $"{fen} came back as {written}";
                }

                return null;
            });

            Console.WriteLine();
            Console.WriteLine(ok ? "All rules checks passed." : "RULES CHECK FAILED.");
            return ok;
        }

        /// <summary>Runs one check, printing what it found. The check returns null when it held, or what went wrong.</summary>
        private static bool Check(string name, Func<string> check)
        {
            string failure;
            try
            {
                failure = check();
            }
            catch (Exception ex)
            {
                failure = $"threw {ex.GetType().Name}: {ex.Message}";
            }

            Console.WriteLine(failure == null ? $"  ok    {name}" : $"  FAIL  {name} - {failure}");
            return failure == null;
        }

        /// <summary>Entry point for the <c>rules</c> command line argument.</summary>
        /// <returns>0 when everything held, 1 otherwise.</returns>
        public static int Run()
        {
            Console.WriteLine("WolfChess 5000 - rules check");
            Console.WriteLine();
            return RunAll() ? 0 : 1;
        }
    }
}
