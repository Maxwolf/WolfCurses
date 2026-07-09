using WolfCurses.Core;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    public class InputManagerTests
    {
        /// <summary>Builds an app with a focused TestWindow (accepting input) so buffered characters are kept.</summary>
        private static TestSimulationApp NewAcceptingApp()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            return app;
        }

        [Fact]
        public void AddCharToInputBuffer_LettersAndDigits_Appended()
        {
            var app = NewAcceptingApp();

            app.InputManager.AddCharToInputBuffer('a');
            app.InputManager.AddCharToInputBuffer('B');
            app.InputManager.AddCharToInputBuffer('7');

            Assert.Equal("aB7", app.InputManager.InputBuffer);
        }

        [Theory]
        [InlineData('!')]
        [InlineData(' ')]
        [InlineData('-')]
        [InlineData('\t')]
        public void AddCharToInputBuffer_NonAlphanumeric_Filtered(char keyChar)
        {
            var app = NewAcceptingApp();

            app.InputManager.AddCharToInputBuffer(keyChar);

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void AddCharToInputBuffer_WhenNotAcceptingInput_Ignored()
        {
            // No window is attached, so WindowManager.AcceptingInput is false.
            var app = new TestSimulationApp();

            app.InputManager.AddCharToInputBuffer('a');

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void RemoveLastCharOfInputBuffer_RemovesOneCharacter_SafeWhenEmpty()
        {
            var app = NewAcceptingApp();
            app.InputManager.AddCharToInputBuffer('a');
            app.InputManager.AddCharToInputBuffer('b');

            app.InputManager.RemoveLastCharOfInputBuffer();
            Assert.Equal("a", app.InputManager.InputBuffer);

            app.InputManager.RemoveLastCharOfInputBuffer();
            app.InputManager.RemoveLastCharOfInputBuffer();
            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void SendInputBufferAsCommand_ClearsBuffer()
        {
            var app = NewAcceptingApp();
            app.InputManager.AddCharToInputBuffer('h');
            app.InputManager.AddCharToInputBuffer('i');

            app.InputManager.SendInputBufferAsCommand();

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void OnTick_DispatchesExactlyOneQueuedCommandPerTick()
        {
            // The focused window gets a form so dispatched commands are observable.
            var app = NewAcceptingApp();
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(TestForm));
            var form = (TestForm) window.CurrentForm;

            SendText(app, "one");
            SendText(app, "two");

            app.InputManager.OnTick(false);
            Assert.Equal(new[] { "one" }, form.ReceivedInputs);

            app.InputManager.OnTick(false);
            Assert.Equal(new[] { "one", "two" }, form.ReceivedInputs);

            // Queue drained; further ticks deliver nothing.
            app.InputManager.OnTick(false);
            Assert.Equal(2, form.ReceivedInputs.Count);
        }

        [Fact]
        public void SendInputBufferAsCommand_IdenticalQueuedCommand_IsDeduped()
        {
            // Documents current behavior: the queue rejects a command that is already waiting in it.
            var app = NewAcceptingApp();
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(TestForm));
            var form = (TestForm) window.CurrentForm;

            SendText(app, "same");
            SendText(app, "same");

            app.InputManager.OnTick(false);
            app.InputManager.OnTick(false);

            Assert.Equal(new[] { "same" }, form.ReceivedInputs);
        }

        [Fact]
        public void ClearBuffer_EmptiesInputBuffer()
        {
            var app = NewAcceptingApp();
            app.InputManager.AddCharToInputBuffer('x');

            app.InputManager.ClearBuffer();

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void PressEnter_ConstantValue_Pinned()
        {
            Assert.Equal("Press ENTER KEY to continue", InputManager.PRESSENTER);
        }

        private static void SendText(TestSimulationApp app, string text)
        {
            foreach (var keyChar in text)
                app.InputManager.AddCharToInputBuffer(keyChar);
            app.InputManager.SendInputBufferAsCommand();
        }
    }
}
