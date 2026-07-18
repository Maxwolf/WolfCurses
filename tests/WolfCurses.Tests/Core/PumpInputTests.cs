using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     Covers <see cref="SimulationApp.PumpInput" />, the deterministic "press the key, then look" helper. The
    ///     thing it retires is the blind two-tick dance every driver copy-pasted: modules tick input → scene → window,
    ///     so a command that changes the window stack is dispatched on one tick and the settled state is only rendered
    ///     on the next. Settling is judged on state (queues drained, no pending removals) because the spinner makes
    ///     frame equality useless.
    /// </summary>
    public class PumpInputTests
    {
        private static void Type(SimulationApp app, string command)
        {
            foreach (var c in command)
                app.InputManager.AddCharToInputBuffer(c);
            app.InputManager.SendInputBufferAsCommand();
        }

        [Fact]
        public void ACommandThatAttachesAFormIsDispatchedAndTheResultRendered()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.AddTestCommand(TestCommandsEnum.First, () => window.SetForm(typeof(TestForm)));

            Type(app, "1");
            var settled = app.PumpInput();

            // The whole point, now assertable thanks to the public ScreenBuffer: after one call the frame on screen
            // is the post-command world, not the pre-command one.
            Assert.True(settled);
            Assert.True(app.InputManager.IsIdle);
            Assert.Contains("TestWindow(TestForm)", app.SceneGraph.ScreenBuffer);
            Assert.Contains(TestForm.RENDER_TEXT, app.SceneGraph.ScreenBuffer);

            app.Destroy();
        }

        [Fact]
        public void AWindowFlaggedForRemovalOutsideATickIsSettledAndTheEmptyStackRendered()
        {
            // The pending-removals half of the settle condition: a driver tears a window down between ticks, and one
            // PumpInput later the screen shows the world without it.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.RemoveWindowNextTick();
            Assert.True(app.WindowManager.HasPendingRemovals);

            var settled = app.PumpInput();

            Assert.True(settled);
            Assert.False(app.WindowManager.HasPendingRemovals);
            Assert.Contains("[NO WINDOW ATTACHED]", app.SceneGraph.ScreenBuffer);

            app.Destroy();
        }

        [Fact]
        public void AnIdleSimulationSettlesInASingleTick()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));

            var settled = app.PumpInput();

            Assert.True(settled);
            // The pump ticks are real simulation ticks — the first one fires OnFirstTick like any other.
            Assert.Equal(1, app.FirstTickCount);

            app.Destroy();
        }

        [Fact]
        public void TheBoundStopsInputThatOutlastsIt()
        {
            // Commands dispatch one per tick, so three distinct queued commands cannot settle inside two ticks; the
            // bound turns a would-be runaway into a false return instead of a hang.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.AddTestCommand(TestCommandsEnum.First);
            window.AddTestCommand(TestCommandsEnum.Second);
            window.AddTestCommand(TestCommandsEnum.Third);

            Type(app, "1");
            Type(app, "2");
            Type(app, "3");

            Assert.False(app.PumpInput(maxTicks: 2));
            Assert.False(app.InputManager.IsIdle);

            // A follow-up pump with room to spare drains the rest and settles.
            Assert.True(app.PumpInput());
            Assert.True(app.InputManager.IsIdle);

            app.Destroy();
        }

        [Fact]
        public void AZeroOrNegativeBoundIsRefusedLoudly()
        {
            var app = new TestSimulationApp();

            Assert.Throws<ArgumentOutOfRangeException>(() => app.PumpInput(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => app.PumpInput(-3));

            app.Destroy();
        }

        [Fact]
        public void ADestroyedSimulationReportsUnsettledRatherThanRendering()
        {
            // SelfDestructingSimulationApp destroys itself from OnFirstTick, which the pump's first tick fires.
            var app = new SelfDestructingSimulationApp();

            Assert.False(app.PumpInput());
            Assert.True(app.IsClosing);
        }
    }
}
