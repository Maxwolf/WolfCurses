using System;
using System.Text.RegularExpressions;
using WolfCurses.Tests.Support;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    /// <summary>
    ///     Covers the arrow-key highlight over a window's menu: hidden until the first arrow key so untouched menus
    ///     render byte-identically to the pre-highlight library, summoned and moved by the key-press path, and run by
    ///     ENTER on an empty buffer — the empty command that used to travel the whole pipeline just to be dropped.
    /// </summary>
    public class MenuHighlightTests
    {
        /// <summary>
        ///     Inverse video is decoration that depends on the environment's color mode (NO_COLOR strips it); the
        ///     "&gt; " marker is the always-present contract, so assertions strip SGR sequences and pin the marker.
        /// </summary>
        private static string StripSgr(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        private static (TestSimulationApp app, TestWindow window) NewAppWithMenu()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.AddTestCommand(TestCommandsEnum.First);
            window.AddTestCommand(TestCommandsEnum.Second);
            window.AddTestCommand(TestCommandsEnum.Third);
            return (app, window);
        }

        [Fact]
        public void BeforeAnyArrowKeyTheMenuRendersExactlyAsItAlwaysHas()
        {
            // The compatibility stance in one assertion: no arrows means no highlight, no marker, no escapes — an
            // application that only ever types numbers cannot tell this feature exists.
            var (_, window) = NewAppWithMenu();

            var rendered = window.OnRenderWindow();

            Assert.Equal(
                "  1. First" + Text.NL +
                "  2. Second command" + Text.NL +
                "  3. Third" + Text.NL,
                rendered);
            Assert.DoesNotContain('\x1b', rendered);
        }

        [Fact]
        public void TheFirstDownArrowSummonsTheHighlightOntoTheFirstChoice()
        {
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            var rendered = StripSgr(window.OnRenderWindow());
            Assert.StartsWith("> 1. First" + Text.NL, rendered);
            Assert.Contains("  2. Second command", rendered);
        }

        [Fact]
        public void ArrowsMoveTheHighlightAndWrapAroundTheMenu()
        {
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow); // First
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow); // Second
            app.OnTick(false);
            Assert.Contains("> 2. Second command", StripSgr(window.OnRenderWindow()));

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow); // Third
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow); // wraps to First
            app.OnTick(false);
            Assert.Contains("> 1. First", StripSgr(window.OnRenderWindow()));
        }

        [Fact]
        public void UpFromNowhereStartsAtTheBottomLikeEveryCursesMenu()
        {
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendKeyPress(ConsoleKey.UpArrow);
            app.OnTick(false);

            Assert.Contains("> 3. Third", StripSgr(window.OnRenderWindow()));
        }

        [Fact]
        public void EmptyEnterRunsTheHighlightedChoice()
        {
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            // ENTER with nothing typed, through the real pipeline: empty buffer submitted as a command.
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);

            Assert.Equal(new[] {TestCommandsEnum.Second}, window.InvokedCommands);
        }

        [Fact]
        public void EmptyEnterWithNoHighlightStaysTheNoOpItAlwaysWas()
        {
            // Before the highlight is summoned, an empty submit must keep doing exactly nothing — that dead path is
            // only given a meaning once the user has pointed at something.
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);

            Assert.Empty(window.InvokedCommands);
        }

        [Fact]
        public void TypedNumbersStillWinOverTheHighlight()
        {
            // Both input styles are first-class: pointing at Second and typing 3 runs 3, because a non-empty buffer
            // is the user saying something more specific than the highlight.
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            app.InputManager.AddCharToInputBuffer('3');
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);

            Assert.Equal(new[] {TestCommandsEnum.Third}, window.InvokedCommands);
        }

        [Fact]
        public void ArrowKeysGoToTheFormWheneverOneIsAttached()
        {
            // A form on top owns the keys, exactly as before this feature: the menu behind it must not scroll.
            var (app, window) = NewAppWithMenu();
            window.SetForm(typeof(KeyRecordingForm));

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            var form = (KeyRecordingForm) window.CurrentForm;
            Assert.Equal(new[] {ConsoleKey.DownArrow}, form.ReceivedKeys);

            // And with the form gone the menu renders un-highlighted, the arrow having been spent on the form.
            window.ClearForm();
            Assert.DoesNotContain(">", StripSgr(window.OnRenderWindow()));
        }

        [Fact]
        public void EmptyEnterWithAFormAttachedStillGoesToTheFormNotTheMenu()
        {
            // InputForm dialogs close on an empty ENTER; that must keep happening instead of the menu eating it.
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            window.SetForm(typeof(PromptDialogForm));
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);

            Assert.Empty(window.InvokedCommands);
        }

        [Fact]
        public void ClearingCommandsHidesTheHighlightUntilAskedForAgain()
        {
            // The store pattern: menus are cleared and rebuilt to reflect purchases. A highlight surviving that could
            // point at whatever now occupies its old row; hiding it is the only honest state.
            var (app, window) = NewAppWithMenu();

            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            window.ClearAndRebuildMenu();

            var rendered = window.OnRenderWindow();
            Assert.DoesNotContain(">", StripSgr(rendered));

            // An empty ENTER right after the rebuild is a no-op again, not an invocation of row zero.
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);
            Assert.Empty(window.InvokedCommands);
        }
    }
}
