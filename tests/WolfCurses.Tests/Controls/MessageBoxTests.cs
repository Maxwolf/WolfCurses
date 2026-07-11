using System;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Drives the <see cref="MessageBox" /> through the real tick/input pipeline: OK dismissal, yes/no answers,
    ///     the yes/no/cancel result, ignoring invalid answers, and the open guards.
    /// </summary>
    public class MessageBoxTests
    {
        private static void Send(SimulationApp app, string command)
        {
            foreach (var c in command)
                app.InputManager.AddCharToInputBuffer(c);
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);
        }

        [Fact]
        public void Show_Ok_DismissesOnEnter()
        {
            var app = new ControlsHostApp();
            var dismissed = false;

            MessageBox.Show(app, "Hello.", () => dismissed = true);
            app.OnTick(false);

            Send(app, string.Empty); // ENTER with no text

            Assert.True(dismissed);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Confirm_Yes_RunsYesCallback()
        {
            var app = new ControlsHostApp();
            var answer = string.Empty;

            MessageBox.Confirm(app, "OK?", () => answer = "yes", () => answer = "no");
            app.OnTick(false);

            Send(app, "Y");

            Assert.Equal("yes", answer);

            app.Destroy();
        }

        [Fact]
        public void Confirm_No_RunsNoCallback()
        {
            var app = new ControlsHostApp();
            var answer = string.Empty;

            MessageBox.Confirm(app, "OK?", () => answer = "yes", () => answer = "no");
            app.OnTick(false);

            Send(app, "no");

            Assert.Equal("no", answer);

            app.Destroy();
        }

        [Fact]
        public void Confirm_InvalidAnswer_IsIgnoredUntilValid()
        {
            var app = new ControlsHostApp();
            var answered = false;

            MessageBox.Confirm(app, "OK?", () => answered = true, () => answered = true);
            app.OnTick(false);

            Send(app, "maybe"); // not yes/no -> ignored, dialog stays open
            Assert.False(answered);
            Assert.NotNull(app.WindowManager.FocusedWindow);

            Send(app, "Y");
            Assert.True(answered);

            app.Destroy();
        }

        [Fact]
        public void Show_YesNoCancel_ReturnsCancel()
        {
            var app = new ControlsHostApp();
            var result = MessageBoxResult.Yes;

            MessageBox.Show(app, "Proceed?", MessageBoxButtons.YesNoCancel, r => result = r);
            app.OnTick(false);

            Send(app, "C");

            Assert.Equal(MessageBoxResult.Cancel, result);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Show_WindowNotAllowed_ThrowsHelpfully()
        {
            var app = new TestSimulationApp();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MessageBox.Show(app, "Hi", () => { }));
            Assert.Contains("AllowedWindows", ex.Message);
            app.Destroy();
        }

        [Fact]
        public void Show_WhenAlreadyOpen_ThrowsInsteadOfDroppingTheFirstCallback()
        {
            var app = new ControlsHostApp();
            var first = false;
            MessageBox.Show(app, "First?", () => first = true);
            app.OnTick(false);

            Assert.Throws<InvalidOperationException>(() => MessageBox.Show(app, "Second?", () => { }));

            // The first dialog is untouched: answering it still runs the first callback.
            Send(app, string.Empty);
            Assert.True(first);

            app.Destroy();
        }

        [Fact]
        public void Show_WhenAlreadyClosing_Throws()
        {
            var app = new ControlsHostApp();
            MessageBox.Show(app, "Hi", () => { });
            app.OnTick(false);

            app.WindowManager.FocusedWindow.RemoveWindowNextTick();

            Assert.Throws<InvalidOperationException>(() => MessageBox.Show(app, "Hi", () => { }));

            app.Destroy();
        }
    }
}
