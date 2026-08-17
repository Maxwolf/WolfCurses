// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Diagnostics;
using System.Globalization;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Counts the leaf nodes of the move tree to a given depth and checks the totals against published values.
    ///     <para>
    ///         <b>This is the only test of a chess move generator that is worth anything.</b> Unit tests of "a
    ///         knight on b1 has three moves" assert what the author believed the rules were, which is precisely the
    ///         thing likely to be wrong. Perft counts every leaf of the tree, so a rule that is wrong anywhere —
    ///         one castle through check, one en passant that should have been illegal because it uncovered a rook
    ///         down the rank, one under-promotion never generated — moves a number by an amount no amount of
    ///         squinting at the board would reveal. The reference counts are published and agreed on by every
    ///         engine, so a mismatch is unambiguous.
    ///     </para>
    ///     <para>
    ///         Reachable as <c>dotnet run --project example/WolfCurses.Games -- perft</c>. It lives here rather than
    ///         in the library's test project on purpose: <c>tests/WolfCurses.Tests</c> testing an example app would
    ///         point the dependency backwards, which this repository has already refused once over the StbImageSharp
    ///         adapter.
    ///     </para>
    /// </summary>
    public static class ChessPerft
    {
        /// <summary>
        ///     One position and what the tree under it is known to count. The six standard positions every engine
        ///     is checked against; between them they cover castling both ways, castling rights lost to a rook
        ///     capture, en passant including the pinned-along-the-rank case, promotion and under-promotion, and
        ///     positions where the side to move is already in check.
        /// </summary>
        private sealed class Case
        {
            public Case(string name, string fen, params long[] expected)
            {
                Name = name;
                Fen = fen;
                Expected = expected;
            }

            public string Name { get; }

            public string Fen { get; }

            /// <summary>Expected leaf counts, index 0 being depth 1.</summary>
            public long[] Expected { get; }
        }

        private static readonly Case[] _cases =
        {
            new("Initial position", ChessBoard.StartingFen,
                20, 400, 8_902, 197_281, 4_865_609),

            new("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                48, 2_039, 97_862, 4_085_603),

            new("Endgame (rank-pin en passant)", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                14, 191, 2_812, 43_238, 674_624),

            new("Promotion and pinned pieces",
                "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                6, 264, 9_467, 422_333),

            new("Mirrored, no castling",
                "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
                44, 1_486, 62_379, 2_103_487),

            new("Steven Edwards' position",
                "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
                46, 2_079, 89_890, 3_894_594)
        };

        /// <summary>
        ///     Counts the leaves of the move tree at <paramref name="depth" />.
        ///     <para>
        ///         The depth-1 shortcut — returning the move count rather than recursing into each — is the standard
        ///         formulation and is not merely an optimisation: recursing one more ply and counting one per leaf
        ///         would make every position cost a make/unmake it does not need, roughly tripling the runtime.
        ///     </para>
        /// </summary>
        /// <param name="board">The position to count from. It is left exactly as it was found.</param>
        /// <param name="depth">How many plies deep to count.</param>
        /// <returns>The number of leaf nodes.</returns>
        public static long Count(ChessBoard board, int depth)
        {
            if (depth <= 0)
                return 1;

            var moves = board.GenerateMoves();
            if (depth == 1)
                return moves.Count;

            long nodes = 0;
            foreach (var move in moves)
            {
                var undo = board.MakeMove(move);
                nodes += Count(board, depth - 1);
                board.UnmakeMove(move, undo);
            }

            return nodes;
        }

        /// <summary>
        ///     Splits the count by first move, which is how a mismatch is actually hunted down: compare this against
        ///     a known-good engine's split for the same position and the one move whose subtree differs is the bug.
        /// </summary>
        /// <param name="board">The position.</param>
        /// <param name="depth">The depth.</param>
        /// <returns>Each legal move and the nodes beneath it.</returns>
        public static (string Move, long Nodes)[] Divide(ChessBoard board, int depth)
        {
            var moves = board.GenerateMoves();
            var split = new (string, long)[moves.Count];

            for (var i = 0; i < moves.Count; i++)
            {
                var undo = board.MakeMove(moves[i]);
                split[i] = (moves[i].ToString(), Count(board, depth - 1));
                board.UnmakeMove(moves[i], undo);
            }

            Array.Sort(split, (a, b) => string.CompareOrdinal(a.Item1, b.Item1));
            return split;
        }

        /// <summary>
        ///     Runs every case and reports. Writes straight to the console because it is a command-line check, not
        ///     something drawn inside the TUI.
        /// </summary>
        /// <param name="maxDepth">Cap on how deep to go; the standard run stops at 4 and finishes in a few seconds.</param>
        /// <returns>True when every count matched.</returns>
        public static bool RunAll(int maxDepth)
        {
            var allMatched = true;
            var total = 0L;
            var clock = Stopwatch.StartNew();

            foreach (var test in _cases)
            {
                Console.WriteLine(test.Name);
                Console.WriteLine($"  {test.Fen}");

                var board = new ChessBoard(test.Fen);
                for (var depth = 1; depth <= test.Expected.Length && depth <= maxDepth; depth++)
                {
                    var started = Stopwatch.GetTimestamp();
                    var counted = Count(board, depth);
                    var elapsed = Stopwatch.GetElapsedTime(started);
                    var expected = test.Expected[depth - 1];
                    var matched = counted == expected;
                    allMatched &= matched;
                    total += counted;

                    Console.WriteLine(matched
                        ? $"    depth {depth}: {counted,12} ok      ({elapsed.TotalMilliseconds,7:F0} ms)"
                        : $"    depth {depth}: {counted,12} WRONG, expected {expected}");

                    // A FEN that does not survive the walk means make/unmake is not exactly reversible, which is a
                    // different bug from a wrong rule and would otherwise hide behind a correct-looking count.
                    if (board.ToFen() != test.Fen)
                    {
                        Console.WriteLine($"    position was not restored: {board.ToFen()}");
                        allMatched = false;
                    }
                }

                Console.WriteLine();
            }

            Console.WriteLine(allMatched
                ? $"All perft counts matched. {total:N0} nodes in {clock.Elapsed.TotalSeconds:F1}s " +
                  $"({total / Math.Max(1, clock.Elapsed.TotalSeconds) / 1000:N0}k nodes/sec)."
                : "PERFT MISMATCH - the move generator is wrong. Use Divide() against a known engine to find it.");

            return allMatched;
        }

        /// <summary>
        ///     Parses the command line the games app was started with and runs the check. Deliberately not named
        ///     <c>Main</c>: a public static <c>Main(string[])</c> is a candidate entry point, and the compiler
        ///     refuses a program that has two.
        /// </summary>
        /// <param name="args">Everything after the "perft" argument.</param>
        /// <returns>0 when every count matched, 1 otherwise.</returns>
        public static int Run(string[] args)
        {
            var depth = 4;
            if (args.Length > 0 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var requested))
                depth = Math.Clamp(requested, 1, 6);

            Console.WriteLine($"WolfChess 5000 - perft to depth {depth}");
            Console.WriteLine();
            return RunAll(depth) ? 0 : 1;
        }
    }
}
