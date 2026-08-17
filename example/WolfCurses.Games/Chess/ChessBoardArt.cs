// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.IO;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Paints a position into a single <see cref="PixelBuffer" />: squares first, then the piece artwork
    ///     composited over them.
    ///     <para>
    ///         <b>One buffer, not sixty-four pictures.</b> A true-pixel picture is one escape blob of zero visible
    ///         width that paints across many rows, and the presenter tracks that with the
    ///         <see cref="AnsiGraphics" /> marker contract — a payload row plus placeholders for the rows it covers.
    ///         Sixty-four of those interleaved on one screen is not a thing the contract can express, and even at
    ///         half blocks it would be sixty-four independently-scaled images that do not line up. Composing the
    ///         whole board into one buffer and rendering it once sidesteps all of it, and is also far cheaper: one
    ///         resample of one image instead of sixty-four.
    ///     </para>
    ///     <para>
    ///         The pieces are transparent PNGs, so the squares underneath show through and the board colours are
    ///         ours to choose — which is what makes highlighting a square possible at all. Compositing is
    ///         <see cref="PixelBuffer.DrawImage" />, which alpha-blends at an offset and clips; nothing here needed
    ///         adding to the library.
    ///     </para>
    /// </summary>
    public sealed class ChessBoardArt
    {
        /// <summary>How many pixels a square is drawn at. The artwork is 48 tall, so this is its native size.</summary>
        public const int SquarePixels = 48;

        /// <summary>The whole board's pixel size.</summary>
        public const int BoardPixels = SquarePixels * 8;

        // Lichess's board colours, which are what most people picture when they picture a chess board.
        private static readonly Rgba32 _lightSquare = new(0xF0, 0xD9, 0xB5, 0xFF);
        private static readonly Rgba32 _darkSquare = new(0xB5, 0x88, 0x63, 0xFF);

        /// <summary>Where the player has put the cursor.</summary>
        private static readonly Rgba32 _cursorTint = new(0x62, 0x9B, 0x3F, 0xFF);

        /// <summary>The piece they have picked up.</summary>
        private static readonly Rgba32 _selectedTint = new(0x83, 0xC5, 0x4E, 0xFF);

        /// <summary>Where it may legally go.</summary>
        private static readonly Rgba32 _targetTint = new(0x9A, 0xB5, 0x7A, 0xFF);

        /// <summary>The move just played, so the player can see what the bot did.</summary>
        private static readonly Rgba32 _lastMoveTint = new(0xCD, 0xD1, 0x6A, 0xFF);

        /// <summary>A king in check.</summary>
        private static readonly Rgba32 _checkTint = new(0xC5, 0x4E, 0x4E, 0xFF);

        private readonly Dictionary<string, PixelBuffer> _pieces = new(StringComparer.Ordinal);

        /// <summary>Initializes a new instance of the <see cref="ChessBoardArt" /> class, loading the piece artwork.</summary>
        /// <param name="folder">Folder holding the twelve PNGs; defaults to the <c>chess</c> folder beside the executable.</param>
        public ChessBoardArt(string folder = null)
        {
            Folder = folder ?? Path.Combine(AppContext.BaseDirectory, "chess");
            LoadPieces();
        }

        /// <summary>Where the artwork was looked for.</summary>
        public string Folder { get; }

        /// <summary>
        ///     Whether all twelve pieces loaded. When false the caller must fall back to the text board — the images
        ///     are copied by the project file, so this being false means somebody ran the executable out of a folder
        ///     that does not have them beside it.
        /// </summary>
        public bool IsAvailable => _pieces.Count == 12;

        /// <summary>Why the artwork could not be loaded, or null.</summary>
        public string Error { get; private set; }

        /// <summary>
        ///     Paints the position, with whatever squares the caller wants marked.
        /// </summary>
        /// <param name="board">The position to draw.</param>
        /// <param name="flipped">True to draw from Black's side.</param>
        /// <param name="marks">What to tint each square, or null for a plain board.</param>
        /// <returns>The finished board as pixels, ready for <see cref="AnsiImage.FromPixels" />.</returns>
        public PixelBuffer Compose(ChessBoard board, bool flipped, IReadOnlyDictionary<int, ChessSquareMarkEnum> marks)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

            var canvas = new PixelBuffer(BoardPixels, BoardPixels);

            for (var square = 0; square < 64; square++)
            {
                var file = ChessBoard.FileOf(square);
                var rank = ChessBoard.RankOf(square);

                // Rank 8 is drawn at the top, so the rank index is flipped on the way to pixels - and flipped back
                // again when the board is turned around for Black.
                var column = flipped ? 7 - file : file;
                var row = flipped ? rank : 7 - rank;

                var mark = ChessSquareMarkEnum.None;
                marks?.TryGetValue(square, out mark);

                canvas.Fill(column * SquarePixels, row * SquarePixels, SquarePixels, SquarePixels,
                    ColorFor((file + rank) % 2 != 0, mark));

                var piece = board[square];
                if (piece.IsEmpty || !_pieces.TryGetValue(KeyFor(piece), out var art))
                    continue;

                // The kings are 46 wide where everything else is 48, so pieces are centred rather than placed at
                // the corner - otherwise the two kings sit a pixel left of everything else, which is exactly the
                // kind of thing that looks "off" without anyone being able to say why.
                canvas.DrawImage(art,
                    column * SquarePixels + (SquarePixels - art.Width) / 2,
                    row * SquarePixels + (SquarePixels - art.Height) / 2);
            }

            return canvas;
        }

        /// <summary>What colour a square is drawn, given whether it is dark and what is marked on it.</summary>
        private static Rgba32 ColorFor(bool dark, ChessSquareMarkEnum mark)
        {
            var baseColor = dark ? _darkSquare : _lightSquare;
            var tint = mark switch
            {
                ChessSquareMarkEnum.Cursor => _cursorTint,
                ChessSquareMarkEnum.Selected => _selectedTint,
                ChessSquareMarkEnum.Target => _targetTint,
                ChessSquareMarkEnum.LastMove => _lastMoveTint,
                ChessSquareMarkEnum.Check => _checkTint,
                _ => baseColor
            };

            if (mark == ChessSquareMarkEnum.None)
                return baseColor;

            // Blended rather than replaced, so a marked dark square still reads as darker than a marked light one
            // and the board's chequer does not vanish under the highlighting.
            return new Rgba32(
                (byte) ((baseColor.R + tint.R * 3) / 4),
                (byte) ((baseColor.G + tint.G * 3) / 4),
                (byte) ((baseColor.B + tint.B * 3) / 4),
                0xFF);
        }

        /// <summary>The file name stem for a piece, which is how the artwork is named: "WKnight", "BPawn".</summary>
        private static string KeyFor(Piece piece) =>
            (piece.Color == PieceColorEnum.White ? "W" : "B") + piece.Kind;

        /// <summary>
        ///     Decodes the twelve PNGs once. Uses the strict path — <see cref="ImageDecoders" /> directly rather than
        ///     <see cref="AnsiImage.FromFile" /> — because a missing piece here should be reported as "fall back to
        ///     the text board", not drawn as a magenta checkerboard shaped like a knight.
        /// </summary>
        private void LoadPieces()
        {
            if (!Directory.Exists(Folder))
            {
                Error = $"{Folder} does not exist; the project file copies media there at build time.";
                return;
            }

            foreach (var color in new[] {"W", "B"})
            foreach (var kind in new[] {"Pawn", "Knight", "Bishop", "Rook", "Queen", "King"})
            {
                var name = color + kind;
                var path = Path.Combine(Folder, name + ".png");

                try
                {
                    using var stream = File.OpenRead(path);
                    _pieces[name] = ImageDecoders.Default.Decode(stream);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException
                                               or UnauthorizedAccessException)
                {
                    Error = $"{name}.png could not be read: {ex.Message}";
                    return;
                }
            }
        }
    }
}
