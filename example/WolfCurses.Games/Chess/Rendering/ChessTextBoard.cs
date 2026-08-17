// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     The board as characters, for when there are no pixels to be had.
    ///     <para>
    ///         Not a consolation prize. The picture goes through <see cref="ImageRenderers.Default" /> like
    ///         everything else in this repository, which on a terminal the probe found nothing on means half blocks
    ///         — and a chess board squeezed into the twenty-odd rows a terminal has left after the chrome is
    ///         forty-eight pixels of knight resampled into three rows, which is a smudge. Chess needs its pieces
    ///         <i>identifiable</i>, not merely present, so below the size where the artwork survives, letters do the
    ///         job better than a picture of letters would.
    ///     </para>
    ///     <para>
    ///         Both sides are drawn with the <b>outlined</b> Unicode pieces and told apart by ink colour, rather
    ///         than by using the filled black glyphs: on a dark terminal the black glyphs disappear into the
    ///         background. The rest of this repository already assumes these code points work — the other three
    ///         games draw with box-drawing and block characters — and the library switches standard output to UTF-8
    ///         whenever a real console is attached.
    ///     </para>
    ///     <para>
    ///         That leaves one honest hole. Colour is what distinguishes the two sides, and it is left at
    ///         <see cref="AnsiColorModeEnum.Auto" /> so <c>NO_COLOR</c> and the library's forced-colour override
    ///         both reach it — at which point White and Black are the same character on the same background. Rather
    ///         than pretend otherwise, <see cref="Render" /> switches to the FEN letters when colour resolves to
    ///         <see cref="AnsiColorModeEnum.None" />, where uppercase and lowercase carry the side exactly as FEN
    ///         has always had them do.
    ///     </para>
    /// </summary>
    public static class ChessTextBoard
    {
        /// <summary>How wide a square is drawn, in characters. Three keeps the board roughly square on screen.</summary>
        private const int SquareWidth = 3;

        private static readonly TextStyle _lightSquare =
            new(new TextColor(new Rgb24(0x1C, 0x1C, 0x1C)), new TextColor(new Rgb24(0xD8, 0xC0, 0xA0)));

        private static readonly TextStyle _darkSquare =
            new(new TextColor(new Rgb24(0x1C, 0x1C, 0x1C)), new TextColor(new Rgb24(0x9A, 0x74, 0x54)));

        private static readonly TextStyle _cursorSquare =
            new(new TextColor(new Rgb24(0x10, 0x20, 0x10)), new TextColor(new Rgb24(0x7E, 0xC8, 0x50)));

        private static readonly TextStyle _selectedSquare =
            new(new TextColor(new Rgb24(0x10, 0x20, 0x10)), new TextColor(new Rgb24(0xA8, 0xE0, 0x70)));

        private static readonly TextStyle _targetSquare =
            new(new TextColor(new Rgb24(0x10, 0x20, 0x10)), new TextColor(new Rgb24(0xAE, 0xC0, 0x86)));

        private static readonly TextStyle _lastMoveSquare =
            new(new TextColor(new Rgb24(0x20, 0x20, 0x10)), new TextColor(new Rgb24(0xD8, 0xDC, 0x78)));

        private static readonly TextStyle _checkSquare =
            new(new TextColor(new Rgb24(0xFF, 0xFF, 0xFF)), new TextColor(new Rgb24(0xC0, 0x40, 0x40)));

        /// <summary>White's pieces. The glyph is the same for both sides; this is what tells them apart.</summary>
        private static readonly TextColor _whiteInk = new(new Rgb24(0xFF, 0xFF, 0xFF));

        /// <summary>Black's pieces.</summary>
        private static readonly TextColor _blackInk = new(new Rgb24(0x10, 0x10, 0x10));

        /// <summary>Draws the position.</summary>
        /// <param name="board">The position.</param>
        /// <param name="flipped">True to draw from Black's side.</param>
        /// <param name="marks">What to highlight, or null.</param>
        /// <returns>The board as text, with rank and file labels.</returns>
        public static string Render(ChessBoard board, bool flipped,
            IReadOnlyDictionary<int, ChessSquareMarkEnum> marks)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

            // Resolved once for the whole board rather than per square, and it decides which alphabet is used: with
            // no colour available the two sides would be identical glyphs, so the letters take over.
            var colorless = AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None;
            var sb = new StringBuilder();

            for (var row = 0; row < 8; row++)
            {
                var rank = flipped ? row : 7 - row;
                sb.Append((char) ('1' + rank)).Append(' ');

                for (var column = 0; column < 8; column++)
                {
                    var file = flipped ? 7 - column : column;
                    var square = ChessBoard.SquareAt(file, rank);

                    var mark = ChessSquareMarkEnum.None;
                    marks?.TryGetValue(square, out mark);

                    var piece = board[square];
                    var style = StyleFor((file + rank) % 2 != 0, mark);

                    // The square decides the background, the piece decides the ink. Both sides use the same
                    // outlined glyph, so without this a black knight and a white knight are the same character on
                    // the same square colour - which is to say, indistinguishable.
                    if (!piece.IsEmpty)
                        style = style.WithForeground(piece.Color == PieceColorEnum.White ? _whiteInk : _blackInk);

                    var glyph = colorless
                        ? piece.IsEmpty ? '.' : piece.ToFenChar()
                        : GlyphFor(piece);

                    sb.Append(style.Apply(CenterInSquare(glyph)));
                }

                sb.AppendLine();
            }

            sb.Append("  ");
            for (var column = 0; column < 8; column++)
                sb.Append(CenterInSquare((char) ('a' + (flipped ? 7 - column : column))));

            return sb.ToString();
        }

        /// <summary>Pads a glyph out to a square's width, leaving it centred.</summary>
        private static string CenterInSquare(char glyph) => $" {glyph} ";

        /// <summary>
        ///     The Unicode piece for a square. <b>Both colours use the outlined (white) glyphs</b>, which looks
        ///     wrong written down and right on screen: the solid black glyphs disappear into a dark terminal
        ///     background, and the piece's colour is already carried by the foreground colour. On a terminal with no
        ///     colour at all this would be ambiguous, which is why <see cref="GlyphFor" /> is not the only thing the
        ///     fallback relies on — see the class summary.
        /// </summary>
        private static char GlyphFor(Piece piece) => piece.Kind switch
        {
            PieceKindEnum.Pawn => '♙',
            PieceKindEnum.Knight => '♘',
            PieceKindEnum.Bishop => '♗',
            PieceKindEnum.Rook => '♖',
            PieceKindEnum.Queen => '♕',
            PieceKindEnum.King => '♔',
            _ => ' '
        };

        /// <summary>The style for a square, with the piece colour written into the foreground.</summary>
        private static TextStyle StyleFor(bool dark, ChessSquareMarkEnum mark) => mark switch
        {
            ChessSquareMarkEnum.Cursor => _cursorSquare,
            ChessSquareMarkEnum.Selected => _selectedSquare,
            ChessSquareMarkEnum.Target => _targetSquare,
            ChessSquareMarkEnum.LastMove => _lastMoveSquare,
            ChessSquareMarkEnum.Check => _checkSquare,
            _ => dark ? _darkSquare : _lightSquare
        };

        /// <summary>How many characters wide a rendered board is, for a caller sizing a panel beside it.</summary>
        public static int Width => 2 + 8 * SquareWidth;
    }
}
