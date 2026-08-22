using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Forms
{
    /// <summary>
    ///     <c>IForm.OnFormClosing</c> is the counterpart to <c>OnFormPostCreate</c>, and it exists because a form
    ///     can own something the garbage collector will not tidy up.
    ///     <para>
    ///         What made it necessary was a child process: a form that started one had nowhere to stop it, so
    ///         backing out of that screen left a program running with no window to close it from. Memory would have
    ///         been forgiven; a handle, a thread or a process is the user's problem afterwards.
    ///     </para>
    ///     <para>
    ///         Every one of these is about a <i>path</i> rather than about the hook itself, because the hook is one
    ///         line and the paths are where it gets missed: cleared, replaced, window removed, simulation torn down.
    ///     </para>
    /// </summary>
    public class FormClosingTests
    {
        /// <summary>Builds an app showing TestWindow with a form that records its own teardown.</summary>
        private static (TestSimulationApp App, TestWindow Window, ClosingRecordingForm Form) Showing()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof (TestWindow));

            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof (ClosingRecordingForm));

            return (app, window, (ClosingRecordingForm) window.CurrentForm);
        }

        [Fact]
        public void AFormIsToldWhenItIsCleared()
        {
            var (_, window, form) = Showing();

            Assert.Equal(0, form.Closings);

            window.ClearForm();

            Assert.Equal(1, form.Closings);
            Assert.Null(window.CurrentForm);
        }

        [Fact]
        public void AFormIsToldWhenAnotherReplacesIt()
        {
            var (_, window, form) = Showing();

            // SetForm clears whatever was there first, which is the path a screen with several forms takes all day.
            window.SetForm(typeof (TestForm));

            Assert.Equal(1, form.Closings);
            Assert.IsType<TestForm>(window.CurrentForm);
        }

        [Fact]
        public void AFormIsToldWhenItsWindowIsRemoved()
        {
            var (_, window, form) = Showing();

            window.RemoveWindowNextTick();

            Assert.Equal(1, form.Closings);
        }

        [Fact]
        public void AFormIsToldWhenTheSimulationIsTornDown()
        {
            // The path that matters most and is easiest to miss: quitting the program. A child process outliving
            // the thing that started it is exactly what the user has to go and find in a task manager.
            var (app, _, form) = Showing();

            app.WindowManager.Destroy();

            Assert.Equal(1, form.Closings);
        }

        [Fact]
        public void AFormIsToldWhenEverythingIsCleared()
        {
            var (app, _, form) = Showing();

            app.WindowManager.Clear();

            Assert.Equal(1, form.Closings);
        }

        [Fact]
        public void ItIsToldOnceAndNotAgain()
        {
            var (_, window, form) = Showing();

            window.ClearForm();
            window.ClearForm();
            window.RemoveWindowNextTick();

            // Releasing something twice is how a teardown hook turns into the bug it was added to prevent.
            Assert.Equal(1, form.Closings);
        }

        [Fact]
        public void AFormMayClearItselfFromInsideItsOwnTeardownWithoutRecursing()
        {
            var (_, window, form) = Showing();
            form.ClearsItselfWhileClosing = true;

            // The form is detached BEFORE it is told, so this finds nothing left to do rather than coming back
            // round through here forever.
            window.ClearForm();

            Assert.Equal(1, form.Closings);
            Assert.Null(window.CurrentForm);
        }

        [Fact]
        public void AWindowWithNoFormClosesNothing()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof (TestWindow));

            var window = (TestWindow) app.WindowManager.FocusedWindow;

            // Nothing attached, so nothing to tell and nothing to throw about.
            window.ClearForm();
            window.RemoveWindowNextTick();
            app.WindowManager.Destroy();

            Assert.Null(window.CurrentForm);
        }

        [Fact]
        public void AFormThatOverridesNothingIsUnaffected()
        {
            // The compatibility half: every form written before this existed still works, because the hook is a
            // default interface member that does nothing.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof (TestWindow));

            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof (TestForm));

            window.ClearForm();

            Assert.Null(window.CurrentForm);
        }
    }
}
