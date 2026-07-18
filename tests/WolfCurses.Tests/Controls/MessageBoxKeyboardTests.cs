using System;
using System.Text.RegularExpressions;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Drives the <see cref="MessageBox" /> with the Left/Right + ENTER style through the real tick/input
    ///     pipeline. The subtleties: the first button is the highlighted default from the first frame (so a bare
    ///     ENTER answers with it — new behavior for the question dialogs, which used to ignore an empty line), the
    ///     bar is horizontal so only Left/Right move it, and typed answers still win.
    /// </summary>
    public class MessageBoxKeyboardTests
    {
        private static void PressKey(SimulationApp app, ConsoleKey key, char keyChar = '\0')
        {
            app.InputManager.SendConsoleKey(new ConsoleKeyInfo(keyChar, key, false, false, false));
            app.OnTick(false);
        }

        private static void PressEnter(SimulationApp app)
        {
            app.InputManager.SendConsoleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
            app.OnTick(false);
        }

        private static string Rendered(SimulationApp app)
        {
            var text = app.WindowManager.FocusedWindow.OnRenderWindow();
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        [Fact]
        public void TheButtonBarShowsWithTheFirstButtonHighlightedFromTheStart()
        {
            var app = new ControlsHostApp();
            MessageBox.Show(app, "Continue?", MessageBoxButtonsEnum.YesNoCancel, _ => { });
            app.OnTick(false);

            var rendered = Rendered(app);
            Assert.Contains("[>Yes<]", rendered);
            Assert.Contains("[ No ]", rendered);
            Assert.Contains("[ Cancel ]", rendered);

            app.Destroy();
        }

        [Fact]
        public void BareEnterPressesTheDefaultButton()
        {
            var app = new ControlsHostApp();
            MessageBoxResultEnum? result = null;
            MessageBox.Show(app, "Continue?", MessageBoxButtonsEnum.YesNo, r => result = r);
            app.OnTick(false);

            PressEnter(app);

            Assert.Equal(MessageBoxResultEnum.Yes, result);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void RightArrowMovesTheHighlightAndEnterPressesIt()
        {
            var app = new ControlsHostApp();
            MessageBoxResultEnum? result = null;
            MessageBox.Show(app, "Continue?", MessageBoxButtonsEnum.YesNoCancel, r => result = r);
            app.OnTick(false);

            PressKey(app, ConsoleKey.RightArrow);
            PressKey(app, ConsoleKey.RightArrow);
            Assert.Contains("[>Cancel<]", Rendered(app));

            PressEnter(app);

            Assert.Equal(MessageBoxResultEnum.Cancel, result);

            app.Destroy();
        }

        [Fact]
        public void TheBarWrapsAroundItsEnds()
        {
            var app = new ControlsHostApp();
            MessageBoxResultEnum? result = null;
            MessageBox.Show(app, "Continue?", MessageBoxButtonsEnum.YesNo, r => result = r);
            app.OnTick(false);

            // Left from the first button wraps to the last.
            PressKey(app, ConsoleKey.LeftArrow);
            PressEnter(app);

            Assert.Equal(MessageBoxResultEnum.No, result);

            app.Destroy();
        }

        [Fact]
        public void UpAndDownLeaveTheHorizontalBarAlone()
        {
            var app = new ControlsHostApp();
            MessageBoxResultEnum? result = null;
            MessageBox.Show(app, "Continue?", MessageBoxButtonsEnum.YesNo, r => result = r);
            app.OnTick(false);

            PressKey(app, ConsoleKey.DownArrow);
            PressKey(app, ConsoleKey.UpArrow);
            PressEnter(app);

            Assert.Equal(MessageBoxResultEnum.Yes, result);

            app.Destroy();
        }

        [Fact]
        public void TypedAnswersStillWinOverTheHighlight()
        {
            var app = new ControlsHostApp();
            MessageBoxResultEnum? result = null;
            MessageBox.Show(app, "Continue?", MessageBoxButtonsEnum.YesNo, r => result = r);
            app.OnTick(false);

            // Highlight sits on Yes; the user types the other answer, which must be what counts.
            PressKey(app, ConsoleKey.N, 'N');
            PressEnter(app);

            Assert.Equal(MessageBoxResultEnum.No, result);

            app.Destroy();
        }

        [Fact]
        public void AnOkDialogStillDismissesOnAnyEnter()
        {
            var app = new ControlsHostApp();
            var dismissed = false;
            MessageBox.Show(app, "Saved.", () => dismissed = true);
            app.OnTick(false);

            Assert.Contains("[>OK<]", Rendered(app));
            PressEnter(app);

            Assert.True(dismissed);

            app.Destroy();
        }
    }
}
