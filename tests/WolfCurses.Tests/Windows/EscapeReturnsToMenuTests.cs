// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/21/2026

using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    /// <summary>
    ///     Pins the window-level Escape-to-menu pattern the example app is built on (its <c>ExampleWindow</c> overrides
    ///     <c>OnKeyPressed</c> exactly this way): a window that intercepts ESC and calls <c>ClearForm</c> backs out of
    ///     whatever form is showing, all the way through the real key-dispatch chain
    ///     (<c>SendKeyPress</c> / <c>SendConsoleKey</c> -> tick -> <c>OnKeyPressed(ConsoleKeyInfo)</c> ->
    ///     <c>OnKeyPressed(ConsoleKey)</c> override). If ESC were ever consumed as buffer control the way ENTER and
    ///     BACKSPACE are, the example's feature would silently break and dead-code the override — this is what catches
    ///     that.
    /// </summary>
    public class EscapeReturnsToMenuTests
    {
        /// <summary>The escape character (0x1B) the console reports as a key's <see cref="ConsoleKeyInfo.KeyChar" />.</summary>
        private const char EscapeChar = (char) 27;

        private static (AutoDiscoverySimulationApp app, EscapeReturnsToMenuWindow window) NewWindow()
        {
            // AutoDiscoverySimulationApp (no curated AllowedWindows) reflection-discovers this window; a curated app
            // such as TestSimulationApp would not list it.
            var app = new AutoDiscoverySimulationApp();
            app.WindowManager.Add(typeof(EscapeReturnsToMenuWindow));
            return (app, (EscapeReturnsToMenuWindow) app.WindowManager.FocusedWindow);
        }

        [Fact]
        public void Escape_WhileAFormIsShowing_ClearsItAndReturnsToTheBareMenu()
        {
            var (app, window) = NewWindow();
            window.SetForm(typeof(EscapeDismissableForm));
            Assert.NotNull(window.CurrentForm);

            app.InputManager.SendKeyPress(ConsoleKey.Escape);
            app.OnTick(false);

            // The form is gone but the window remains focused — that is exactly "back at the main menu".
            Assert.Null(window.CurrentForm);
            Assert.Same(window, app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Escape_ArrivesThroughTheConsolePath_WithoutPollutingTheInputBuffer()
        {
            // The real path: the console reports ESC (KeyChar 0x1B) and InputManager routes it via SendConsoleKey.
            // Being a control character it is dropped from the text buffer, and it is not ENTER/BACKSPACE, so it is
            // reported as a key press — which the window turns into "back out".
            var (app, window) = NewWindow();
            window.SetForm(typeof(EscapeDismissableForm));

            app.InputManager.SendConsoleKey(new ConsoleKeyInfo(EscapeChar, ConsoleKey.Escape, false, false, false));
            app.OnTick(false);

            Assert.Null(window.CurrentForm);
            Assert.Equal(string.Empty, app.InputManager.InputBuffer);

            app.Destroy();
        }

        [Fact]
        public void AKeyThatIsNotEscape_StillReachesTheForm()
        {
            // The override intercepts only ESC; every other key must pass through to the form, or the arrow-driven
            // demos (the sprite tests) would stop steering.
            var (app, window) = NewWindow();
            window.SetForm(typeof(EscapeDismissableForm));
            var form = (EscapeDismissableForm) window.CurrentForm;

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);

            Assert.NotNull(window.CurrentForm);
            Assert.Equal(new[] {ConsoleKey.LeftArrow}, form.ReceivedKeys);

            app.Destroy();
        }

        [Fact]
        public void Escape_OnTheBareMenu_DoesNothingAndDoesNotThrow()
        {
            // No form attached: there is nothing to back out of, so ESC is handed to the base (which ignores it) and
            // the window simply stays put. You are already at the top.
            var (app, window) = NewWindow();
            Assert.Null(window.CurrentForm);

            app.InputManager.SendKeyPress(ConsoleKey.Escape);
            app.OnTick(false);

            Assert.Null(window.CurrentForm);
            Assert.Same(window, app.WindowManager.FocusedWindow);

            app.Destroy();
        }
    }
}
