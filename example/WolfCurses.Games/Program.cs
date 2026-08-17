// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Threading;
using WolfCurses.Graphics;

namespace WolfCurses.Games
{
    /// <summary>
    ///     Host for the WolfCurses games. A handful of small, complete arcade games, each written to lean on a
    ///     different part of the library, so "what does this look like in a real interactive program?" has an answer
    ///     shorter than a real program.
    /// </summary>
    internal static class Program
    {
        /// <summary>Main entry point for the application being startup.</summary>
        /// <param name="args">
        ///     Command line arguments. <c>perft [depth]</c> runs the chess move generator against published node
        ///     counts and exits without starting the interface — a check that belongs with the rules rather than in
        ///     the library's test project, which testing an example app would point the wrong way round.
        /// </param>
        /// <returns>The process exit code.</returns>
        private static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "perft", StringComparison.OrdinalIgnoreCase))
                return Chess.ChessPerft.Run(args[1..]);

            if (args.Length > 0 && string.Equals(args[0], "rules", StringComparison.OrdinalIgnoreCase))
                return Chess.ChessRulesCheck.Run();

            if (args.Length > 0 && string.Equals(args[0], "bot", StringComparison.OrdinalIgnoreCase))
                return Chess.ChessBotCheck.Run();

            if (args.Length > 0 && string.Equals(args[0], "board", StringComparison.OrdinalIgnoreCase))
                return Chess.ChessRenderCheck.Run(args[1..]);

            Console.WriteLine("Starting...");
            Console.CancelKeyPress += Console_CancelKeyPress;

            // Guarded because both of these throw when there is no real console behind standard output — which is
            // exactly how anything driving this app for a screenshot or a test runs it. The library answers the
            // question for us rather than each host catching IOException itself.
            if (!AnsiConsole.SafeIsOutputRedirected())
            {
                Console.Title = "WolfCurses Games";
                Console.CursorVisible = false;
            }

            // The whole host loop, and everything it does not have to do, is the same as WolfCurses.Example's: the
            // simulation reads the keyboard itself at the start of each tick, and presents each changed frame to this
            // console itself, flicker-free. A game needs neither a key loop nor a draw call here — it overrides
            // OnKeyPressed and returns a string, and that is the entire contract.
            GamesSimulationApp.Create();

            // Asked for AFTER the app is built, because the SimulationApp constructor probes the terminal for a
            // graphics protocol and that probe reads standard input. Opt-in and host-owned on purpose: enabling it
            // takes click-drag text selection away from the user and swaps the console read path, so the library
            // never does it behind an application's back. It answers false on a terminal that has no mouse to give,
            // and every game here still plays exactly as it did from the keyboard.
            AnsiConsole.EnableMouse();

            while (GamesSimulationApp.Instance != null)
            {
                GamesSimulationApp.Instance.OnTick(true);

                // Do not consume all of the CPU, allow other messages to occur. Note this sleep is why the games
                // below pace themselves on a Stopwatch rather than by counting ticks: Windows' default timer
                // granularity is about 15ms, so "one tick" is not a unit of time anyone should build a game on.
                Thread.Sleep(1);
            }

            // Handed back before anything else. On a classic console host the mouse mode belongs to the WINDOW
            // rather than to this process, so a program that exits without restoring it leaves that window with the
            // user's own text selection switched off until they close and reopen it.
            AnsiConsole.DisableMouse();

            Console.Clear();
            Console.WriteLine("Thanks for playing!");
            Console.WriteLine("Press ANY KEY to close this window...");
            Console.ReadKey();
            return 0;
        }

        /// <summary>Fired when the user presses CTRL-C, which closes the simulation rather than the process.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The console cancel event arguments.</param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            AnsiConsole.DisableMouse();
            GamesSimulationApp.Instance?.Destroy();
            e.Cancel = true;
        }

        /// <summary>Forces the current simulation app to close and return control to the operating system.</summary>
        public static void Destroy()
        {
            GamesSimulationApp.Instance.Destroy();
        }
    }
}
