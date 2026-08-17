using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Forms
{
    /// <summary>
    ///     Form&lt;TData&gt;.RestartOnActivate exists because forgetting it fails silently and looks like a game bug.
    ///     A form only ticks while its window is focused, so a dialog on top freezes it — but the clock it paces
    ///     itself with is measuring real time and does not freeze, so on the way back the form is owed every step
    ///     that fell due while it was away and takes them all at once.
    /// </summary>
    public class FormTimerRestartTests
    {
        /// <summary>Builds an app showing TestWindow with a PacedForm attached, and hands both back.</summary>
        private static (TestSimulationApp App, PacedForm Form) Paced()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));

            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(PacedForm));

            return (app, (PacedForm) window.CurrentForm);
        }

        [Fact]
        public void ARegisteredTimerIsRestartedWhenTheWindowIsFocusedAgain()
        {
            // The modal bug as a test. Four seconds pass while something else has focus; without the restart the
            // form comes back owing itself forty steps of a 100ms pacer and takes them in one tick.
            var (app, form) = Paced();
            form.Now = TimeSpan.FromSeconds(4);

            Assert.True(form.Timer.IsDue);

            app.WindowManager.Add(typeof(TestWindow));

            Assert.Equal(1, form.ActivateCount);
            Assert.Equal(TimeSpan.Zero, form.Timer.Elapsed);
            Assert.False(form.Timer.IsDue);
        }

        [Fact]
        public void RestartingOnActivationDoesNotResetTheTotalElapsedPhaseClock()
        {
            // A form using one timer for both pacing and phase (the splash screen does) must not have its animation
            // jump when it regains focus.
            var (app, form) = Paced();
            form.Now = TimeSpan.FromSeconds(4);

            app.WindowManager.Add(typeof(TestWindow));

            Assert.Equal(TimeSpan.FromSeconds(4), form.Timer.TotalElapsed);
        }

        [Fact]
        public void AnUnregisteredTimerIsLeftAloneByActivation()
        {
            // The seam is per timer on purpose: a clock timing a scripted sequence rather than a frame rate would
            // have its phase stretched by however long the dialog was up.
            var (app, form) = Paced();
            form.Now = TimeSpan.FromSeconds(4);

            app.WindowManager.Add(typeof(TestWindow));

            Assert.Equal(TimeSpan.FromSeconds(4), form.Unregistered.Elapsed);
            Assert.True(form.Unregistered.IsDue);
        }

        [Fact]
        public void RegisteringAlsoStartsTheTimer()
        {
            // Which is what lets a form drop the separate Restart it used to make in OnFormPostCreate. The paced
            // form winds its clock on by SetUpDelay between constructing the timer and registering it, so the
            // unregistered timer shows the time that really passed and the registered one shows it was reset.
            var (_, form) = Paced();

            Assert.Equal(PacedForm.SetUpDelay, form.Unregistered.Elapsed);
            Assert.Equal(TimeSpan.Zero, form.Timer.Elapsed);
        }

        [Fact]
        public void RegisteringTheSameTimerTwiceIsHarmless()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;

            window.SetForm(typeof(PacedForm));
            var form = (PacedForm) window.CurrentForm;
            form.RegisterTwice = true;
            form.OnFormPostCreate();

            form.Now = TimeSpan.FromSeconds(4);
            app.WindowManager.Add(typeof(TestWindow));

            Assert.Equal(TimeSpan.Zero, form.Timer.Elapsed);
        }

        [Fact]
        public void AFormThatRegistersNothingIsUnchangedByActivation()
        {
            // The default path allocates nothing and the base OnFormActivate returns immediately, so every form
            // written before this seam existed behaves exactly as it did.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(TestForm));

            var activationsBefore = window.ActivateCount;
            app.WindowManager.Add(typeof(TestWindow));

            Assert.Equal(activationsBefore + 1, window.ActivateCount);
            Assert.IsType<TestForm>(window.CurrentForm);
        }
    }
}
