using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Apps.Tests.Support
{
    /// <summary>
    ///     Runs the real suite headlessly and drives it the way a person would: keys in, frames out.
    ///     <para>
    ///         Everything here goes through the library's published driver surface,
    ///         <c>InputManager.SendConsoleKey</c>, <c>SimulationApp.PumpInput</c> and
    ///         <c>SceneGraph.ScreenBuffer</c>, which exists for exactly this. Nothing reaches into a form to poke at
    ///         its fields, so a test that passes here is a statement about what the application does rather than
    ///         about how it is built.
    ///     </para>
    ///     <para>
    ///         <b>Why form discovery works from a different assembly.</b> <c>FormFactory</c> scans
    ///         <c>Assembly.GetEntryAssembly()</c>, which under xunit.v3 is <i>this test assembly</i> and not the
    ///         suite. Forms are found anyway because the discovery set also includes the <c>SimulationApp</c>
    ///         subclass's own assembly, which is the suite. If that ever regresses, every test here fails at once
    ///         with "no such window", which is signal enough not to need a canary of its own.
    ///     </para>
    ///     <para>
    ///         <b>Dispose matters.</b> <c>AppsSimulationApp</c> is a singleton that refuses to be created twice, so
    ///         a test that leaks one fails every test after it. Hence <c>using</c> at every call site and the
    ///         non-parallel collection, see <see cref="AppsAppCollection" />.
    ///     </para>
    /// </summary>
    public sealed class DrivenAppsApp : IDisposable
    {
        public DrivenAppsApp()
        {
            AppsSimulationApp.Create();
            App = AppsSimulationApp.Instance;

            // The suite would otherwise try to read the console, which a test host does not have.
            App.InputManager.ReadsConsoleInput = false;

            WaitForFirstFrame();
        }

        /// <summary>The running simulation.</summary>
        public AppsSimulationApp App { get; }

        /// <summary>The last frame with the escapes taken out, for asserting on what is visible.</summary>
        public string Screen => AnsiText.StripEscapes(App.SceneGraph.ScreenBuffer);

        /// <summary>The last frame exactly as the terminal would receive it.</summary>
        public string RawScreen => App.SceneGraph.ScreenBuffer;

        /// <summary>
        ///     The frame without its first row, which is the only honest way to assert that a screen did not change:
        ///     the scene graph's status line carries a spinner that advances on every tick, so the whole frame
        ///     differs from one tick to the next whatever the window did.
        /// </summary>
        public string ScreenBelowStatusLine
        {
            get
            {
                var rows = Screen.Split('\n');
                return rows.Length < 2 ? string.Empty : string.Join('\n', rows, 1, rows.Length - 1);
            }
        }

        /// <summary>Types a line and presses ENTER, then lets the simulation settle.</summary>
        /// <param name="text">What to type; empty submits a bare ENTER.</param>
        public void Type(string text)
        {
            foreach (var character in text ?? string.Empty)
                App.InputManager.SendConsoleKey(new ConsoleKeyInfo(character, ConsoleKey.NoName, false, false, false));

            App.InputManager.SendConsoleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
            App.PumpInput();
        }

        /// <summary>Presses a key that has no character, such as an arrow, TAB or ESC.</summary>
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

        /// <summary>Chooses a menu item by its number and settles.</summary>
        /// <param name="item">The menu number to type; take it from the enum, never a literal.</param>
        public void ChooseMenuItem(int item)
        {
            Type(item.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Backs out of whatever application is showing, which the suite binds to ESC.</summary>
        public void Escape()
        {
            Press(ConsoleKey.Escape);
        }

        /// <summary>The screen, indented, for a failure message.</summary>
        /// <returns>The formatted screen.</returns>
        public string Describe()
        {
            var sb = new StringBuilder();
            foreach (var row in Screen.Split('\n'))
                sb.Append("    ").AppendLine(row.TrimEnd('\r'));

            return sb.ToString();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            AppsSimulationApp.Instance?.Destroy();
        }

        /// <summary>
        ///     Ticks, with real time passing, until the suite has a window.
        ///     <para>
        ///         <b>The sleep is not padding and cannot be tuned away.</b> <c>OnFirstTick</c>, which is where the
        ///         window is attached, fires on the first <i>simulation</i> tick, and those are gated on
        ///         <c>TICK_INTERVAL</c>, a fixed second of real elapsed time. Spinning <c>OnTick(true)</c> ten
        ///         thousand times in a microsecond produces no simulation tick, no window and an empty
        ///         <c>ScreenBuffer</c>. The interval is deliberately not configurable, so a driver has to wait it
        ///         out; this loop gives up the moment a frame lands rather than sleeping a flat second.
        ///     </para>
        /// </summary>
        private void WaitForFirstFrame()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            // Waits on STATE, "is there a window yet", rather than on any words being on screen. Waiting for text
            // costs the whole timeout every time the phrase turns out not to be there yet.
            while (App.WindowManager.FocusedWindow == null && clock.Elapsed < TimeSpan.FromSeconds(10))
            {
                App.OnTick(true);
                if (App.WindowManager.FocusedWindow == null)
                    System.Threading.Thread.Sleep(10);
            }

            // One more tick so the scene graph renders the window the loop above attached: modules run in the order
            // input, scene, window, so a window created this tick is drawn on the next one.
            App.OnTick(true);
            App.PumpInput();
        }
    }
}
