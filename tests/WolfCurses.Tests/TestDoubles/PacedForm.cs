using System;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     A form that paces itself the way every real-time screen in the repository does, so
    ///     <see cref="Forms.FormTimerRestartTests" /> can drive the modal-pause case without sleeping. Its timer runs
    ///     on <see cref="Now" />, which the test winds forward by hand to stand in for time spent under a dialog.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public class PacedForm : Form<TestWindowData>
    {
        /// <summary>How far the clock is wound on between constructing the timers and registering one of them.</summary>
        public static readonly TimeSpan SetUpDelay = TimeSpan.FromSeconds(1);

        public PacedForm(IWindow window) : base(window)
        {
            Timer = new IntervalTimer(TimeSpan.FromMilliseconds(100), () => Now);
            Unregistered = new IntervalTimer(TimeSpan.FromMilliseconds(100), () => Now);

            // Time passes between the field initializers and OnFormPostCreate, which is where registration happens.
            // Without this the clock reads zero at both points and "registering restarts the timer" is unfalsifiable
            // — the assertion passes whether or not RestartOnActivate does anything at all.
            Now = SetUpDelay;
        }

        /// <summary>The fake clock both timers read; the test moves it.</summary>
        public TimeSpan Now { get; set; }

        /// <summary>Registered with RestartOnActivate, so the base class restarts it on activation.</summary>
        public IntervalTimer Timer { get; }

        /// <summary>
        ///     Never registered, and here to prove the seam is per timer rather than blanket: a timer measuring a
        ///     scripted sequence rather than a frame rate must survive activation untouched.
        /// </summary>
        public IntervalTimer Unregistered { get; }

        /// <summary>How many times activation reached this form, so a test can tell "did not fire" from "did nothing".</summary>
        public int ActivateCount { get; private set; }

        /// <summary>Set by <see cref="RegisterTwice" /> to exercise the duplicate-registration guard.</summary>
        public bool RegisterTwice { get; set; }

        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            RestartOnActivate(Timer);

            if (RegisterTwice)
                RestartOnActivate(Timer);
        }

        public override void OnFormActivate()
        {
            base.OnFormActivate();

            ActivateCount++;
        }

        public override string OnRenderForm()
        {
            return nameof(PacedForm);
        }

        public override void OnInputBufferReturned(string input)
        {
        }
    }
}
