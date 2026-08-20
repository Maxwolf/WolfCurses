using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     ENTER and BACKSPACE are input-buffer control, and a screen that edits a document needs them as keys
    ///     instead. <c>IForm.EditsText</c> is how a screen says so.
    ///     <para>
    ///         Both halves matter and the second one matters more. The library documents the ENTER/BACKSPACE routing
    ///         as load-bearing, and its own note is that a <c>case ConsoleKey.Enter:</c> in an override is dead code
    ///         that compiles: plenty of existing screens may carry one written speculatively. So the opt-in has to
    ///         default to off and change nothing whatsoever for a screen that has not asked, or this feature silently
    ///         brings all of that dead code to life.
    ///     </para>
    /// </summary>
    public class EditsTextRoutingTests
    {
        private static (TestSimulationApp app, TestWindow window) NewApp()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            return (app, (TestWindow) app.WindowManager.FocusedWindow);
        }

        private static ConsoleKeyInfo Key(ConsoleKey key, char character = '\0')
        {
            return new ConsoleKeyInfo(character, key, false, false, false);
        }

        [Fact]
        public void AnEditingFormIsHandedBackspaceWhichItCouldNotOtherwiseSee()
        {
            // The whole reason the flag exists. Without it a backspace is spent rubbing a character off the input
            // buffer and no key handler anywhere is told it happened.
            var (app, window) = NewApp();
            window.SetForm(typeof(TextEditingRecordingForm));
            var form = (TextEditingRecordingForm) window.CurrentForm;

            app.InputManager.SendConsoleKey(Key(ConsoleKey.Backspace, '\b'));
            app.OnTick(false);

            Assert.Contains(ConsoleKey.Backspace, form.ReceivedKeys);
        }

        [Fact]
        public void AnEditingFormIsHandedEnterAsAKeyRatherThanAsASubmittedLine()
        {
            // An editor splits a line on ENTER; it is not collecting a command, so the buffer must not see it.
            var (app, window) = NewApp();
            window.SetForm(typeof(TextEditingRecordingForm));
            var form = (TextEditingRecordingForm) window.CurrentForm;

            app.InputManager.SendConsoleKey(Key(ConsoleKey.Enter, '\r'));
            app.OnTick(false);

            Assert.Contains(ConsoleKey.Enter, form.ReceivedKeys);
            Assert.Empty(form.ReceivedInputs);
        }

        [Fact]
        public void AnEditingFormStillGetsOrdinaryCharactersTheWayItAlwaysDid()
        {
            var (app, window) = NewApp();
            window.SetForm(typeof(TextEditingRecordingForm));
            var form = (TextEditingRecordingForm) window.CurrentForm;

            app.InputManager.SendConsoleKey(Key(ConsoleKey.A, 'a'));
            app.OnTick(false);

            Assert.Contains(ConsoleKey.A, form.ReceivedKeys);
        }

        [Fact]
        public void WithoutTheOptInEnterStillSubmitsTheBufferAndReachesNoKeyHandler()
        {
            // The compatibility half. A form that has not asked sees exactly what it always saw, which is a
            // submitted line and no key press at all.
            var (app, window) = NewApp();
            window.SetForm(typeof(KeyInfoRecordingForm));

            app.InputManager.SendConsoleKey(Key(ConsoleKey.T, 't'));
            app.InputManager.SendConsoleKey(Key(ConsoleKey.Enter, '\r'));
            app.OnTick(false);

            var form = (KeyInfoRecordingForm) window.CurrentForm;
            Assert.DoesNotContain(form.ReceivedKeyInfos, info => info.Key == ConsoleKey.Enter);
        }

        [Fact]
        public void WithoutTheOptInBackspaceStillOnlyEditsTheBuffer()
        {
            var (app, window) = NewApp();
            window.SetForm(typeof(KeyInfoRecordingForm));

            app.InputManager.SendConsoleKey(Key(ConsoleKey.A, 'a'));
            app.InputManager.SendConsoleKey(Key(ConsoleKey.B, 'b'));
            app.InputManager.SendConsoleKey(Key(ConsoleKey.Backspace, '\b'));
            app.OnTick(false);

            var form = (KeyInfoRecordingForm) window.CurrentForm;
            Assert.DoesNotContain(form.ReceivedKeyInfos, info => info.Key == ConsoleKey.Backspace);
            Assert.Equal("a", app.InputManager.InputBuffer);
        }

        [Fact]
        public void AWindowWithNoFormIsNotEditingText()
        {
            // The window answers with whatever its form says, and a bare menu has none. A menu that started
            // swallowing ENTER would stop being able to run the highlighted choice.
            var (app, window) = NewApp();
            window.AddTestCommand(TestCommandsEnum.First);

            Assert.False(window.EditsText);

            app.InputManager.SendConsoleKey(Key(ConsoleKey.Enter, '\r'));
            app.OnTick(false);

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void ClosingTheEditorHandsEnterAndBackspaceBackToTheBuffer()
        {
            // The flag follows the focused screen rather than being a mode somebody has to remember to unset, so
            // leaving the editor restores the ordinary routing with nothing to clean up.
            var (app, window) = NewApp();
            window.SetForm(typeof(TextEditingRecordingForm));
            Assert.True(window.EditsText);

            window.ClearForm();
            Assert.False(window.EditsText);

            app.InputManager.SendConsoleKey(Key(ConsoleKey.A, 'a'));
            app.InputManager.SendConsoleKey(Key(ConsoleKey.Backspace, '\b'));
            app.OnTick(false);

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }
    }
}
