using System;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Drives the <see cref="TextInputDialog" /> through the real tick/input pipeline: submitting a value, the
    ///     pre-filled default, validation (reject then accept), blank-line cancellation, and masked (password) echo
    ///     via the scene graph.
    /// </summary>
    public class TextInputDialogTests
    {
        private static void Send(SimulationApp app, string command)
        {
            foreach (var c in command)
                app.InputManager.AddCharToInputBuffer(c);
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);
        }

        [Fact]
        public void Prompt_SubmitsTypedValue()
        {
            var app = new ControlsHostApp();
            string submitted = null;

            TextInputDialog.Prompt(app, "Name?", value => submitted = value);
            app.OnTick(false);

            Send(app, "Alice");

            Assert.Equal("Alice", submitted);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Prompt_DefaultValue_PreFillsTheBufferAndSubmitsOnBareEnter()
        {
            var app = new ControlsHostApp();
            string submitted = null;

            TextInputDialog.Prompt(app, "Name?", value => submitted = value, defaultValue: "Bob");
            app.OnTick(false);

            Assert.Equal("Bob", app.InputManager.InputBuffer); // pre-filled and editable

            Send(app, string.Empty); // bare ENTER submits the default

            Assert.Equal("Bob", submitted);

            app.Destroy();
        }

        [Fact]
        public void Prompt_Validation_RejectsThenAccepts()
        {
            var app = new ControlsHostApp();
            string submitted = null;

            TextInputDialog.Prompt(app, "Name?", value => submitted = value,
                validator: value => value.Length < 3 ? "Too short." : null);
            app.OnTick(false);

            Send(app, "ab"); // invalid -> stays open, nothing submitted
            Assert.Null(submitted);
            Assert.NotNull(app.WindowManager.FocusedWindow);

            Send(app, "abcd"); // valid
            Assert.Equal("abcd", submitted);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Prompt_BlankLine_Cancels()
        {
            var app = new ControlsHostApp();
            var cancelled = false;
            var submitted = false;

            TextInputDialog.Prompt(app, "Name?", _ => submitted = true, () => cancelled = true);
            app.OnTick(false);

            Send(app, string.Empty); // blank ENTER cancels

            Assert.True(cancelled);
            Assert.False(submitted);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Prompt_Masked_EchoesAsterisksAndHidesTheValue()
        {
            var app = new ControlsHostApp();
            string last = null;
            app.SceneGraph.ScreenBufferDirtyEvent += content => last = content;

            TextInputDialog.Prompt(app, "Passphrase:", _ => { }, masked: true);
            Assert.True(app.WindowManager.FocusedWindow.MaskInput);

            foreach (var c in "secret")
                app.InputManager.AddCharToInputBuffer(c);
            app.OnTick(false);

            Assert.EndsWith("******", last); // six characters, masked
            Assert.DoesNotContain("secret", last);

            app.Destroy();
        }

        [Fact]
        public void Prompt_NotMasked_EchoesTheTypedText()
        {
            var app = new ControlsHostApp();
            string last = null;
            app.SceneGraph.ScreenBufferDirtyEvent += content => last = content;

            TextInputDialog.Prompt(app, "Name:", _ => { });
            Assert.False(app.WindowManager.FocusedWindow.MaskInput);

            foreach (var c in "Alice")
                app.InputManager.AddCharToInputBuffer(c);
            app.OnTick(false);

            Assert.EndsWith("Alice", last);

            app.Destroy();
        }

        [Fact]
        public void Prompt_Masked_BufferIsClearedWhenTornDownWithoutSubmit()
        {
            var app = new ControlsHostApp();

            TextInputDialog.Prompt(app, "Pass:", _ => { }, masked: true);
            foreach (var c in "secret")
                app.InputManager.AddCharToInputBuffer(c);
            Assert.Equal("secret", app.InputManager.InputBuffer);

            // Tear the masked window down some way other than an ENTER submit (a host navigating away).
            app.WindowManager.FocusedWindow.RemoveWindowNextTick();

            // The secret must not survive in the shared buffer where a later non-masked prompt would echo it.
            Assert.Equal(string.Empty, app.InputManager.InputBuffer);

            app.Destroy();
        }

        [Fact]
        public void Prompt_WhenAlreadyOpen_Throws()
        {
            var app = new ControlsHostApp();
            TextInputDialog.Prompt(app, "First?", _ => { });
            app.OnTick(false);

            Assert.Throws<InvalidOperationException>(() => TextInputDialog.Prompt(app, "Second?", _ => { }));

            app.Destroy();
        }

        [Fact]
        public void Prompt_OpenedFromTheFirstPromptsCallback_AsksTheSecondQuestion()
        {
            // Asking for two values in a row is the composition these controls exist for, and the callback is the
            // place every one of them documents as the right place to do it. It could not be done until a window
            // that had already asked to be removed stopped being re-activated: the second prompt was handed the
            // first one's spent window, and the removal the first had already asked for then took it away.
            var app = new ControlsHostApp();
            string first = null;
            string second = null;

            TextInputDialog.Prompt(app, "Find what?", find =>
            {
                first = find;
                TextInputDialog.Prompt(app, "Change to what?", to => second = to);
            });

            app.OnTick(false);
            Send(app, "alpha");
            app.OnTick(false);

            Assert.Equal("alpha", first, StringComparer.Ordinal);
            Assert.Null(second);

            Send(app, "beta");
            app.OnTick(false);

            Assert.Equal("beta", second, StringComparer.Ordinal);

            app.Destroy();
        }

        [Fact]
        public void Prompt_ChainedSecondPrompt_StillPreFillsItsOwnDefault()
        {
            // The second prompt is opened part way through the tick that is processing the first one's ENTER, so
            // its pre-filled default is written into a buffer that command handling is still walking away from.
            // Asserted rather than assumed, since a default silently going missing is invisible until somebody
            // relies on it.
            var app = new ControlsHostApp();

            TextInputDialog.Prompt(app, "Find what?", _ =>
                TextInputDialog.Prompt(app, "Change to what?", _ => { }, defaultValue: "suggested"));

            app.OnTick(false);
            Send(app, "alpha");
            app.OnTick(false);

            Assert.Equal("suggested", app.InputManager.InputBuffer, StringComparer.Ordinal);

            app.Destroy();
        }

        [Fact]
        public void Prompt_WindowNotAllowed_ThrowsHelpfully()
        {
            var app = new TestSimulationApp();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                TextInputDialog.Prompt(app, "Name?", _ => { }));
            Assert.Contains("AllowedWindows", ex.Message);
            app.Destroy();
        }
    }
}
