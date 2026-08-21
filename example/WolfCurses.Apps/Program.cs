// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Threading;
using WolfCurses.Graphics;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     Host for the WolfCurses applications. Where the arcade next door answers "what does a game built on this
    ///     look like?", this one asks the same question about the other half of what a terminal used to be for: an
    ///     editor, a spreadsheet, a card file, a calculator, a diary.
    ///     <para>
    ///         There is nothing on the menu yet but Quit, and the scaffolding is deliberately here first. Every
    ///         application added after this is a folder plus one <c>AddCommand</c> line in
    ///         <see cref="AppsWindow" />, and that claim is worth proving before any of them are written.
    ///     </para>
    /// </summary>
    internal static class Program
    {
        /// <summary>Main entry point for the application being startup.</summary>
        private static void Main()
        {
            Console.WriteLine("Starting...");
            Console.CancelKeyPress += Console_CancelKeyPress;

            // Guarded because both of these throw when there is no real console behind standard output, which is
            // exactly how anything driving this app for a screenshot or a test runs it. The library answers the
            // question for us rather than each host catching IOException itself.
            if (!AnsiConsole.SafeIsOutputRedirected())
            {
                Console.Title = "WolfCurses Apps";
                Console.CursorVisible = false;
            }

            // The same host loop as the two sibling examples, and the same three things it does not have to do: no
            // key reading (the simulation drains the console itself at the start of every tick), no drawing (the
            // scene graph presents each changed frame to this console, flicker-free), and no renderer set-up (the
            // SimulationApp constructor probes the terminal). An application overrides OnKeyPressed and returns a
            // string, and that is the entire contract.
            AppsSimulationApp.Create();

            // Asked for AFTER the app is built, because the SimulationApp constructor probes the terminal for a
            // graphics protocol and that probe reads standard input. Opt-in and host-owned on purpose: switching it
            // on takes click-drag text selection away from the user for as long as the program runs, so the library
            // never does it behind an application's back. It answers false on a terminal with no mouse to give, and
            // every application here is still fully driveable from the keyboard.
            AnsiConsole.EnableMouse();

            while (AppsSimulationApp.Instance != null)
            {
                AppsSimulationApp.Instance.OnTick(true);

                // Do not consume all of the CPU, allow other messages to occur. Note this sleep is why anything
                // real-time here should pace itself on an IntervalTimer rather than by counting ticks: Windows'
                // default timer granularity is about 15ms, so "one tick" is not a unit of time to build on.
                Thread.Sleep(1);
            }

            // Handed back before anything else. On a classic console host the mouse mode belongs to the WINDOW rather
            // than to this process, so exiting without restoring it leaves that window with the user's own text
            // selection switched off until they close and reopen it.
            AnsiConsole.DisableMouse();

            Console.Clear();
            Console.WriteLine("Goodbye!");
            Console.WriteLine("Press ANY KEY to close this window...");
            Console.ReadKey();
        }

        /// <summary>Fired when the user presses CTRL-C, which closes the simulation rather than the process.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The console cancel event arguments.</param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            AnsiConsole.DisableMouse();
            AppsSimulationApp.Instance?.Destroy();
            e.Cancel = true;
        }

        /// <summary>Forces the current simulation app to close and return control to the operating system.</summary>
        public static void Destroy()
        {
            AppsSimulationApp.Instance?.Destroy();
        }
    }
}
