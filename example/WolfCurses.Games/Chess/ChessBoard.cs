// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     A chess position, and the rules for getting from one to the next. No screen, no bot, no history — just
    ///     the board, so it can be walked by a test at a few million positions a second and checked against
    ///     published numbers.
    ///     <para>
    ///         <b>Squares are 0-63 with a1 = 0 and h8 = 63</b>, so <c>rank * 8 + file</c>. A plain sixty-four
    ///         element array rather than the 0x88 or bitboard tricks a real engine would use: this is an example,
    ///         the bounds checks are written out where they happen, and it is fast enough to search four ply inside
    ///         a frame.
    ///     </para>
    ///     <para>
    ///         <b>Move generation is pseudo-legal, then filtered.</b> Every move a piece could make ignoring check
    ///         is generated, then each is made, tested for "did that leave my own king attacked", and unmade. A real
    ///         engine computes pins and generates only legal moves; that is faster and much easier to get subtly
    ///         wrong, and being subtly wrong here is invisible until a perft count is off by eleven at depth four.
    ///         The filter cannot be subtly wrong: it either left the king attacked or it did not.
    ///     </para>
    /// </summary>
    public sealed class ChessBoard
    {
        /// <summary>The opening position, in Forsyth-Edwards Notation.</summary>
        public const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        private static readonly (int File, int Rank)[] _knightSteps =
        {
            (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)
        };

        private static readonly (int File, int Rank)[] _kingSteps =
        {
            (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1)
        };

        private static readonly (int File, int Rank)[] _bishopRays = {(1, 1), (1, -1), (-1, -1), (-1, 1)};

        private static readonly (int File, int Rank)[] _rookRays = {(0, 1), (1, 0), (0, -1), (-1, 0)};

        /// <summary>The two files either side of a square: where a pawn captures to, and where one captures from.</summary>
        private static readonly int[] _sideSteps = {-1, 1};

        /// <summary>What promotions are offered, best first — the order also decides what a lazy caller gets.</summary>
        private static readonly PieceKindEnum[] _promotionChoices =
            {PieceKindEnum.Queen, PieceKindEnum.Rook, PieceKindEnum.Bishop, PieceKindEnum.Knight};

        private readonly Piece[] _squares = new Piece[64];

        /// <summary>
        ///     Where each king stands, kept up to date by <see cref="MakeMove" /> rather than searched for. The check
        ///     filter needs it after every single pseudo-legal move, and scanning sixty-four squares each time is the
        ///     difference between a perft that finishes and one you give up on.
        /// </summary>
        private readonly int[] _kingSquares = new int[2];

        /// <summary>Initializes a new instance of the <see cref="ChessBoard" /> class in the opening position.</summary>
        public ChessBoard() : this(StartingFen)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ChessBoard" /> class from a FEN string.</summary>
        /// <param name="fen">The position, in Forsyth-Edwards Notation.</param>
        public ChessBoard(string fen)
        {
            LoadFen(fen);
        }

        /// <summary>Whose turn it is.</summary>
        public PieceColorEnum SideToMove { get; private set; }

        /// <summary>Which castles are still available in principle.</summary>
        public CastlingRightsEnum CastlingRights { get; private set; }

        /// <summary>The square a pawn may capture onto en passant, or -1.</summary>
        public int EnPassantSquare { get; private set; }

        /// <summary>Plies since the last capture or pawn move; the fifty-move rule draws at 100.</summary>
        public int HalfmoveClock { get; private set; }

        /// <summary>The move number, incremented after Black moves.</summary>
        public int FullmoveNumber { get; private set; }

        /// <summary>What is standing on a square.</summary>
        /// <param name="square">The square, 0-63.</param>
        /// <returns>The piece, or <see cref="Piece.None" />.</returns>
        public Piece this[int square] => _squares[square];

        /// <summary>The file of a square, 0 (a) to 7 (h).</summary>
        /// <param name="square">The square.</param>
        /// <returns>The file.</returns>
        public static int FileOf(int square) => square & 7;

        /// <summary>The rank of a square, 0 (rank 1) to 7 (rank 8).</summary>
        /// <param name="square">The square.</param>
        /// <returns>The rank.</returns>
        public static int RankOf(int square) => square >> 3;

        /// <summary>The square at a file and rank, without checking they are on the board.</summary>
        /// <param name="file">The file, 0-7.</param>
        /// <param name="rank">The rank, 0-7.</param>
        /// <returns>The square index.</returns>
        public static int SquareAt(int file, int rank) => (rank << 3) | file;

        /// <summary>Whether a file and rank name a square that exists.</summary>
        /// <param name="file">The file.</param>
        /// <param name="rank">The rank.</param>
        /// <returns>True when both are within 0-7.</returns>
        public static bool OnBoard(int file, int rank) => file >= 0 && file < 8 && rank >= 0 && rank < 8;

        /// <summary>The usual name of a square, "a1" through "h8".</summary>
        /// <param name="square">The square.</param>
        /// <returns>The name.</returns>
        public static string SquareName(int square) =>
            $"{(char) ('a' + FileOf(square))}{(char) ('1' + RankOf(square))}";

        /// <summary>Reads a square name such as "e4".</summary>
        /// <param name="name">The name to read.</param>
        /// <param name="square">The square it named.</param>
        /// <returns>True when the name was a square.</returns>
        public static bool TryParseSquare(string name, out int square)
        {
            square = -1;
            if (string.IsNullOrEmpty(name) || name.Length < 2)
                return false;

            var file = char.ToLowerInvariant(name[0]) - 'a';
            var rank = name[1] - '1';
            if (!OnBoard(file, rank))
                return false;

            square = SquareAt(file, rank);
            return true;
        }

        /// <summary>The other side.</summary>
        /// <param name="color">A colour.</param>
        /// <returns>The opposite colour.</returns>
        public static PieceColorEnum Opponent(PieceColorEnum color) =>
            color == PieceColorEnum.White ? PieceColorEnum.Black : PieceColorEnum.White;

        /// <summary>Where a side's king is standing.</summary>
        /// <param name="color">The side.</param>
        /// <returns>The king's square.</returns>
        public int KingSquare(PieceColorEnum color) => _kingSquares[(int) color];

        /// <summary>Whether that side's king is currently attacked.</summary>
        /// <param name="color">The side.</param>
        /// <returns>True when in check.</returns>
        public bool IsInCheck(PieceColorEnum color) =>
            IsSquareAttacked(_kingSquares[(int) color], Opponent(color));

        /// <summary>
        ///     Whether a square is attacked by a side. Asked of the king's square to detect check, and of the squares
        ///     a castling king passes over.
        ///     <para>
        ///         Runs outward from the square being tested rather than over every enemy piece, which is both faster
        ///         and the reason the pawn case reads backwards: to find a <i>white</i> pawn attacking this square,
        ///         look for one a rank <i>below</i> and a file to either side, because that is where it would have to
        ///         stand to be capturing upward onto it.
        ///     </para>
        /// </summary>
        /// <param name="square">The square to test.</param>
        /// <param name="by">The side that might be attacking it.</param>
        /// <returns>True when that side attacks the square.</returns>
        public bool IsSquareAttacked(int square, PieceColorEnum by)
        {
            var file = FileOf(square);
            var rank = RankOf(square);
            var pawnRankStep = by == PieceColorEnum.White ? -1 : 1;

            foreach (var fileStep in _sideSteps)
            {
                var f = file + fileStep;
                var r = rank + pawnRankStep;
                if (OnBoard(f, r) && _squares[SquareAt(f, r)] == new Piece(by, PieceKindEnum.Pawn))
                    return true;
            }

            foreach (var (df, dr) in _knightSteps)
            {
                var f = file + df;
                var r = rank + dr;
                if (OnBoard(f, r) && _squares[SquareAt(f, r)] == new Piece(by, PieceKindEnum.Knight))
                    return true;
            }

            foreach (var (df, dr) in _kingSteps)
            {
                var f = file + df;
                var r = rank + dr;
                if (OnBoard(f, r) && _squares[SquareAt(f, r)] == new Piece(by, PieceKindEnum.King))
                    return true;
            }

            if (RayHits(file, rank, _bishopRays, by, PieceKindEnum.Bishop))
                return true;

            return RayHits(file, rank, _rookRays, by, PieceKindEnum.Rook);
        }

        /// <summary>Whether sliding out along these rays runs into that piece kind, or a queen, of that colour.</summary>
        private bool RayHits(int file, int rank, (int File, int Rank)[] rays, PieceColorEnum by, PieceKindEnum kind)
        {
            foreach (var (df, dr) in rays)
            {
                var f = file + df;
                var r = rank + dr;
                while (OnBoard(f, r))
                {
                    var occupant = _squares[SquareAt(f, r)];
                    if (!occupant.IsEmpty)
                    {
                        if (occupant.Color == by && (occupant.Kind == kind || occupant.Kind == PieceKindEnum.Queen))
                            return true;

                        break;
                    }

                    f += df;
                    r += dr;
                }
            }

            return false;
        }

        /// <summary>
        ///     Every move the side to move may legally make. An empty list means the game is over — checkmate if the
        ///     side is in check, stalemate if it is not, which is the only difference between the two.
        /// </summary>
        /// <returns>The legal moves.</returns>
        public List<ChessMove> GenerateMoves()
        {
            var pseudo = GeneratePseudoLegalMoves();
            var legal = new List<ChessMove>(pseudo.Count);
            var mover = SideToMove;

            foreach (var move in pseudo)
            {
                var undo = MakeMove(move);
                if (!IsSquareAttacked(_kingSquares[(int) mover], Opponent(mover)))
                    legal.Add(move);

                UnmakeMove(move, undo);
            }

            return legal;
        }

        /// <summary>Every move ignoring whether it leaves the mover's own king attacked.</summary>
        /// <returns>The pseudo-legal moves.</returns>
        public List<ChessMove> GeneratePseudoLegalMoves()
        {
            var moves = new List<ChessMove>(48);

            for (var square = 0; square < 64; square++)
            {
                var piece = _squares[square];
                if (!piece.Is(SideToMove))
                    continue;

                switch (piece.Kind)
                {
                    case PieceKindEnum.Pawn:
                        AddPawnMoves(moves, square);
                        break;
                    case PieceKindEnum.Knight:
                        AddStepMoves(moves, square, _knightSteps);
                        break;
                    case PieceKindEnum.Bishop:
                        AddSlidingMoves(moves, square, _bishopRays);
                        break;
                    case PieceKindEnum.Rook:
                        AddSlidingMoves(moves, square, _rookRays);
                        break;
                    case PieceKindEnum.Queen:
                        AddSlidingMoves(moves, square, _bishopRays);
                        AddSlidingMoves(moves, square, _rookRays);
                        break;
                    case PieceKindEnum.King:
                        AddStepMoves(moves, square, _kingSteps);
                        AddCastles(moves, square);
                        break;
                }
            }

            return moves;
        }

        private void AddStepMoves(List<ChessMove> moves, int from, (int File, int Rank)[] steps)
        {
            var file = FileOf(from);
            var rank = RankOf(from);

            foreach (var (df, dr) in steps)
            {
                var f = file + df;
                var r = rank + dr;
                if (!OnBoard(f, r))
                    continue;

                var to = SquareAt(f, r);
                if (!_squares[to].Is(SideToMove))
                    moves.Add(new ChessMove(from, to));
            }
        }

        private void AddSlidingMoves(List<ChessMove> moves, int from, (int File, int Rank)[] rays)
        {
            var file = FileOf(from);
            var rank = RankOf(from);

            foreach (var (df, dr) in rays)
            {
                var f = file + df;
                var r = rank + dr;
                while (OnBoard(f, r))
                {
                    var to = SquareAt(f, r);
                    var occupant = _squares[to];

                    if (occupant.IsEmpty)
                    {
                        moves.Add(new ChessMove(from, to));
                    }
                    else
                    {
                        if (occupant.Color != SideToMove)
                            moves.Add(new ChessMove(from, to));

                        break;
                    }

                    f += df;
                    r += dr;
                }
            }
        }

        private void AddPawnMoves(List<ChessMove> moves, int from)
        {
            var file = FileOf(from);
            var rank = RankOf(from);
            var forward = SideToMove == PieceColorEnum.White ? 1 : -1;
            var startRank = SideToMove == PieceColorEnum.White ? 1 : 6;
            var lastRank = SideToMove == PieceColorEnum.White ? 7 : 0;

            var oneRank = rank + forward;
            if (OnBoard(file, oneRank))
            {
                var one = SquareAt(file, oneRank);
                if (_squares[one].IsEmpty)
                {
                    AddPawnMove(moves, from, one, oneRank == lastRank);

                    // The double push needs BOTH squares empty; testing only the destination lets a pawn jump a
                    // piece standing directly in front of it.
                    var twoRank = rank + forward + forward;
                    if (rank == startRank && OnBoard(file, twoRank))
                    {
                        var two = SquareAt(file, twoRank);
                        if (_squares[two].IsEmpty)
                            moves.Add(new ChessMove(from, two));
                    }
                }
            }

            foreach (var fileStep in _sideSteps)
            {
                var f = file + fileStep;
                if (!OnBoard(f, oneRank))
                    continue;

                var to = SquareAt(f, oneRank);
                var occupant = _squares[to];

                if (!occupant.IsEmpty && occupant.Color != SideToMove)
                    AddPawnMove(moves, from, to, oneRank == lastRank);
                else if (to == EnPassantSquare && occupant.IsEmpty)
                    moves.Add(new ChessMove(from, to, isEnPassant: true));
            }
        }

        /// <summary>Adds a pawn move, fanning it out into the four promotions when it reaches the last rank.</summary>
        private static void AddPawnMove(List<ChessMove> moves, int from, int to, bool promoting)
        {
            if (!promoting)
            {
                moves.Add(new ChessMove(from, to));
                return;
            }

            foreach (var kind in _promotionChoices)
                moves.Add(new ChessMove(from, to, kind));
        }

        /// <summary>
        ///     Adds whichever castles are available. Three separate conditions, and conflating any two of them is a
        ///     classic bug: the right must still exist, the squares <b>between</b> king and rook must be empty (three
        ///     of them on the queen's side, including the b-file square the king never visits), and the king must not
        ///     be in check, pass through an attacked square, or land on one. Attacks are only tested for the two
        ///     squares the king actually travels over — the b-file square may be attacked freely.
        /// </summary>
        private void AddCastles(List<ChessMove> moves, int kingSquare)
        {
            var white = SideToMove == PieceColorEnum.White;
            var home = white ? 4 : 60;
            if (kingSquare != home)
                return;

            var enemy = Opponent(SideToMove);
            if (IsSquareAttacked(home, enemy))
                return;

            var shortRight = white ? CastlingRightsEnum.WhiteKingSide : CastlingRightsEnum.BlackKingSide;
            if ((CastlingRights & shortRight) != 0 &&
                _squares[home + 1].IsEmpty && _squares[home + 2].IsEmpty &&
                !IsSquareAttacked(home + 1, enemy) && !IsSquareAttacked(home + 2, enemy))
                moves.Add(new ChessMove(home, home + 2, isCastle: true));

            var longRight = white ? CastlingRightsEnum.WhiteQueenSide : CastlingRightsEnum.BlackQueenSide;
            if ((CastlingRights & longRight) != 0 &&
                _squares[home - 1].IsEmpty && _squares[home - 2].IsEmpty && _squares[home - 3].IsEmpty &&
                !IsSquareAttacked(home - 1, enemy) && !IsSquareAttacked(home - 2, enemy))
                moves.Add(new ChessMove(home, home - 2, isCastle: true));
        }

        /// <summary>
        ///     Everything a move destroys, so <see cref="UnmakeMove" /> can put it back. None of it can be recomputed
        ///     from the position afterwards, which is why it is returned rather than derived.
        /// </summary>
        public readonly struct Undo
        {
            internal Undo(Piece captured, int capturedSquare, CastlingRightsEnum rights, int enPassant,
                int halfmoveClock)
            {
                Captured = captured;
                CapturedSquare = capturedSquare;
                Rights = rights;
                EnPassant = enPassant;
                HalfmoveClock = halfmoveClock;
            }

            internal Piece Captured { get; }

            /// <summary>Where the captured piece stood — not the destination square, for an en passant capture.</summary>
            internal int CapturedSquare { get; }

            internal CastlingRightsEnum Rights { get; }

            internal int EnPassant { get; }

            internal int HalfmoveClock { get; }
        }

        /// <summary>
        ///     Plays a move and hands back what is needed to take it back. Assumes the move is one this position
        ///     actually offers; it is called a few million times by a search, so it validates nothing.
        /// </summary>
        /// <param name="move">The move to play.</param>
        /// <returns>The state needed to undo it.</returns>
        public Undo MakeMove(ChessMove move)
        {
            var mover = _squares[move.From];
            var capturedSquare = move.To;

            if (move.IsEnPassant)
            {
                // The pawn taken is beside the capturing pawn, not under it: same file as the destination, same
                // rank as the square moved from.
                capturedSquare = SquareAt(FileOf(move.To), RankOf(move.From));
            }

            var captured = _squares[capturedSquare];
            var undo = new Undo(captured, capturedSquare, CastlingRights, EnPassantSquare, HalfmoveClock);

            _squares[capturedSquare] = Piece.None;
            _squares[move.From] = Piece.None;
            _squares[move.To] = move.Promotion == PieceKindEnum.None
                ? mover
                : new Piece(mover.Color, move.Promotion);

            if (mover.Kind == PieceKindEnum.King)
            {
                _kingSquares[(int) mover.Color] = move.To;

                if (move.IsCastle)
                {
                    // The rook hops the king. Which rook is decided by which way the king went.
                    var kingSide = move.To > move.From;
                    var rookFrom = kingSide ? move.To + 1 : move.To - 2;
                    var rookTo = kingSide ? move.To - 1 : move.To + 1;
                    _squares[rookTo] = _squares[rookFrom];
                    _squares[rookFrom] = Piece.None;
                }
            }

            UpdateCastlingRights(move, mover, capturedSquare, captured);

            // Set only by a double push, and cleared by everything else — an en passant right lasts exactly one ply.
            EnPassantSquare = mover.Kind == PieceKindEnum.Pawn && Math.Abs(RankOf(move.To) - RankOf(move.From)) == 2
                ? SquareAt(FileOf(move.From), (RankOf(move.From) + RankOf(move.To)) / 2)
                : -1;

            HalfmoveClock = mover.Kind == PieceKindEnum.Pawn || !captured.IsEmpty ? 0 : HalfmoveClock + 1;

            if (SideToMove == PieceColorEnum.Black)
                FullmoveNumber++;

            SideToMove = Opponent(SideToMove);
            return undo;
        }

        /// <summary>Takes a move back, restoring everything <see cref="MakeMove" /> destroyed.</summary>
        /// <param name="move">The move that was played.</param>
        /// <param name="undo">What that move returned.</param>
        public void UnmakeMove(ChessMove move, Undo undo)
        {
            SideToMove = Opponent(SideToMove);
            if (SideToMove == PieceColorEnum.Black)
                FullmoveNumber--;

            var moved = _squares[move.To];

            // A promoted piece goes back to being the pawn it was, or the pawn is lost and the count drifts.
            _squares[move.From] = move.Promotion == PieceKindEnum.None
                ? moved
                : new Piece(moved.Color, PieceKindEnum.Pawn);

            _squares[move.To] = Piece.None;
            _squares[undo.CapturedSquare] = undo.Captured;

            if (moved.Kind == PieceKindEnum.King)
            {
                _kingSquares[(int) moved.Color] = move.From;

                if (move.IsCastle)
                {
                    var kingSide = move.To > move.From;
                    var rookFrom = kingSide ? move.To + 1 : move.To - 2;
                    var rookTo = kingSide ? move.To - 1 : move.To + 1;
                    _squares[rookFrom] = _squares[rookTo];
                    _squares[rookTo] = Piece.None;
                }
            }

            CastlingRights = undo.Rights;
            EnPassantSquare = undo.EnPassant;
            HalfmoveClock = undo.HalfmoveClock;
        }

        /// <summary>
        ///     Clears whichever castling rights this move destroys. Three separate causes, and the third is the one
        ///     that gets missed: a right dies when the king moves, when that rook moves, <b>and when that rook is
        ///     captured on its home square</b> — which is the opponent losing a right on a move they did not make.
        /// </summary>
        private void UpdateCastlingRights(ChessMove move, Piece mover, int capturedSquare, Piece captured)
        {
            if (mover.Kind == PieceKindEnum.King)
            {
                CastlingRights &= mover.Color == PieceColorEnum.White
                    ? ~(CastlingRightsEnum.WhiteKingSide | CastlingRightsEnum.WhiteQueenSide)
                    : ~(CastlingRightsEnum.BlackKingSide | CastlingRightsEnum.BlackQueenSide);
            }

            CastlingRights &= ~RightsOnCorner(move.From);

            if (!captured.IsEmpty)
                CastlingRights &= ~RightsOnCorner(capturedSquare);
        }

        /// <summary>Which right lives on that corner square, if any.</summary>
        private static CastlingRightsEnum RightsOnCorner(int square) => square switch
        {
            0 => CastlingRightsEnum.WhiteQueenSide,
            7 => CastlingRightsEnum.WhiteKingSide,
            56 => CastlingRightsEnum.BlackQueenSide,
            63 => CastlingRightsEnum.BlackKingSide,
            _ => CastlingRightsEnum.None
        };

        /// <summary>Replaces the whole position from a FEN string.</summary>
        /// <param name="fen">The position to load.</param>
        public void LoadFen(string fen)
        {
            if (string.IsNullOrWhiteSpace(fen))
                throw new ArgumentException("A FEN string is required.", nameof(fen));

            var fields = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 4)
                throw new ArgumentException($"\"{fen}\" is not a FEN string; it needs at least four fields.",
                    nameof(fen));

            Array.Clear(_squares, 0, _squares.Length);

            var rank = 7;
            var file = 0;
            foreach (var symbol in fields[0])
            {
                if (symbol == '/')
                {
                    rank--;
                    file = 0;
                    continue;
                }

                if (char.IsDigit(symbol))
                {
                    file += symbol - '0';
                    continue;
                }

                if (!Piece.TryFromFenChar(symbol, out var piece))
                    throw new ArgumentException($"\"{symbol}\" is not a piece.", nameof(fen));

                if (!OnBoard(file, rank))
                    throw new ArgumentException($"\"{fields[0]}\" describes more squares than a board has.",
                        nameof(fen));

                var square = SquareAt(file, rank);
                _squares[square] = piece;
                if (piece.Kind == PieceKindEnum.King)
                    _kingSquares[(int) piece.Color] = square;

                file++;
            }

            SideToMove = fields[1] == "b" ? PieceColorEnum.Black : PieceColorEnum.White;

            CastlingRights = CastlingRightsEnum.None;
            foreach (var right in fields[2])
            {
                CastlingRights |= right switch
                {
                    'K' => CastlingRightsEnum.WhiteKingSide,
                    'Q' => CastlingRightsEnum.WhiteQueenSide,
                    'k' => CastlingRightsEnum.BlackKingSide,
                    'q' => CastlingRightsEnum.BlackQueenSide,
                    _ => CastlingRightsEnum.None
                };
            }

            EnPassantSquare = TryParseSquare(fields[3], out var ep) ? ep : -1;
            HalfmoveClock = fields.Length > 4 && int.TryParse(fields[4], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var clock)
                ? clock
                : 0;
            FullmoveNumber = fields.Length > 5 && int.TryParse(fields[5], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var move)
                ? move
                : 1;
        }

        /// <summary>The position as a FEN string, which round-trips through <see cref="LoadFen" />.</summary>
        /// <returns>The FEN.</returns>
        public string ToFen()
        {
            var sb = new StringBuilder();

            for (var rank = 7; rank >= 0; rank--)
            {
                var empty = 0;
                for (var file = 0; file < 8; file++)
                {
                    var piece = _squares[SquareAt(file, rank)];
                    if (piece.IsEmpty)
                    {
                        empty++;
                        continue;
                    }

                    if (empty > 0)
                    {
                        sb.Append(empty.ToString(CultureInfo.InvariantCulture));
                        empty = 0;
                    }

                    sb.Append(piece.ToFenChar());
                }

                if (empty > 0)
                    sb.Append(empty.ToString(CultureInfo.InvariantCulture));

                if (rank > 0)
                    sb.Append('/');
            }

            sb.Append(SideToMove == PieceColorEnum.White ? " w " : " b ");

            if (CastlingRights == CastlingRightsEnum.None)
            {
                sb.Append('-');
            }
            else
            {
                if ((CastlingRights & CastlingRightsEnum.WhiteKingSide) != 0) sb.Append('K');
                if ((CastlingRights & CastlingRightsEnum.WhiteQueenSide) != 0) sb.Append('Q');
                if ((CastlingRights & CastlingRightsEnum.BlackKingSide) != 0) sb.Append('k');
                if ((CastlingRights & CastlingRightsEnum.BlackQueenSide) != 0) sb.Append('q');
            }

            sb.Append(' ').Append(EnPassantSquare >= 0 ? SquareName(EnPassantSquare) : "-");
            sb.Append(' ').Append(HalfmoveClock.ToString(CultureInfo.InvariantCulture));
            sb.Append(' ').Append(FullmoveNumber.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>A copy of this position, sharing nothing with it.</summary>
        /// <returns>The copy.</returns>
        public ChessBoard Clone() => new(ToFen());
    }
}
