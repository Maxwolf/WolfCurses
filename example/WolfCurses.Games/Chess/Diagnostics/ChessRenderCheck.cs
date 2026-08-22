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
    ///         up in a captured log, so the first three can be told apart from the fourth. The fourth is then named
    ///         outright rather than left to be inferred, because it is the only one of the four a user cannot see
    ///         for themselves: a payload the terminal refuses looks exactly like a board that was never drawn.
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

            // Normally the SimulationApp constructor does this, and this command never builds one, so without it the
            // report below would describe the half blocks every process starts with rather than what the game would
            // get here. The probe reads the terminal's reply off standard input, so it has to happen before anything
            // else reads a key: Program dispatches the "board" verb and returns long before the simulation exists,
            // which is the only reason calling it from a command is safe.
            ImageRenderers.AutoDetect();

            Console.WriteLine("WolfChess 5000 - render check");
            Console.WriteLine();

            // The fourth way the pipeline goes wrong is the one the user cannot see for themselves: a payload the
            // terminal will not draw looks exactly like a board that was never composited. Nothing below can test
            // that from inside a piped command, so it is reported instead - which renderer the terminal actually
            // got, what shape of pixels it wants, and whether a true-pixel payload would survive to the screen at
            // all. SupportsPictures is the clause that reads false with virtual-terminal processing off, where
            // ConsolePresenter strips every escape from a row and writes a picture out blank.
            //
            // The reference has to stay IImageRenderer-typed: Name, DrawsTruePixels and the two cell sizes are
            // default interface members, so narrowing this to a concrete renderer stops compiling rather than
            // quietly reporting something else.
            var renderer = ImageRenderers.Default;
            Console.WriteLine($"graphics       : {AnsiConsole.DetectGraphicsProtocol()}");
            Console.WriteLine(
                $"renderer       : {renderer.Name} - {(renderer.DrawsTruePixels ? "true pixels" : "character cells")}");
            Console.WriteLine($"cell pixels    : {renderer.CellPixelWidth}x{renderer.CellPixelHeight} per cell");
            Console.WriteLine(
                $"pictures       : {(AnsiConsole.SupportsPictures() ? "yes" : "no - a payload row would be written out blank")}");

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
            //
            // So the renderer REPORTED above and the renderer DRAWN with here are deliberately different, and that is
            // not an oversight to tidy up: the report has to name whatever the terminal actually got, while the
            // drawing has to be something a log file can hold. Handing this ImageRenderers.Default would put a sixel
            // or kitty payload in the capture, which is unreadable in a file and unreadable again on the next
            // terminal somebody pastes it into - and the picture would then be the one thing in the report nobody
            // could check.
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
