// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Threading;

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
        private static void Main()
        {
            Console.Title = "WolfCurses Games";
            Console.WriteLine("Starting...");
            Console.CursorVisible = false;
            Console.CancelKeyPress += Console_CancelKeyPress;

            // The whole host loop, and everything it does not have to do, is the same as WolfCurses.Example's: the
            // simulation reads the keyboard itself at the start of each tick, and presents each changed frame to this
            // console itself, flicker-free. A game needs neither a key loop nor a draw call here — it overrides
            // OnKeyPressed and returns a string, and that is the entire contract.
            GamesSimulationApp.Create();

            while (GamesSimulationApp.Instance != null)
            {
                GamesSimulationApp.Instance.OnTick(true);

                // Do not consume all of the CPU, allow other messages to occur. Note this sleep is why the games
                // below pace themselves on a Stopwatch rather than by counting ticks: Windows' default timer
                // granularity is about 15ms, so "one tick" is not a unit of time anyone should build a game on.
                Thread.Sleep(1);
            }

            Console.Clear();
            Console.WriteLine("Thanks for playing!");
            Console.WriteLine("Press ANY KEY to close this window...");
            Console.ReadKey();
        }

        /// <summary>Fired when the user presses CTRL-C, which closes the simulation rather than the process.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The console cancel event arguments.</param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
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
