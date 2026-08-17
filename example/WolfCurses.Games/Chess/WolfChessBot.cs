// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     The built-in opponent: alpha-beta with iterative deepening, a quiescence search, and the published
    ///     Simplified Evaluation Function. Not strong — a few hundred thousand positions a move against an engine's
    ///     tens of millions — but it does not hang pieces, it takes yours, and it sees a two-move tactic, which is
    ///     the whole brief.
    ///     <para>
    ///         <b>The evaluation is somebody else's on purpose.</b> Piece values plus a table of per-square bonuses
    ///         per piece is Tomasz Michniewski's Simplified Evaluation Function, published on the Chess Programming
    ///         Wiki and used as the starting point by approximately every hobby engine ever written. Inventing
    ///         numbers here would have produced a bot with opinions nobody could check; these are the reference
    ///         ones, so the tables below can be diffed against the source.
    ///     </para>
    ///     <para>
    ///         <b>Alpha-beta is a shortcut, not an approximation.</b> It returns exactly what searching every branch
    ///         would return, having skipped branches that provably cannot change the answer, and it typically looks
    ///         at the square root of the nodes when the moves are well ordered — which is why the move ordering
    ///         below (captures first, most-valuable victim taken by least-valuable attacker) is not a nicety but
    ///         most of the speed. <b>Quiescence</b> is the other half: stopping a search on a fixed depth in the
    ///         middle of an exchange makes the bot believe it has won a queen when the recapture was one ply past
    ///         the horizon, so the leaves keep searching captures until the position is quiet.
    ///     </para>
    /// </summary>
    public sealed class WolfChessBot : IChessBot
    {
        /// <summary>What a checkmate scores. Far outside any material evaluation, and short of int.MaxValue so it can be negated.</summary>
        private const int MateScore = 1_000_000;

        /// <summary>Returned by an abandoned search; the caller throws the whole iteration away rather than trusting it.</summary>
        private const int Aborted = int.MinValue + 1;

        private static readonly int[] _pieceValues = {0, 100, 320, 330, 500, 900, 20_000};

        // The Simplified Evaluation Function's tables, written rank 8 first so they read like a board. Index
        // [0] is a8 and [63] is h1, which is the printing order and the reverse of this codebase's square
        // numbering - TableValue does that flip, and doing it there rather than rewriting the tables is deliberate:
        // these are meant to stay diffable against the published source.
        private static readonly int[] _pawnTable =
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
            5, 5, 10, 25, 25, 10, 5, 5,
            0, 0, 0, 20, 20, 0, 0, 0,
            5, -5, -10, 0, 0, -10, -5, 5,
            5, 10, 10, -20, -20, 10, 10, 5,
            0, 0, 0, 0, 0, 0, 0, 0
        };

        private static readonly int[] _knightTable =
        {
            -50, -40, -30, -30, -30, -30, -40, -50,
            -40, -20, 0, 0, 0, 0, -20, -40,
            -30, 0, 10, 15, 15, 10, 0, -30,
            -30, 5, 15, 20, 20, 15, 5, -30,
            -30, 0, 15, 20, 20, 15, 0, -30,
            -30, 5, 10, 15, 15, 10, 5, -30,
            -40, -20, 0, 5, 5, 0, -20, -40,
            -50, -40, -30, -30, -30, -30, -40, -50
        };

        private static readonly int[] _bishopTable =
        {
            -20, -10, -10, -10, -10, -10, -10, -20,
            -10, 0, 0, 0, 0, 0, 0, -10,
            -10, 0, 5, 10, 10, 5, 0, -10,
            -10, 5, 5, 10, 10, 5, 5, -10,
            -10, 0, 10, 10, 10, 10, 0, -10,
            -10, 10, 10, 10, 10, 10, 10, -10,
            -10, 5, 0, 0, 0, 0, 5, -10,
            -20, -10, -10, -10, -10, -10, -10, -20
        };

        private static readonly int[] _rookTable =
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            5, 10, 10, 10, 10, 10, 10, 5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            0, 0, 0, 5, 5, 0, 0, 0
        };

        private static readonly int[] _queenTable =
        {
            -20, -10, -10, -5, -5, -10, -10, -20,
            -10, 0, 0, 0, 0, 0, 0, -10,
            -10, 0, 5, 5, 5, 5, 0, -10,
            -5, 0, 5, 5, 5, 5, 0, -5,
            0, 0, 5, 5, 5, 5, 0, -5,
            -10, 5, 5, 5, 5, 5, 0, -10,
            -10, 0, 5, 0, 0, 0, 0, -10,
            -20, -10, -10, -5, -5, -10, -10, -20
        };

        private static readonly int[] _kingMiddlegameTable =
        {
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -20, -30, -30, -40, -40, -30, -30, -20,
            -10, -20, -20, -20, -20, -20, -20, -10,
            20, 20, 0, 0, 0, 0, 20, 20,
            20, 30, 10, 0, 0, 10, 30, 20
        };

        /// <summary>How long a whole move may take before the search gives up on going deeper.</summary>
        private static readonly TimeSpan _hardLimit = TimeSpan.FromSeconds(3);

        /// <summary>
        ///     Michniewski's other king table, for when the queens and rooks are gone. In the middlegame a king
        ///     wants to be hiding in a corner behind its pawns; in an endgame the same square is the worst on the
        ///     board and it wants the middle.
        /// </summary>
        private static readonly int[] _kingEndgameTable =
        {
            -50, -40, -30, -20, -20, -30, -40, -50,
            -30, -20, -10, 0, 0, -10, -20, -30,
            -30, -10, 20, 30, 30, 20, -10, -30,
            -30, -10, 30, 40, 40, 30, -10, -30,
            -30, -10, 30, 40, 40, 30, -10, -30,
            -30, -10, 20, 30, 30, 20, -10, -30,
            -30, -30, 0, 0, 0, 0, -30, -30,
            -50, -30, -30, -30, -30, -30, -30, -50
        };

        private readonly Stopwatch _slice = new();

        /// <summary>Runs for the whole move rather than the slice; the only thing that can abort an iteration.</summary>
        private readonly Stopwatch _wholeMove = new();

        private ChessBoard _board;
        private ChessMove _bestThisIteration;
        private bool _aborted;

        /// <summary>
        ///     The root move list, and the cursor into it. Non-null means still thinking.
        ///     <para>
        ///         <b>This is what makes the search resumable, and the root list is the only place it could be.</b>
        ///         A recursive search cannot be parked mid-recursion in C# without turning it into a state machine;
        ///         the loop over the root moves is the one linear thing in it, so that is where the search stops and
        ///         picks up again next tick. <see cref="_rootAlpha" /> carries across ticks so the sliced search
        ///         prunes exactly as the same loop run straight through would, and therefore returns exactly the
        ///         same move — which is a property worth testing rather than assuming.
        ///     </para>
        /// </summary>
        private ChessMove[] _rootMoves;

        private int _rootIndex;
        private int _rootAlpha;
        private int _depth;

        /// <summary>Initializes a new instance of the <see cref="WolfChessBot" /> class.</summary>
        /// <param name="maxDepth">
        ///     How deep it is allowed to go, and the only difficulty knob there is.
        ///     <para>
        ///         Three by default, which is chosen for the <i>worst</i> case rather than the best: measured on
        ///         one middlegame position, a move costs about 5,000 nodes and 8ms at depth 3, 58,000 and 130ms at
        ///         depth 4, and 426,000 and 722ms at depth 5 — <b>in a Release build</b>. Debug is roughly seven
        ///         times slower, and <c>dotnet run</c> gives you Debug, so the depth that feels instant while
        ///         developing is a second-long stall for anyone who tries it that way. Depth 3 already sees a
        ///         hanging piece and a one-move tactic; 4 and 5 are offered to the player as the harder settings.
        ///     </para>
        /// </param>
        public WolfChessBot(int maxDepth = 3)
        {
            MaxDepth = Math.Clamp(maxDepth, 1, 8);
        }

        /// <summary>How deep the search may go.</summary>
        public int MaxDepth { get; }

        /// <inheritdoc />
        public string Name => $"WolfChess 5000 (depth {MaxDepth})";

        /// <inheritdoc />
        public ChessMove BestMove { get; private set; }

        /// <inheritdoc />
        public int CompletedDepth { get; private set; }

        /// <inheritdoc />
        public long NodesSearched { get; private set; }

        /// <inheritdoc />
        public void Begin(ChessGame game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            // A copy, because the search makes and unmakes millions of moves and the caller is drawing the real
            // board from another thread of control (the tick loop) between slices.
            // Cloned once per move, never per node: Clone goes through FEN and costs microseconds, which is
            // nothing once and ruinous a million times.
            _board = game.Board.Clone();
            BestMove = ChessMove.None;
            CompletedDepth = 0;
            NodesSearched = 0;
            LastScore = 0;
            _aborted = false;

            // Reset, not Restart: the deadline counts time actually spent thinking, accumulated by Think. A wall
            // clock started here would burn the budget while the form sat under a dialog searching nothing, and
            // then hand back no move at all.
            _wholeMove.Reset();

            var moves = game.LegalMoves;
            _rootMoves = new ChessMove[moves.Count];
            for (var i = 0; i < moves.Count; i++)
                _rootMoves[i] = moves[i];

            if (_rootMoves.Length == 0)
            {
                _rootMoves = null;
                return;
            }

            var ordered = new List<ChessMove>(_rootMoves);
            OrderMoves(ordered);
            ordered.CopyTo(_rootMoves);

            BeginIteration(1);
        }

        /// <summary>Starts a deepening rung from the top of the root list.</summary>
        private void BeginIteration(int depth)
        {
            _depth = depth;
            _rootIndex = 0;
            _rootAlpha = -MateScore * 2;
            _bestThisIteration = ChessMove.None;
        }

        /// <summary>How far through the current deepening rung the search is, 0 to 1, for a progress readout.</summary>
        public double Progress => _rootMoves == null || _rootMoves.Length == 0
            ? 1.0
            : (double) _rootIndex / _rootMoves.Length;

        /// <inheritdoc />
        public bool Think(TimeSpan budget)
        {
            if (_rootMoves == null)
                return true;

            _wholeMove.Start();
            _slice.Restart();

            // A do-while, so EVERY CALL FINISHES AT LEAST ONE UNIT OF WORK and the search cannot livelock - the
            // budget decides how many MORE units to start, never whether to finish the one in hand, which is the
            // same bargain AnimatedGifDialog strikes with its frames.
            //
            // The unit is ONE ROOT MOVE, not one whole deepening iteration. That distinction is the difference
            // between a game that answers the keyboard and one that does not: an iteration is unbounded (measured
            // up to 170ms Release and 1.2s Debug on a quiet position at depth 3), and while a Think call is inside
            // one, InputManager reads no keys at all - so ESC does nothing and a held arrow piles a backlog into
            // the console buffer that the library's while-drain then spends in a single tick. A root move is small
            // and there are dozens of them, so the slice is honoured to within one of them.
            do
            {
                SearchOneRootMove();

                if (_aborted)
                {
                    // The hard deadline, which means a pathological position. Keep whatever depth did finish.
                    _rootMoves = null;
                    _wholeMove.Stop();
                    return true;
                }

                if (_rootIndex < _rootMoves.Length)
                    continue;

                CompletedDepth = _depth;
                LastScore = _rootAlpha;
                if (_bestThisIteration.IsValid)
                    BestMove = _bestThisIteration;

                if (_depth >= MaxDepth || Math.Abs(_rootAlpha) > MateScore - 1000)
                {
                    _rootMoves = null;
                    _wholeMove.Stop();
                    return true;
                }

                // The completed rung's best move goes first in the next one. Free, and not a micro-optimisation:
                // alpha-beta prunes on the strength of its first move, and MVV-LVA ordering has nothing to say
                // about a quiet position where no move captures anything.
                PromoteBestToFront();
                BeginIteration(_depth + 1);
            }
            while (_slice.Elapsed < budget);

            _wholeMove.Stop();
            return false;
        }

        /// <summary>Searches the one root move the cursor is on and advances it.</summary>
        private void SearchOneRootMove()
        {
            var move = _rootMoves[_rootIndex++];

            var undo = _board.MakeMove(move);
            var score = -Search(_depth - 1, -MateScore * 2, -_rootAlpha, 1);
            _board.UnmakeMove(move, undo);

            if (_aborted || score <= _rootAlpha)
                return;

            _rootAlpha = score;
            _bestThisIteration = move;
        }

        /// <summary>Moves the best move found so far to the front of the root list.</summary>
        private void PromoteBestToFront()
        {
            if (!_bestThisIteration.IsValid)
                return;

            for (var i = 0; i < _rootMoves.Length; i++)
            {
                if (_rootMoves[i] != _bestThisIteration)
                    continue;

                (_rootMoves[0], _rootMoves[i]) = (_rootMoves[i], _rootMoves[0]);
                return;
            }
        }

        /// <summary>The score of the last completed iteration, from the bot's point of view, in centipawns.</summary>
        public int LastScore { get; private set; }

        /// <summary>
        ///     Negamax with alpha-beta. Every score is from the point of view of the side to move, which is what
        ///     lets one function serve both sides — the caller negates on the way back up.
        /// </summary>
        private int Search(int depth, int alpha, int beta, int ply)
        {
            // The hard limit, not the slice budget: an iteration that has started is always allowed to finish, and
            // this exists only so a pathological position cannot think forever. Checked one node in a thousand,
            // because the clock read would otherwise cost more than the node.
            if (_aborted || ((NodesSearched & 1023) == 0 && _wholeMove.Elapsed > _hardLimit))
            {
                _aborted = true;
                return Aborted;
            }

            if (depth <= 0)
                return Quiesce(alpha, beta, ply);

            NodesSearched++;

            var moves = _board.GenerateMoves();
            if (moves.Count == 0)
            {
                // Mate is scored by PLY - how far from the root it is - so a mate in one beats a mate in three and
                // the bot delivers it instead of announcing it and shuffling. Scoring by remaining depth instead
                // gives the same mate a different score at every deepening rung, which makes the number
                // meaningless to anything outside the search.
                return _board.IsInCheck(_board.SideToMove) ? -MateScore + ply : 0;
            }

            OrderMoves(moves);

            foreach (var move in moves)
            {
                var undo = _board.MakeMove(move);
                var score = -Search(depth - 1, -beta, -alpha, ply + 1);
                _board.UnmakeMove(move, undo);

                // A flag rather than a sentinel score: the score is negated on the way back up, so a sentinel has
                // to be recognised in both signs and one of them collides with a real extreme.
                if (_aborted)
                    return Aborted;

                if (score >= beta)
                    return beta;

                if (score <= alpha)
                    continue;

                alpha = score;
            }

            return alpha;
        }

        /// <summary>
        ///     Keeps searching captures once the depth runs out, so the evaluation is never taken in the middle of
        ///     an exchange. Without this the bot happily grabs a defended pawn with its queen, because the recapture
        ///     was one ply beyond where it stopped looking.
        /// </summary>
        private int Quiesce(int alpha, int beta, int ply)
        {
            NodesSearched++;

            // The deadline is checked here too. Without it a wild position can spend the whole of a supposedly
            // bounded search inside quiescence, where the abort has no say - which makes the limit advisory in
            // exactly the positions it exists for.
            if (_aborted || ((NodesSearched & 1023) == 0 && _wholeMove.Elapsed > _hardLimit))
            {
                _aborted = true;
                return Aborted;
            }

            // "Standing pat": the side to move is not obliged to capture, so the static evaluation is a floor.
            var standingPat = Evaluate();
            if (standingPat >= beta)
                return beta;

            if (standingPat > alpha)
                alpha = standingPat;

            var captures = new List<ChessMove>();
            foreach (var move in _board.GenerateMoves())
            {
                if (move.IsEnPassant || !_board[move.To].IsEmpty)
                    captures.Add(move);
            }

            OrderMoves(captures);

            foreach (var move in captures)
            {
                var undo = _board.MakeMove(move);
                var score = -Quiesce(-beta, -alpha, ply + 1);
                _board.UnmakeMove(move, undo);

                if (score >= beta)
                    return beta;

                if (score > alpha)
                    alpha = score;
            }

            return alpha;
        }

        /// <summary>
        ///     Sorts captures to the front, most valuable victim first and cheapest attacker among equals. Ordering
        ///     is most of what makes alpha-beta fast: a cut-off found on the first move prunes the whole subtree,
        ///     and the move most likely to cause one is almost always "take the biggest thing".
        /// </summary>
        private void OrderMoves(List<ChessMove> moves)
        {
            var board = _board;
            moves.Sort((a, b) => ScoreFor(b).CompareTo(ScoreFor(a)));
            return;

            int ScoreFor(ChessMove move)
            {
                var victim = move.IsEnPassant ? PieceKindEnum.Pawn : board[move.To].Kind;
                if (victim == PieceKindEnum.None)
                    return move.Promotion != PieceKindEnum.None ? _pieceValues[(int) move.Promotion] : 0;

                return _pieceValues[(int) victim] * 10 - _pieceValues[(int) board[move.From].Kind];
            }
        }

        /// <summary>
        ///     Material plus position, from the point of view of the side to move. Positive means the side to move
        ///     is better off.
        /// </summary>
        private int Evaluate()
        {
            var score = 0;
            var heavyMaterial = 0;
            var whiteExtras = 0;
            var blackExtras = 0;

            for (var square = 0; square < 64; square++)
            {
                var piece = _board[square];
                if (piece.IsEmpty)
                    continue;

                if (piece.Kind != PieceKindEnum.King && piece.Kind != PieceKindEnum.Pawn)
                    heavyMaterial += _pieceValues[(int) piece.Kind];

                if (piece.Kind != PieceKindEnum.King)
                {
                    if (piece.Color == PieceColorEnum.White)
                        whiteExtras++;
                    else
                        blackExtras++;
                }
            }

            var endgame = heavyMaterial <= 1300;

            for (var square = 0; square < 64; square++)
            {
                var piece = _board[square];
                if (piece.IsEmpty)
                    continue;

                var value = _pieceValues[(int) piece.Kind] + TableValue(piece, square, endgame);
                score += piece.Color == PieceColorEnum.White ? value : -value;
            }

            score += MopUp(whiteExtras, blackExtras);
            return _board.SideToMove == PieceColorEnum.White ? score : -score;
        }

        /// <summary>
        ///     Basic mating technique, as an evaluation term: drive the bare king to the edge and walk your own king
        ///     toward it.
        ///     <para>
        ///         <b>Without this the bot cannot win a won game.</b> Material and piece-square tables score every
        ///         king-and-queen-versus-king position identically, so nothing distinguishes the move that mates
        ///         from the move that shuffles, and the search — which sees only a few plies and cannot find mate
        ///         from the middle of the board — picks a shuffle. Measured before adding this: king and queen,
        ///         king and rook, king and two rooks, and king and pawn against a lone king ALL ended in threefold
        ///         repetition. Two terms and fifteen lines turn all four into mates.
        ///     </para>
        /// </summary>
        /// <param name="whiteExtras">How many non-king pieces White has.</param>
        /// <param name="blackExtras">How many non-king pieces Black has.</param>
        /// <returns>The bonus, from White's point of view.</returns>
        private int MopUp(int whiteExtras, int blackExtras)
        {
            // Only when one side is down to a bare king; anywhere else this would be noise pushing kings around
            // for no reason.
            if (whiteExtras > 0 == blackExtras > 0)
                return 0;

            var winner = whiteExtras > 0 ? PieceColorEnum.White : PieceColorEnum.Black;
            var loser = ChessBoard.Opponent(winner);
            var loserKing = _board.KingSquare(loser);
            var winnerKing = _board.KingSquare(winner);

            var loserFile = ChessBoard.FileOf(loserKing);
            var loserRank = ChessBoard.RankOf(loserKing);

            // How far the bare king is from the centre, and how close the other king has come. The second term is
            // what stops the winning side from checking forever from a distance.
            var fromCentre = Math.Max(Math.Abs(3 - loserFile), Math.Abs(3 - loserRank)) +
                             Math.Max(Math.Abs(4 - loserFile), Math.Abs(4 - loserRank));
            var between = Math.Abs(loserFile - ChessBoard.FileOf(winnerKing)) +
                          Math.Abs(loserRank - ChessBoard.RankOf(winnerKing));

            var bonus = fromCentre * 10 + (14 - between) * 4;
            return winner == PieceColorEnum.White ? bonus : -bonus;
        }

        /// <summary>
        ///     What a piece is worth for standing on that square. The tables are written from White's side and read
        ///     top-left-first, so White flips the rank to index them and Black does not — which looks backwards and
        ///     is not: this codebase numbers a1 as 0, and the tables start at a8.
        /// </summary>
        private static int TableValue(Piece piece, int square, bool endgame)
        {
            var table = piece.Kind switch
            {
                PieceKindEnum.Pawn => _pawnTable,
                PieceKindEnum.Knight => _knightTable,
                PieceKindEnum.Bishop => _bishopTable,
                PieceKindEnum.Rook => _rookTable,
                PieceKindEnum.Queen => _queenTable,
                PieceKindEnum.King => endgame ? _kingEndgameTable : _kingMiddlegameTable,
                _ => null
            };

            if (table == null)
                return 0;

            var file = ChessBoard.FileOf(square);
            var rank = ChessBoard.RankOf(square);
            var row = piece.Color == PieceColorEnum.White ? 7 - rank : rank;
            return table[row * 8 + file];
        }
    }
}
