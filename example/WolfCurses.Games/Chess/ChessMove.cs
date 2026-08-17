// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Globalization;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     One move: where from, where to, and the three things that cannot be worked out from those two squares
    ///     alone.
    ///     <para>
    ///         Those three are worth naming, because each is a bug if it is inferred instead of recorded. A pawn
    ///         capturing <b>en passant</b> lands on an empty square, so "is there something on the destination"
    ///         cannot tell you a capture happened, and the piece removed is not on the square moved to. A
    ///         <b>castle</b> moves two pieces, and inferring it from "the king moved two files" breaks the moment
    ///         you want to support Chess960. A <b>promotion</b> is four different moves to the same square, and
    ///         under-promotion is not a curiosity — it changes perft counts, which is how you find out your
    ///         generator is wrong.
    ///     </para>
    /// </summary>
    public readonly struct ChessMove : IEquatable<ChessMove>
    {
        /// <summary>A move that is not a move; what a failed parse or an empty search returns.</summary>
        public static readonly ChessMove None = default;

        /// <summary>Initializes a new instance of the <see cref="ChessMove" /> struct.</summary>
        /// <param name="from">Square moved from, 0-63.</param>
        /// <param name="to">Square moved to, 0-63.</param>
        /// <param name="promotion">What a pawn reaching the last rank becomes, or None.</param>
        /// <param name="isEnPassant">Whether this pawn capture takes a pawn that is not on the destination square.</param>
        /// <param name="isCastle">Whether this king move also moves a rook.</param>
        public ChessMove(int from, int to, PieceKindEnum promotion = PieceKindEnum.None,
            bool isEnPassant = false, bool isCastle = false)
        {
            From = from;
            To = to;
            Promotion = promotion;
            IsEnPassant = isEnPassant;
            IsCastle = isCastle;
        }

        /// <summary>Square moved from, 0-63 with a1 = 0 and h8 = 63.</summary>
        public int From { get; }

        /// <summary>Square moved to.</summary>
        public int To { get; }

        /// <summary>What a promoting pawn becomes, or <see cref="PieceKindEnum.None" />.</summary>
        public PieceKindEnum Promotion { get; }

        /// <summary>Whether this is an en passant capture.</summary>
        public bool IsEnPassant { get; }

        /// <summary>Whether this is a castle.</summary>
        public bool IsCastle { get; }

        /// <summary>Whether this is a real move rather than <see cref="None" />.</summary>
        public bool IsValid => From != To;

        /// <summary>
        ///     The move in long algebraic notation — "e2e4", "e7e8q" — which is what UCI speaks and what this game
        ///     accepts from the keyboard. Short algebraic (SAN, "Nf3") is prettier and is what the move list shows;
        ///     it needs the whole position to write, so it lives on <see cref="ChessGame" /> rather than here.
        /// </summary>
        /// <returns>The move as coordinates.</returns>
        public override string ToString()
        {
            if (!IsValid)
                return "0000";

            var text = ChessBoard.SquareName(From) + ChessBoard.SquareName(To);
            return Promotion == PieceKindEnum.None
                ? text
                : text + char.ToLowerInvariant(new Piece(PieceColorEnum.Black, Promotion).ToFenChar())
                    .ToString(CultureInfo.InvariantCulture);
        }

        /// <inheritdoc />
        public bool Equals(ChessMove other) =>
            From == other.From && To == other.To && Promotion == other.Promotion &&
            IsEnPassant == other.IsEnPassant && IsCastle == other.IsCastle;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ChessMove other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (From << 14) | (To << 8) | ((int) Promotion << 4) |
                                             (IsEnPassant ? 2 : 0) | (IsCastle ? 1 : 0);

        /// <summary>Whether two moves are the same.</summary>
        /// <param name="left">The first move.</param>
        /// <param name="right">The second move.</param>
        /// <returns>True when they are equal.</returns>
        public static bool operator ==(ChessMove left, ChessMove right) => left.Equals(right);

        /// <summary>Whether two moves differ.</summary>
        /// <param name="left">The first move.</param>
        /// <param name="right">The second move.</param>
        /// <returns>True when they are not equal.</returns>
        public static bool operator !=(ChessMove left, ChessMove right) => !left.Equals(right);
    }
}
