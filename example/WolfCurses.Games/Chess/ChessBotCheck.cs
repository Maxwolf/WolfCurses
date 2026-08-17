// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Diagnostics;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Checks that the bot plays chess and fits inside a frame.
    ///     <para>
    ///         The tests are written so they never have to name the right move, which would only assert what the
    ///         author believed the right move was: a mate-in-one is checked by playing whatever the bot chose and
    ///         asking the <i>rules</i> whether the game is now over. That makes these tests about the bot rather
    ///         than about the author. Reachable as
    ///         <c>dotnet run --project example/WolfCurses.Games -- bot</c>.
    ///     </para>
    /// </summary>
    public static class ChessBotCheck
    {
        /// <summary>Thinks to completion, the way the dialog does, and hands back what it chose.</summary>
        /// <param name="bot">The bot to ask.</param>
        /// <param name="game">The position.</param>
        /// <param name="slice">The per-tick budget to hand it, so the slicing itself is exercised.</param>
        /// <returns>The move and how long the whole think took.</returns>
        public static (ChessMove Move, TimeSpan Elapsed, int Slices) Decide(IChessBot bot, ChessGame game,
            TimeSpan slice)
        {
            var clock = Stopwatch.StartNew();
            var slices = 0;

            bot.Begin(game);
            while (!bot.Think(slice))
            {
                slices++;
                if (slices > 10_000)
                    throw new InvalidOperationException("The bot never finished thinking.");
            }

            return (bot.BestMove, clock.Elapsed, slices + 1);
        }

        /// <summary>Runs every check and reports.</summary>
        /// <returns>True when everything held.</returns>
        public static bool RunAll()
        {
            var ok = true;
            var slice = TimeSpan.FromMilliseconds(25);

            ok &= Check("It finds mate in one", () =>
            {
                foreach (var fen in new[]
                         {
                             "k7/7R/1K6/8/8/8/8/8 w - - 0 1",
                             "6k1/8/6K1/8/8/8/8/1Q6 w - - 0 1",
                             "6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1"
                         })
                {
                    var game = new ChessGame(fen);
                    var (move, _, _) = Decide(new WolfChessBot(), game, slice);
                    if (!move.IsValid)
                        return $"no move chosen in {fen}";

                    game.Play(move);
                    if (game.Result != ChessResultEnum.Checkmate)
                        return $"{move} in {fen} gave {game.Result}, not mate";
                }

                return null;
            });

            ok &= Check("It takes a free queen", () =>
            {
                var game = new ChessGame("4k3/8/8/3q4/8/8/8/3RK3 w - - 0 1");
                var (move, _, _) = Decide(new WolfChessBot(), game, slice);

                if (!move.IsValid)
                    return "no move chosen";

                var target = game.Board[move.To];
                return target.Kind == PieceKindEnum.Queen ? null : $"played {game.ToSan(move)} instead of taking it";
            });

            ok &= Check("It does not hang its own queen", () =>
            {
                // A queen may capture the pawn on b7, but the rook on b8 would take it. A bot with no quiescence
                // search evaluates the capture as a free pawn and plays it.
                var game = new ChessGame("1r2k3/1p6/8/8/8/8/8/3QK3 w - - 0 1");
                var (move, _, _) = Decide(new WolfChessBot(), game, slice);

                if (!move.IsValid)
                    return "no move chosen";

                return ChessBoard.SquareName(move.To) == "b7" && game.Board[move.From].Kind == PieceKindEnum.Queen
                    ? "played Qxb7 and loses the queen to Rxb7"
                    : null;
            });

            ok &= Check("Every move it plays is legal, over a whole self-played game", () =>
            {
                var game = new ChessGame();
                var white = new WolfChessBot(3);
                var black = new WolfChessBot(3);

                for (var ply = 0; ply < 60 && !game.IsOver; ply++)
                {
                    var bot = game.Board.SideToMove == PieceColorEnum.White ? white : black;
                    var (move, _, _) = Decide(bot, game, slice);

                    if (!move.IsValid)
                        return $"no move chosen at ply {ply}";

                    var legal = false;
                    foreach (var candidate in game.LegalMoves)
                    {
                        if (candidate != move)
                            continue;

                        legal = true;
                        break;
                    }

                    if (!legal)
                        return $"illegal move {move} at ply {ply} in {game.Board.ToFen()}";

                    game.Play(move);
                }

                Console.WriteLine($"        self-play reached ply {game.Notation.Count}, result {game.Result}");
                return null;
            });

            ok &= Check("It mates a lone king", () =>
            {
                // The test that caught the bot being unable to win a won game. Material and the piece-square
                // tables score every king-and-queen-versus-king position the same, so before the mop-up term all
                // four of these ended in threefold repetition - the bot shuffled while a queen up.
                foreach (var (name, fen) in new[]
                         {
                             ("king and queen", "4k3/8/8/8/8/8/8/3QK3 w - - 0 1"),
                             ("king and rook", "4k3/8/8/8/8/8/8/3RK3 w - - 0 1"),
                             ("king and two rooks", "4k3/8/8/8/8/8/8/R3K2R w - - 0 1")
                         })
                {
                    var game = new ChessGame(fen);
                    var bots = new IChessBot[] {new WolfChessBot(4), new WolfChessBot(4)};

                    for (var ply = 0; ply < 80 && !game.IsOver; ply++)
                    {
                        var (move, _, _) = Decide(bots[(int) game.Board.SideToMove], game, slice);
                        if (!move.IsValid)
                            return $"{name}: no move at ply {ply}";

                        game.Play(move);
                    }

                    if (game.Result != ChessResultEnum.Checkmate)
                        return $"{name}: ended in {game.Result} after {game.Notation.Count} plies, not mate";

                    Console.WriteLine($"        {name}: mate in {game.Notation.Count} plies");
                }

                return null;
            });

            ok &= Check("The slice size does not change the move it plays", () =>
            {
                // The property that makes slicing safe to do at all: alpha is carried across ticks and the root
                // order is fixed, so a search chopped into forty pieces prunes identically to one run straight
                // through, and therefore answers identically. If this ever fails, the search is not resumable and
                // the bot's move depends on how busy the machine was.
                foreach (var fen in new[]
                         {
                             ChessBoard.StartingFen,
                             "r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4",
                             "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1"
                         })
                {
                    var game = new ChessGame(fen);
                    var reference = ChessMove.None;

                    foreach (var milliseconds in new[] {1, 5, 15, 40, 5_000})
                    {
                        var bot = new WolfChessBot(4);
                        var (move, _, _) = Decide(bot, game, TimeSpan.FromMilliseconds(milliseconds));

                        if (!reference.IsValid)
                            reference = move;
                        else if (move != reference)
                            return $"{fen}: a {milliseconds}ms slice chose {move}, not {reference}";
                    }
                }

                return null;
            });

            ok &= Check("Thinking time is known at every depth", () =>
            {
                // The number that decides the default difficulty. A search cannot be paused mid-recursion, so
                // whatever one iteration costs is a pause the screen takes in one piece - which is fine for a
                // turn-based game that has drawn "thinking..." first, and not fine if it runs to seconds.
                foreach (var fen in new[]
                         {
                             "r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4",
                             "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1"
                         })
                {
                    Console.WriteLine($"        {fen}");
                    for (var depth = 2; depth <= 5; depth++)
                    {
                        var game = new ChessGame(fen);
                        var bot = new WolfChessBot(depth);
                        var (move, elapsed, _) = Decide(bot, game, slice);

                        if (!move.IsValid)
                            return $"no move chosen at depth {depth}";

                        Console.WriteLine($"          depth {depth}: {bot.NodesSearched,9:N0} nodes  " +
                                          $"{elapsed.TotalMilliseconds,7:F0} ms   {game.ToSan(move)}");
                    }
                }

                return null;
            });

            Console.WriteLine();
            Console.WriteLine(ok ? "All bot checks passed." : "BOT CHECK FAILED.");
            return ok;
        }

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

        /// <summary>Entry point for the <c>bot</c> command line argument.</summary>
        /// <returns>0 when everything held, 1 otherwise.</returns>
        public static int Run()
        {
            Console.WriteLine("WolfChess 5000 - bot check");
            Console.WriteLine();
            return RunAll() ? 0 : 1;
        }
    }
}
