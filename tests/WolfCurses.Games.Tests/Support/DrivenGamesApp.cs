using System;
using System.Text;
using WolfCurses.Games;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Tests.Support
{
    /// <summary>
    ///     Runs the real arcade headlessly and drives it the way a person would: keys in, frames out.
    ///     <para>
    ///         Everything here goes through the library's published driver surface —
    ///         <c>InputManager.SendConsoleKey</c>, <c>SimulationApp.PumpInput</c>, <c>SceneGraph.ScreenBuffer</c> —
    ///         which exists for exactly this. Nothing reaches into a form to poke at its fields, so a test that
    ///         passes here is a statement about what the application does rather than about how it is built.
    ///     </para>
    ///     <para>
    ///         <b>Why the discovery works from a different assembly.</b> <c>FormFactory</c> scans
    ///         <c>Assembly.GetEntryAssembly()</c>, which under xunit.v3 is <i>this test assembly</i> and not the
    ///         game. The forms are found anyway because the discovery set also includes the <c>SimulationApp</c>
    ///         subclass's own assembly, which is the game — the same mechanism that lets a host embed the library.
    ///         If that ever regresses, every test here fails at once with "no such window", which is a clear enough
    ///         signal not to need a canary of its own.
    ///     </para>
    ///     <para>
    ///         <b>Dispose matters.</b> <c>GamesSimulationApp</c> is a singleton that refuses to be created twice, so
    ///         a test that leaks one fails every test after it. Hence <c>using</c> at every call site and the
    ///         non-parallel collection — see <see cref="GamesAppCollection" />.
    ///     </para>
    /// </summary>
    public sealed class DrivenGamesApp : IDisposable
    {
        private const char EscapeCharacter = '';

        public DrivenGamesApp()
        {
            GamesSimulationApp.Create();
            App = GamesSimulationApp.Instance;

            // The app would otherwise try to read the console, which a test host does not have.
            App.InputManager.ReadsConsoleInput = false;

            WaitForFirstFrame();
        }

        /// <summary>
        ///     Ticks, with real time passing, until the first frame exists.
        ///     <para>
        ///         <b>The sleep is not padding and cannot be tuned away.</b> <c>OnFirstTick</c> — which is where the
        ///         app attaches its window — fires on the first <i>simulation</i> tick, and those are gated on
        ///         <c>TICK_INTERVAL</c>, a fixed second of real elapsed time. Spinning <c>OnTick(true)</c> ten
        ///         thousand times in a microsecond produces no simulation tick, no window, and an empty
        ///         <c>ScreenBuffer</c>, which is what every screen test here failed on first. The interval is
        ///         deliberately not configurable, so a driver has to wait it out; this loop gives up the moment a
        ///         frame lands rather than sleeping a flat second.
        ///     </para>
        /// </summary>
        private void WaitForFirstFrame()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            // Waits on STATE - "is there a window yet" - rather than on any words being on screen. Waiting for text
            // costs the whole timeout every time the phrase turns out not to be there: the arcade's prompt is only
            // set when a form is cleared, so it does not exist at start-up, and waiting for it took this file from
            // nine seconds to two minutes.
            while (App.WindowManager.FocusedWindow == null && clock.Elapsed < TimeSpan.FromSeconds(10))
            {
                App.OnTick(true);
                if (App.WindowManager.FocusedWindow == null)
                    System.Threading.Thread.Sleep(10);
            }

            // One more tick so the scene graph renders the window the tick above attached - the modules run in the
            // order input, scene, window, so a window created this tick is drawn on the next one.
            App.OnTick(true);
            App.PumpInput();
        }

        /// <summary>The running simulation.</summary>
        public GamesSimulationApp App { get; }

        /// <summary>The last frame, with the ANSI escapes taken out so a test can assert on what is visible.</summary>
        public string Screen => AnsiText.StripEscapes(App.SceneGraph.ScreenBuffer);

        /// <summary>The last frame exactly as it would go to the terminal, escapes and all.</summary>
        public string RawScreen => App.SceneGraph.ScreenBuffer;

        /// <summary>Types a line and presses ENTER, then lets the simulation settle.</summary>
        /// <param name="text">What to type; empty submits a bare ENTER.</param>
        public void Type(string text)
        {
            foreach (var character in text ?? string.Empty)
                App.InputManager.SendConsoleKey(new ConsoleKeyInfo(character, ConsoleKey.NoName, false, false, false));

            App.InputManager.SendConsoleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
            App.PumpInput();
        }

        /// <summary>Presses a key that has no character — an arrow, TAB, ESC — and lets the simulation settle.</summary>
        /// <param name="key">The key to press.</param>
        public void Press(ConsoleKey key)
        {
            App.InputManager.SendConsoleKey(new ConsoleKeyInfo((char) 0, key, false, false, false));
            App.PumpInput();
        }

        /// <summary>Presses a printable key, which reaches both the input buffer and the focused form.</summary>
        /// <param name="character">The character to send.</param>
        /// <param name="key">The key it arrives as.</param>
        public void PressChar(char character, ConsoleKey key)
        {
            App.InputManager.SendConsoleKey(new ConsoleKeyInfo(character, key, false, false, false));
            App.PumpInput();
        }

        /// <summary>Ticks a fixed number of times, for a screen that advances on its own clock.</summary>
        /// <param name="ticks">How many system ticks to run.</param>
        public void Tick(int ticks = 1)
        {
            for (var i = 0; i < ticks; i++)
                App.OnTick(true);

            App.PumpInput();
        }

        /// <summary>
        ///     Ticks until the visible screen contains <paramref name="phrase" />, or gives up.
        /// </summary>
        /// <param name="phrase">What to wait for.</param>
        /// <param name="maxTicks">How many ticks to allow before giving up.</param>
        /// <returns>True when the phrase appeared.</returns>
        public bool TickUntil(string phrase, int maxTicks = 20_000)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                App.OnTick(true);
                if (Screen.Contains(phrase, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>The longest a single tick took while waiting, which is what an unanswered keystroke waits behind.</summary>
        /// <param name="phrase">What to wait for.</param>
        /// <param name="maxTicks">How many ticks to allow.</param>
        /// <returns>Whether it appeared, and the worst single tick.</returns>
        public (bool Found, TimeSpan WorstTick) TickUntilTimed(string phrase, int maxTicks = 20_000)
        {
            var worst = TimeSpan.Zero;

            for (var i = 0; i < maxTicks; i++)
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                App.OnTick(true);
                var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
                if (elapsed > worst)
                    worst = elapsed;

                if (Screen.Contains(phrase, StringComparison.Ordinal))
                    return (true, worst);
            }

            return (false, worst);
        }

        /// <summary>How many escape sequences the last frame carried, for asserting that colour happened at all.</summary>
        public int EscapeCount
        {
            get
            {
                var count = 0;
                foreach (var character in RawScreen)
                {
                    if (character == EscapeCharacter)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Chooses a menu item by its number and settles.</summary>
        /// <param name="item">The menu number to type.</param>
        public void ChooseMenuItem(int item)
        {
            Type(item.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Backs out of whatever demo is showing, which the arcade binds to ESC.</summary>
        public void Escape()
        {
            Press(ConsoleKey.Escape);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            GamesSimulationApp.Instance?.Destroy();
        }

        /// <summary>Builds a StringBuilder of the screen for a failure message.</summary>
        /// <returns>The screen, indented, for pasting into an assertion.</returns>
        public string Describe()
        {
            var sb = new StringBuilder();
            foreach (var row in Screen.Split('\n'))
                sb.Append("    ").AppendLine(row.TrimEnd('\r'));

            return sb.ToString();
        }
    }
}
