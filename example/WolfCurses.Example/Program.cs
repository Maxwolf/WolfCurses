// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:10 PM

using System;
using System.Threading;
using WolfCurses.Graphics;

namespace WolfCurses.Example
{
    /// <summary>
    ///     Example console application using wolf curses library to power interaction.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        ///     Draws frames without flicker: rows are overwritten in place (never blanked first), only changed rows are
        ///     rewritten, and each update goes out as a single write. Its constructor also enables ANSI escape handling
        ///     and a UTF-8 output encoding so images (and any other colored output) render correctly, especially on
        ///     Windows where virtual-terminal processing is off by default.
        /// </summary>
        private static readonly ConsolePresenter _presenter = new ConsolePresenter();

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
            // PNG, JPEG and GIF itself, so the logo splash and every picture in this app load with no set-up line at
            // all, and the package still has no dependencies. This app is the proof — every image it shows goes
            // through the built-in decoders.
            //
            // Swapping in something else is one line, and StbImageDecoder next door is a complete example of the
            // thirty-line adapter it takes to wrap an imaging library you already have. Uncomment to use it here:
            //
            //     ImageDecoders.Default = new StbImageDecoder();
            //
            // Worth doing when you need a format outside those three (WebP, TIFF), when decode speed turns out to
            // matter, or simply so one process is not decoding images two different ways.

            // Ask the terminal whether it can draw real pixels, and if so draw every image that way from here on.
            // This must happen before the key-reading loop below starts: the probe writes a question to the terminal
            // and reads the answer back off standard input, so if both were running the loop would read the terminal's
            // reply as typed input and the probe could swallow a real keystroke. It is also before the simulation is
            // created, so the logo splash it puts up already benefits. Whatever the terminal says (including nothing
            // at all, or that it is a plain console), the result is always a renderer that works here.
            ImageRenderers.Default = ImageRenderers.For(AnsiConsole.ProbeGraphicsProtocol());

            // Entry point for the entire simulation.
            ConsoleSimulationApp.Create();

            // Hook event to know when screen buffer wants to redraw the entire console screen.
            ConsoleSimulationApp.Instance.SceneGraph.ScreenBufferDirtyEvent += Simulation_ScreenBufferDirtyEvent;

            // Prevent console session from closing.
            while (ConsoleSimulationApp.Instance != null)
            {
                // Input first, then the simulation that acts on it, then round again. Reading the keys after the tick
                // instead would leave everything typed during it waiting a whole turn of this loop before anything
                // looked at it, which is a delay nobody can do anything about later.
                //
                // WHILE, not IF, and the difference is the entire feel of anything being steered. Windows repeats a
                // held key about thirty times a second and puts every one in the console's own buffer; taking one per
                // turn means the buffer only empties as fast as this loop spins. It usually does not matter, because a
                // loop with nothing to redraw spins far faster than a person types. But drawing costs what it costs,
                // and once a turn is slower than a thirtieth of a second the buffer fills faster than it drains and the
                // backlog is permanent: let go of the arrow key and the sprite keeps going, for as long as it takes to
                // pay off what has piled up. Draining it here means what is left to process is only ever what has
                // actually been pressed since the last turn, so releasing a key stops the sprite on the next one.
                while (Console.KeyAvailable)
                {
                    // GetModule the key that was pressed, without printing it to console.
                    var key = Console.ReadKey(true);

                    // If enter is pressed, pass whatever we have to simulation.
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (key.Key)
                    {
                        case ConsoleKey.Enter:
                            ConsoleSimulationApp.Instance.InputManager.SendInputBufferAsCommand();
                            break;
                        case ConsoleKey.Backspace:
                            ConsoleSimulationApp.Instance.InputManager.RemoveLastCharOfInputBuffer();
                            break;
                        default:
                            // Both, and they are not the same thing. The input buffer takes text, so it is offered the
                            // character and quietly ignores anything that is not printable. An arrow key has no
                            // character at all, so without the second line it would simply be dropped and nothing could
                            // ever be steered; reporting the key press as well is what lets a form hear one. Windows and
                            // forms that do not care override nothing and pay nothing.
                            ConsoleSimulationApp.Instance.InputManager.AddCharToInputBuffer(key.KeyChar);
                            ConsoleSimulationApp.Instance.InputManager.SendKeyPress(key.Key);
                            break;
                    }
                }

                // Simulation takes any numbers of pulses to determine seconds elapsed. Everything read above is waiting
                // in the input manager and gets handed on during this call.
                ConsoleSimulationApp.Instance.OnTick(true);

                // Do not consume all of the CPU, allow other messages to occur.
                Thread.Sleep(1);
            }

            // Make user press any key to close out the simulation completely, this way they know it closed without error.
            Console.Clear();
            Console.WriteLine("Goodbye!");
            Console.WriteLine("Press ANY KEY to close this window...");
            Console.ReadKey();
        }

        /// <summary>Write all text from objects to screen.</summary>
        /// <param name="tuiContent">The text user interface content.</param>
        private static void Simulation_ScreenBufferDirtyEvent(string tuiContent)
        {
            _presenter.Present(tuiContent);
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
            ConsoleSimulationApp.Instance.Destroy();
        }
    }
}