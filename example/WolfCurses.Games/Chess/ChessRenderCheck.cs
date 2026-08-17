// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Draws a board to the console so the graphics pipeline can be looked at without a game running.
    ///     <para>
    ///         Worth having as its own command because the pipeline has four places to go wrong that all look the
    ///         same from inside the game — the artwork not being beside the executable, the decoder refusing it, the
    ///         compositing putting pieces in the wrong squares, and the renderer producing something the terminal
    ///         cannot show. This prints the board as shaded ASCII, which every terminal can display and which shows
    ///         up in a captured log, so the first three can be told apart from the fourth.
    ///         <c>dotnet run --project example/WolfCurses.Games -- board [fen]</c>.
    ///     </para>
    /// </summary>
    public static class ChessRenderCheck
    {
        /// <summary>Entry point for the <c>board</c> command line argument.</summary>
        /// <param name="args">Optionally a FEN to draw instead of the opening position.</param>
        /// <returns>0 when a board was drawn.</returns>
        public static int Run(string[] args)
        {
            var fen = args.Length > 0 ? string.Join(' ', args) : ChessBoard.StartingFen;

            // The text board is drawn with the Unicode chess pieces, and standard output has to be UTF-8 for them
            // to survive. A running game gets this from the frame presenter; a bare command has to ask.
            AnsiConsole.Enable();

            Console.WriteLine("WolfChess 5000 - render check");
            Console.WriteLine();

            var art = new ChessBoardArt();
            Console.WriteLine($"artwork folder : {art.Folder}");
            Console.WriteLine($"artwork loaded : {art.IsAvailable}{(art.Error == null ? "" : " - " + art.Error)}");

            if (!art.IsAvailable)
            {
                Console.WriteLine();
                Console.WriteLine("Falling back to the text board:");
                Console.WriteLine(ChessTextBoard.Render(new ChessBoard(fen), false, null));
                return 1;
            }

            var board = new ChessBoard(fen);
            var marks = new Dictionary<int, ChessSquareMarkEnum>
            {
                [ChessBoard.SquareAt(4, 3)] = ChessSquareMarkEnum.Cursor,
                [ChessBoard.SquareAt(3, 3)] = ChessSquareMarkEnum.Target
            };

            var pixels = art.Compose(board, false, marks);
            Console.WriteLine($"canvas         : {pixels.Width}x{pixels.Height} pixels");

            // Shaded ASCII with no colour at all, because this output has to survive being piped into a file.
            var options = new AnsiImageOptions
            {
                MaxColumns = 64,
                MaxRows = 32,
                ColorMode = AnsiColorModeEnum.None
            };

            var ansi = AnsiImage.FromPixels(pixels).ToAnsi(options, new HalfBlockImageRenderer());
            var rows = ansi.Split('\n').Length;
            Console.WriteLine($"rendered       : {rows} rows, {ansi.Length:N0} characters");
            Console.WriteLine();
            Console.WriteLine(ansi);
            Console.WriteLine();
            Console.WriteLine("The same position as text:");
            Console.WriteLine(ChessTextBoard.Render(board, false, marks));
            return 0;
        }
    }
}
