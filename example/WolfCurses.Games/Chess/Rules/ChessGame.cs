// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     A game rather than a position: the board, everything that has happened to it, and the several different
    ///     ways it can be over. Still no screen and no bot.
    ///     <para>
    ///         The split from <see cref="ChessBoard" /> is not tidiness. The board must stay cheap because a search
    ///         makes and unmakes millions of moves on it, and none of what is here — the move list, the repetition
    ///         history, the notation — is anything a search wants to pay for.
    ///     </para>
    /// </summary>
    public sealed class ChessGame
    {
        /// <summary>Keys of every position that has occurred, for threefold repetition.</summary>
        private readonly List<string> _positions = new();

        private readonly List<string> _notation = new();

        /// <summary>Initializes a new instance of the <see cref="ChessGame" /> class in the opening position.</summary>
        public ChessGame() : this(ChessBoard.StartingFen)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ChessGame" /> class from a position.</summary>
        /// <param name="fen">The position to start from.</param>
        public ChessGame(string fen)
        {
            Board = new ChessBoard(fen);
            LegalMoves = Board.GenerateMoves();
            _positions.Add(RepetitionKey());

            // A position can arrive already finished - loaded from a FEN, or set up by a puzzle - and a game that
            // only ever decides its result after a move would call that one InProgress and let the player move.
            UpdateResult(LegalMoves.Count == 0, IsInCheck);
        }

        /// <summary>The position.</summary>
        public ChessBoard Board { get; }

        /// <summary>Every move the side to move may play, recomputed once per move rather than per question.</summary>
        public IReadOnlyList<ChessMove> LegalMoves { get; private set; }

        /// <summary>The moves played, in algebraic notation.</summary>
        public IReadOnlyList<string> Notation => _notation;

        /// <summary>How the game ended, or <see cref="ChessResultEnum.InProgress" />.</summary>
        public ChessResultEnum Result { get; private set; }

        /// <summary>Who won, when <see cref="Result" /> is <see cref="ChessResultEnum.Checkmate" /> or a resignation.</summary>
        public PieceColorEnum Winner { get; private set; }

        /// <summary>Whether the game is over for any reason.</summary>
        public bool IsOver => Result != ChessResultEnum.InProgress;

        /// <summary>Whether the side to move is in check.</summary>
        public bool IsInCheck => Board.IsInCheck(Board.SideToMove);

        /// <summary>Plays a move, updating the notation, the repetition history and the result.</summary>
        /// <param name="move">The move to play; must be one of <see cref="LegalMoves" />.</param>
        public void Play(ChessMove move)
        {
            if (IsOver)
                return;

            // Written before the move is made, because notation describes the position the move was played in —
            // which pieces could have gone there, and therefore whether it needs disambiguating.
            var san = ToSan(move);

            Board.MakeMove(move);
            LegalMoves = Board.GenerateMoves();
            _positions.Add(RepetitionKey());

            var check = Board.IsInCheck(Board.SideToMove);
            var noMoves = LegalMoves.Count == 0;

            if (noMoves)
                san += check ? "#" : "";
            else if (check)
                san += "+";

            _notation.Add(san);
            UpdateResult(noMoves, check);
        }

        /// <summary>Ends the game with one side giving up.</summary>
        /// <param name="loser">The side resigning.</param>
        public void Resign(PieceColorEnum loser)
        {
            if (IsOver)
                return;

            Result = ChessResultEnum.Resignation;
            Winner = ChessBoard.Opponent(loser);
        }

        /// <summary>
        ///     Finds the legal move a player meant.
        ///     <para>
        ///         Rather than parse notation — which is a surprising amount of code, and all of it a chance to
        ///         reject something legal — this generates the legal moves and asks each one what it is called. If a
        ///         name matches, that is the move. It is the same trick that makes the notation and the parser
        ///         impossible to disagree with each other, and it accepts long algebraic ("e2e4", what UCI speaks),
        ///         short algebraic ("e4", "Nf3", "exd5", "e8=Q"), and both spellings of castling for free.
        ///     </para>
        /// </summary>
        /// <param name="text">What the player typed.</param>
        /// <param name="move">The move they meant.</param>
        /// <returns>True when exactly one legal move answers to that name.</returns>
        public bool TryParseMove(string text, out ChessMove move)
        {
            move = ChessMove.None;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var wanted = text.Trim().Replace("0", "O").Replace("-", string.Empty)
                .Replace("+", string.Empty).Replace("#", string.Empty).Replace("x", string.Empty);

            foreach (var candidate in LegalMoves)
            {
                var lan = candidate.ToString();
                var san = ToSan(candidate).Replace("-", string.Empty).Replace("x", string.Empty);

                if (string.Equals(wanted, lan, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(wanted, san, StringComparison.Ordinal))
                {
                    move = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     What a move is called, in standard algebraic notation, in the position it is about to be played in.
        ///     <para>
        ///         The disambiguation rules are the fiddly part and they are exactly these: if another piece of the
        ///         same kind could also legally move to that square, add the file it came from; if that is still
        ///         ambiguous — two pieces on the same file — add the rank instead; and only if <i>that</i> is still
        ///         ambiguous, which needs three pieces of a kind and so usually a promotion, write both.
        ///     </para>
        /// </summary>
        /// <param name="move">The move to name.</param>
        /// <returns>The move in SAN, without any check or mate suffix.</returns>
        public string ToSan(ChessMove move)
        {
            if (move.IsCastle)
                return ChessBoard.FileOf(move.To) > ChessBoard.FileOf(move.From) ? "O-O" : "O-O-O";

            var piece = Board[move.From];
            var captures = move.IsEnPassant || !Board[move.To].IsEmpty;
            var sb = new StringBuilder();

            if (piece.Kind == PieceKindEnum.Pawn)
            {
                // A pawn is named by the file it left, but only when it captured — "exd5", but plain "d5" otherwise.
                if (captures)
                    sb.Append((char) ('a' + ChessBoard.FileOf(move.From))).Append('x');
            }
            else
            {
                sb.Append(char.ToUpperInvariant(piece.ToFenChar()));
                sb.Append(Disambiguate(move, piece));
                if (captures)
                    sb.Append('x');
            }

            sb.Append(ChessBoard.SquareName(move.To));

            if (move.Promotion != PieceKindEnum.None)
                sb.Append('=').Append(char.ToUpperInvariant(new Piece(piece.Color, move.Promotion).ToFenChar()));

            return sb.ToString();
        }

        /// <summary>Works out how much of the origin square a move has to state to be unambiguous.</summary>
        private string Disambiguate(ChessMove move, Piece piece)
        {
            var sameFile = false;
            var sameRank = false;
            var others = false;

            foreach (var other in LegalMoves)
            {
                if (other.From == move.From || other.To != move.To)
                    continue;

                var mover = Board[other.From];
                if (mover.Kind != piece.Kind || mover.Color != piece.Color)
                    continue;

                others = true;
                if (ChessBoard.FileOf(other.From) == ChessBoard.FileOf(move.From))
                    sameFile = true;
                if (ChessBoard.RankOf(other.From) == ChessBoard.RankOf(move.From))
                    sameRank = true;
            }

            if (!others)
                return string.Empty;

            // File alone is enough unless a rival shares it; then rank; then, for three-of-a-kind, both.
            if (!sameFile)
                return ((char) ('a' + ChessBoard.FileOf(move.From))).ToString(CultureInfo.InvariantCulture);

            if (!sameRank)
                return ((char) ('1' + ChessBoard.RankOf(move.From))).ToString(CultureInfo.InvariantCulture);

            return ChessBoard.SquareName(move.From);
        }

        /// <summary>Decides whether the game has ended, and how.</summary>
        private void UpdateResult(bool noMoves, bool check)
        {
            if (noMoves)
            {
                // The whole difference between the best result in chess and a draw is whether the king happens to
                // be attacked while having nowhere to go.
                Result = check ? ChessResultEnum.Checkmate : ChessResultEnum.Stalemate;
                Winner = ChessBoard.Opponent(Board.SideToMove);
                return;
            }

            if (Board.HalfmoveClock >= 100)
            {
                Result = ChessResultEnum.FiftyMoveRule;
                return;
            }

            if (HasInsufficientMaterial())
            {
                Result = ChessResultEnum.InsufficientMaterial;
                return;
            }

            var key = _positions[^1];
            var seen = 0;
            foreach (var position in _positions)
            {
                if (string.Equals(position, key, StringComparison.Ordinal))
                    seen++;
            }

            if (seen >= 3)
                Result = ChessResultEnum.ThreefoldRepetition;
        }

        /// <summary>
        ///     Whether neither side could force mate with what is left. The list is exact: bare kings, king and
        ///     bishop, king and knight, and king and bishop each where both bishops stand on the same colour. King
        ///     and two knights is <b>not</b> on it — mate is not forced but it is possible, so the game continues.
        /// </summary>
        private bool HasInsufficientMaterial()
        {
            var minors = 0;
            var bishopSquareColors = 0;
            var bishops = 0;

            for (var square = 0; square < 64; square++)
            {
                var piece = Board[square];
                switch (piece.Kind)
                {
                    case PieceKindEnum.None:
                    case PieceKindEnum.King:
                        continue;
                    case PieceKindEnum.Bishop:
                        bishops++;
                        bishopSquareColors |= 1 << ((ChessBoard.FileOf(square) + ChessBoard.RankOf(square)) & 1);
                        minors++;
                        break;
                    case PieceKindEnum.Knight:
                        minors++;
                        break;
                    default:
                        // A pawn, rook or queen anywhere means mate is still reachable.
                        return false;
                }

                if (minors > 2)
                    return false;
            }

            if (minors <= 1)
                return true;

            // Two minors only draw when they are both bishops on squares of one colour, in which case neither can
            // ever attack the squares the other covers.
            return bishops == 2 && bishopSquareColors != 3;
        }

        /// <summary>
        ///     What counts as "the same position" for repetition: the pieces, the side to move, the castling rights
        ///     and the en passant square — and deliberately <b>not</b> the move counters, which always differ.
        /// </summary>
        private string RepetitionKey()
        {
            var fen = Board.ToFen();
            var lastSpace = fen.LastIndexOf(' ');
            var secondLast = lastSpace < 0 ? -1 : fen.LastIndexOf(' ', lastSpace - 1);
            return secondLast < 0 ? fen : fen[..secondLast];
        }
    }
}
