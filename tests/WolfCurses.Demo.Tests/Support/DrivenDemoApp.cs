using System;
using System.Text;
using WolfCurses.Demo;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Demo.Tests.Support
{
    /// <summary>
    ///     Runs the demo application headlessly and drives it through the library's published driver surface, the
    ///     same way <c>WolfCurses.Games.Tests</c> drives the arcade.
    /// </summary>
    public sealed class DrivenDemoApp : IDisposable
    {
        public DrivenDemoApp()
        {
            ConsoleSimulationApp.Create();
            App = ConsoleSimulationApp.Instance;
            App.InputManager.ReadsConsoleInput = false;

            WaitForFirstFrame();
        }

        /// <summary>The running simulation.</summary>
        public ConsoleSimulationApp App { get; }

        /// <summary>The last frame with the escapes taken out, for asserting on what is visible.</summary>
        public string Screen => AnsiText.StripEscapes(App.SceneGraph.ScreenBuffer);

        /// <summary>The last frame exactly as the terminal would receive it.</summary>
        public string RawScreen => App.SceneGraph.ScreenBuffer;

        /// <summary>Types a line and presses ENTER.</summary>
        /// <param name="text">What to type.</param>
        public void Type(string text)
        {
            foreach (var character in text ?? string.Empty)
                App.InputManager.SendConsoleKey(new ConsoleKeyInfo(character, ConsoleKey.NoName, false, false, false));

            App.InputManager.SendConsoleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
            App.PumpInput();
        }

        /// <summary>Presses a key with no character, such as ESC or TAB.</summary>
        /// <param name="key">The key to press.</param>
        public void Press(ConsoleKey key)
        {
            App.InputManager.SendConsoleKey(new ConsoleKeyInfo((char) 0, key, false, false, false));
            App.PumpInput();
        }

        /// <summary>Ticks the simulation.</summary>
        /// <param name="ticks">How many system ticks to run.</param>
        public void Tick(int ticks = 1)
        {
            for (var i = 0; i < ticks; i++)
                App.OnTick(true);

            App.PumpInput();
        }

        /// <summary>Dismisses the start-up splash, which every other screen is behind.</summary>
        public void DismissSplash()
        {
            Type(string.Empty);
        }

        /// <summary>Opens a menu item by number, from the menu.</summary>
        /// <param name="item">The item number.</param>
        public void ChooseMenuItem(int item)
        {
            Type(item.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ConsoleSimulationApp.Instance?.Destroy();
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

        /// <summary>
        ///     Ticks, with real time passing, until the app has a window.
        ///     <para>
        ///         The wait is unavoidable: <c>OnFirstTick</c> is where the window is attached and it fires on the
        ///         first <i>simulation</i> tick, which is gated on a fixed second of real elapsed time. Spinning
        ///         <c>OnTick(true)</c> produces no simulation tick and therefore no window at all.
        ///     </para>
        /// </summary>
        private void WaitForFirstFrame()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            while (App.WindowManager.FocusedWindow == null && clock.Elapsed < TimeSpan.FromSeconds(10))
            {
                App.OnTick(true);
                if (App.WindowManager.FocusedWindow == null)
                    System.Threading.Thread.Sleep(10);
            }

            Assert.True(App.WindowManager.FocusedWindow != null, "the app never attached a window");

            // Modules tick input, scene, window - so a window attached this tick is drawn on the next one.
            App.OnTick(true);
            App.PumpInput();
        }
    }
}
