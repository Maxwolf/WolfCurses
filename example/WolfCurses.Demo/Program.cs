// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:10 PM

using System;
using System.Threading;
using WolfCurses.Graphics;

namespace WolfCurses.Demo
{
    /// <summary>
    ///     Demo console application using wolf curses library to power interaction.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        ///     Main entry point for the application being startup.
        /// </summary>
        private static void Main()
        {
            // Create console with title, no cursor, make CTRL-C act as input.
            Console.Title = "WolfCurses Console Application";
            Console.WriteLine("Starting...");
            Console.CursorVisible = false;
            Console.CancelKeyPress += Console_CancelKeyPress;

            // Nothing here teaches the library to read image files, deliberately: it already knows. WolfCurses decodes
            // PNG, JPEG and GIF itself, so every picture in this app loads with no set-up line at all, and the package
            // still has no dependencies. This app is the proof — every image it shows goes through the built-in
            // decoders.
            //
            // Swapping in something else is one line, and StbImageDecoder next door is a complete example of the
            // thirty-line adapter it takes to wrap an imaging library you already have. Uncomment to use it here:
            //
            //     ImageDecoders.Default = new StbImageDecoder();
            //
            // Worth doing when you need a format outside those three (WebP, TIFF), when decode speed turns out to
            // matter, or simply so one process is not decoding images two different ways.

            // Nothing here picks how images are drawn either, for the same reason. Creating the simulation below
            // asks the terminal, once, whether it can draw real pixels (sixel or kitty) and routes every image
            // through the best answer, falling back to character cells when the answer is nothing special — so this
            // app gets true-pixel pictures on Windows Terminal 1.22+ without a line of set-up. The one rule is that
            // the question must be asked before the key-reading loop below starts (the terminal answers on standard
            // input, where the loop would read the reply as typed keys), and creating the simulation first satisfies
            // that naturally. To overrule the terminal's answer, assign ImageRenderers.Default yourself — a choice
            // made before this line is respected (detection stands down), and one made after simply replaces it.

            // Entry point for the entire simulation.
            ConsoleSimulationApp.Create();

            // Asked for AFTER the app is built, because the SimulationApp constructor probes the terminal for a
            // graphics protocol and that probe reads standard input. Opt-in and host-owned on purpose: enabling it
            // takes click-drag text selection away from the user and swaps the console read path, so the library
            // never does it behind an application's back. It answers false on a terminal that has no mouse to give,
            // and every demo here still works exactly as it did from the keyboard.
            //
            // Nothing here asks for mouse MOTION (InputManager.ReportsMouseMotion), which the library leaves off by
            // default because motion is one event per cell the pointer crosses. It stays opt-in per screen, so a
            // demo that wants a drawn pointer or a drag turns it on for itself.
            AnsiConsole.EnableMouse();

            // Nothing draws the frames here, and that is the third deliberate absence: whenever a frame changes, the
            // scene graph presents it to this console itself — flicker-free, only changed rows rewritten, one write
            // per update. Subscribing to SceneGraph.ScreenBufferDirtyEvent is how a host that wants to draw frames
            // its own way (or somewhere else entirely) takes that job over; while any handler is attached, the
            // built-in presenter stands down.

            // Prevent console session from closing.
            while (ConsoleSimulationApp.Instance != null)
            {
                // No key-reading loop either, which completes the set: each tick, the input manager drains every key
                // waiting in the console's buffer itself — ENTER submits the typed command, BACKSPACE edits it, any
                // other key both fills the prompt and reaches the focused form — and it does so before dispatching or
                // drawing anything, so a key is acted on the very turn it arrives. A host with its own ideas about
                // keys sets InputManager.ReadsConsoleInput = false and hands them to InputManager.SendConsoleKey (the
                // identical routing) from wherever, and whenever, it likes.
                ConsoleSimulationApp.Instance.OnTick(true);

                // Do not consume all of the CPU, allow other messages to occur.
                Thread.Sleep(1);
            }

            // Handed back before anything else. On a classic console host the mouse mode belongs to the WINDOW rather
            // than to this process, so a program that exits without restoring it leaves that window with the user's
            // own text selection switched off until they close and reopen it. This host makes that worse than the two
            // beside it by staying alive afterwards: the goodbye prompt below waits on Console.ReadKey, so without
            // this line the user sits at a prompt unable to select the text on it.
            AnsiConsole.DisableMouse();

            // Make user press any key to close out the simulation completely, this way they know it closed without error.
            Console.Clear();
            Console.WriteLine("Goodbye!");
            Console.WriteLine("Press ANY KEY to close this window...");
            Console.ReadKey();
        }

        /// <summary>
        ///     Fired when the user presses CTRL-C on their keyboard, this is only relevant to operating system tick and this view
        ///     of simulation. If moved into another framework like game engine this statement would be removed and just destroy
        ///     the simulation when the engine is destroyed using its overrides.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            // Restored first, because CTRL-C at the goodbye prompt is the one exit that runs none of the code above.
            AnsiConsole.DisableMouse();

            // Destroy the simulation, unless it is already gone (CTRL-C pressed at the goodbye prompt).
            ConsoleSimulationApp.Instance?.Destroy();

            // Stop the operating system from killing the entire process.
            e.Cancel = true;
        }

        /// <summary>
        ///     Forces the current simulation app to close and return control to underlying operating system.
        /// </summary>
        public static void Destroy()
        {
            // Null-conditional because CTRL-C runs on its own thread and OnPreDestroy nulls Instance, so a CTRL-C
            // landing while a menu Quit is queued reaches this line with nothing left to destroy and throws out of
            // the tick loop. SimulationApp.Destroy is already idempotent (it returns at once when IsClosing), so the
            // guard can only ever skip a call that would have thrown rather than one that would have done work.
            ConsoleSimulationApp.Instance?.Destroy();
        }
    }
}