// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     What is standing on a square: a kind and a colour, or nothing. A value type, because a board is
    ///     sixty-four of these and a search makes and unmakes millions of moves — every one of these being an object
    ///     on the heap would make the bot's node count a garbage-collection benchmark.
    /// </summary>
    public readonly struct Piece : IEquatable<Piece>
    {
        /// <summary>An empty square. <c>default(Piece)</c> is this, so a new board is empty with nothing said.</summary>
        public static readonly Piece None = default;

        /// <summary>Initializes a new instance of the <see cref="Piece" /> struct.</summary>
        /// <param name="color">Whose piece it is.</param>
        /// <param name="kind">What the piece is.</param>
        public Piece(PieceColorEnum color, PieceKindEnum kind)
        {
            Color = color;
            Kind = kind;
        }

        /// <summary>Whose piece it is. Meaningless when <see cref="IsEmpty" />.</summary>
        public PieceColorEnum Color { get; }

        /// <summary>What the piece is, or <see cref="PieceKindEnum.None" /> for an empty square.</summary>
        public PieceKindEnum Kind { get; }

        /// <summary>Whether the square is empty.</summary>
        public bool IsEmpty => Kind == PieceKindEnum.None;

        /// <summary>Whether the square holds a piece of that colour. False for an empty square, which is the point.</summary>
        /// <param name="color">The colour to test.</param>
        /// <returns>True when this is a piece and it belongs to that side.</returns>
        public bool Is(PieceColorEnum color) => Kind != PieceKindEnum.None && Color == color;

        /// <summary>
        ///     The FEN letter for this piece — uppercase for White, lowercase for Black — or '.' for an empty square.
        /// </summary>
        /// <returns>The letter.</returns>
        public char ToFenChar()
        {
            var letter = Kind switch
            {
                PieceKindEnum.Pawn => 'p',
                PieceKindEnum.Knight => 'n',
                PieceKindEnum.Bishop => 'b',
                PieceKindEnum.Rook => 'r',
                PieceKindEnum.Queen => 'q',
                PieceKindEnum.King => 'k',
                _ => '.'
            };

            return Color == PieceColorEnum.White ? char.ToUpperInvariant(letter) : letter;
        }

        /// <summary>Reads a FEN letter into a piece. Case is the colour.</summary>
        /// <param name="letter">The letter to read.</param>
        /// <param name="piece">The piece it named.</param>
        /// <returns>True when the letter named a piece.</returns>
        public static bool TryFromFenChar(char letter, out Piece piece)
        {
            var color = char.IsUpper(letter) ? PieceColorEnum.White : PieceColorEnum.Black;
            var kind = char.ToLowerInvariant(letter) switch
            {
                'p' => PieceKindEnum.Pawn,
                'n' => PieceKindEnum.Knight,
                'b' => PieceKindEnum.Bishop,
                'r' => PieceKindEnum.Rook,
                'q' => PieceKindEnum.Queen,
                'k' => PieceKindEnum.King,
                _ => PieceKindEnum.None
            };

            piece = kind == PieceKindEnum.None ? None : new Piece(color, kind);
            return kind != PieceKindEnum.None;
        }

        /// <inheritdoc />
        public bool Equals(Piece other)
        {
            // An empty square is an empty square whatever colour happens to be sitting in the unused field.
            if (Kind == PieceKindEnum.None || other.Kind == PieceKindEnum.None)
                return Kind == other.Kind;

            return Kind == other.Kind && Color == other.Color;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Piece other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Kind == PieceKindEnum.None ? 0 : ((int) Kind << 1) | (int) Color;

        /// <summary>Whether two pieces are the same.</summary>
        /// <param name="left">The first piece.</param>
        /// <param name="right">The second piece.</param>
        /// <returns>True when they are equal.</returns>
        public static bool operator ==(Piece left, Piece right) => left.Equals(right);

        /// <summary>Whether two pieces differ.</summary>
        /// <param name="left">The first piece.</param>
        /// <param name="right">The second piece.</param>
        /// <returns>True when they are not equal.</returns>
        public static bool operator !=(Piece left, Piece right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => IsEmpty ? "." : ToFenChar().ToString();
    }
}
