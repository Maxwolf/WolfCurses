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
        [InlineData(' ')]
        [InlineData('!')]
        [InlineData('-')]
        [InlineData('\'')]
        [InlineData(',')]
        [InlineData('.')]
        [InlineData('@')]
        [InlineData('$')]
        public void AddCharToInputBuffer_PrintableNonAlphanumeric_Appended(char keyChar)
        {
            // Spaces and punctuation are valid free-text input (epitaphs, names, sentences) and must land in the buffer.
            var app = NewAcceptingApp();

            app.InputManager.AddCharToInputBuffer(keyChar);

            Assert.Equal(char.ToString(keyChar), app.InputManager.InputBuffer);
        }

        [Theory]
        [InlineData('\t')]
        [InlineData('\r')]
        [InlineData('\n')]
        [InlineData('\b')]
        [InlineData('\u001b')] // Escape
        [InlineData('\0')]
        public void AddCharToInputBuffer_ControlCharacters_Filtered(char keyChar)
        {
            // Control keys (Enter/Backspace are handled by the caller, Tab/Escape/etc. are noise) never enter the buffer.
            var app = NewAcceptingApp();

            app.InputManager.AddCharToInputBuffer(keyChar);

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Theory]
        [InlineData('\u200b')] // zero-width space
        [InlineData('\ufeff')] // byte-order mark / zero-width no-break space
        [InlineData('\u200d')] // zero-width joiner
        [InlineData('\u00ad')] // soft hyphen
        public void AddCharToInputBuffer_ZeroWidthFormatCharacters_Filtered(char keyChar)
        {
            // Invisible formatting marks are not IsControl and survive Trim(), so they would silently break exact
            // command matching (e.g. "1<ZWSP>" != "1"). They must never enter the buffer.
            var app = NewAcceptingApp();

            app.InputManager.AddCharToInputBuffer(keyChar);

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void AddCharToInputBuffer_SpaceBetweenLetters_ProducesSpacedText()
        {
            // Regression guard: a player typing "H", space, "i" must get "H i", not the old space-swallowing "Hi".
            var app = NewAcceptingApp();

            app.InputManager.AddCharToInputBuffer('H');
            app.InputManager.AddCharToInputBuffer(' ');
            app.InputManager.AddCharToInputBuffer('i');

            Assert.Equal("H i", app.InputManager.InputBuffer);
        }

        [Fact]
        public void TypedMultiWordText_RoundTripsToCommand_WithInternalSpacesPreserved()
        {
            // The downstream bug: typing an epitaph like "Here lies Bob" char-by-char (as a host Program.cs forwards
            // each key) used to collapse to "HereliesBob" (the old filter dropped the spaces but kept every letter).
            // Prove the whole path — per-char append then command dispatch — now keeps the internal spaces
            // (SendInputBufferAsCommand only Trim()s the ends).
            var app = NewAcceptingApp();
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(TestForm));
            var form = (TestForm) window.CurrentForm;

            SendText(app, "  Here lies Bob  ");
            app.InputManager.OnTick(false);

            Assert.Equal(new[] { "Here lies Bob" }, form.ReceivedInputs);
        }

        [Fact]
        public void AppendToInputBuffer_WhenAccepting_AppendsText()
        {
            var app = NewAcceptingApp();

            app.InputManager.AppendToInputBuffer("Here lies ");
            app.InputManager.AppendToInputBuffer("Bob");

            Assert.Equal("Here lies Bob", app.InputManager.InputBuffer);
        }

        [Fact]
        public void AppendToInputBuffer_StripsControlAndFormatCharacters()
        {
            // Injected/pasted text must not smuggle newlines, tabs, escapes, or invisible marks into the single-line
            // buffer (and from there into a dispatched command) — only the printable characters survive.
            var app = NewAcceptingApp();

            app.InputManager.AppendToInputBuffer("a\nb\tc\u001bd\u200be\ufeff");

            Assert.Equal("abcde", app.InputManager.InputBuffer);
        }

        [Fact]
        public void AppendToInputBuffer_AllNonPrintable_NoOp()
        {
            // A string that reduces to nothing after filtering must leave the existing buffer untouched.
            var app = NewAcceptingApp();
            app.InputManager.AddCharToInputBuffer('x');

            app.InputManager.AppendToInputBuffer("\n\t\u001b\u200b");

            Assert.Equal("x", app.InputManager.InputBuffer);
        }

        [Fact]
        public void AppendToInputBuffer_WhenNotAcceptingInput_Ignored()
        {
            // No window is attached, so WindowManager.AcceptingInput is false and injected text is dropped.
            var app = new TestSimulationApp();

            app.InputManager.AppendToInputBuffer("ignored");

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AppendToInputBuffer_NullOrEmpty_NoOp(string text)
        {
            var app = NewAcceptingApp();
            app.InputManager.AddCharToInputBuffer('a');

            app.InputManager.AppendToInputBuffer(text);

            Assert.Equal("a", app.InputManager.InputBuffer);
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
        public void SendInputBufferAsCommand_WhenNotAcceptingInput_SendsEmptyNotStaleBuffer()
        {
            // Text typed while input was accepted must not leak through as a command after a non-filling form
            // (AcceptingInput == false) takes over; only an empty submit may pass.
            var app = NewAcceptingApp();
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            app.InputManager.AddCharToInputBuffer('h');
            app.InputManager.AddCharToInputBuffer('i');

            window.SetForm(typeof(NonFillingRecordingForm));
            var form = (NonFillingRecordingForm) window.CurrentForm;
            Assert.False(app.WindowManager.AcceptingInput);

            app.InputManager.SendInputBufferAsCommand();
            app.InputManager.OnTick(false);

            Assert.Equal(new[] { string.Empty }, form.ReceivedInputs);
        }

        [Fact]
        public void ClearQueue_DropsPendingCommands()
        {
            var app = NewAcceptingApp();
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(TestForm));
            var form = (TestForm) window.CurrentForm;
            SendText(app, "pending");

            app.InputManager.ClearQueue();
            app.InputManager.OnTick(false);

            Assert.Empty(form.ReceivedInputs);
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
