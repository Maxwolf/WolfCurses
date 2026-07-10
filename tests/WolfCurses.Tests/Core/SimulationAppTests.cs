using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     OnTick(true) is gated by real DateTime.UtcNow with no injection seam, so every deterministic test drives
    ///     simulation ticks directly with OnTick(false).
    /// </summary>
    public class SimulationAppTests
    {
        [Fact]
        public void Ctor_InitializesModules_NotClosing()
        {
            var app = new TestSimulationApp();

            Assert.False(app.IsClosing);
            Assert.NotNull(app.Random);
            Assert.NotNull(app.WindowManager);
            Assert.NotNull(app.InputManager);
            Assert.NotNull(app.SceneGraph);
            Assert.NotEmpty(app.TickPhase);
        }

        [Fact]
        public void SeededCtor_PlumbsSeedIntoRandomizer()
        {
            var app = new SeededSimulationApp(2024);

            Assert.Equal(2024, app.Random.RandomSeed);
        }

        [Fact]
        public void SeededCtor_SameSeed_MakesTheSharedRandomizerReproducible()
        {
            var a = new SeededSimulationApp(2024);
            var b = new SeededSimulationApp(2024);

            for (var i = 0; i < 100; i++)
                Assert.Equal(a.Random.Next(), b.Random.Next());
        }

        [Fact]
        public void OnTickFalse_FiresOnFirstTickExactlyOnce()
        {
            var app = new TestSimulationApp();

            app.OnTick(false);
            app.OnTick(false);
            app.OnTick(false);

            Assert.Equal(1, app.FirstTickCount);
        }

        [Fact]
        public void OnTickFalse_AdvancesTickPhase()
        {
            var app = new TestSimulationApp();
            var initialPhase = app.TickPhase;

            app.OnTick(false);

            Assert.NotEqual(initialPhase, app.TickPhase);
        }

        [Fact]
        public void OnTickTrue_BackToBack_DoesNotFireSimulationTick()
        {
            // Documents current behavior: system ticks only trigger a simulation tick after the real-time
            // TICK_INTERVAL (1000 ms) elapses, so immediate successive calls never advance the simulation.
            var app = new TestSimulationApp();

            app.OnTick(true);
            app.OnTick(true);
            app.OnTick(true);

            Assert.Equal(0, app.FirstTickCount);
        }

        [Fact]
        public void Destroy_SetsIsClosing_FiresOnPreDestroy_NullsModules()
        {
            var app = new TestSimulationApp();

            app.Destroy();

            Assert.True(app.IsClosing);
            Assert.Equal(1, app.PreDestroyCount);
            Assert.Null(app.Random);
            Assert.Null(app.WindowManager);
            Assert.Null(app.InputManager);
            Assert.Null(app.SceneGraph);
        }

        [Fact]
        public void OnTick_AfterDestroy_IsIgnored()
        {
            var app = new TestSimulationApp();
            app.Destroy();

            app.OnTick(false);

            Assert.Equal(0, app.FirstTickCount);
        }

        [Fact]
        public void Restart_ClearsWindowsAndInputBuffer()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.InputManager.AddCharToInputBuffer('x');

            app.Restart();

            Assert.Equal(0, app.WindowManager.Count);
            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
            Assert.NotNull(app.WindowManager);
        }

        [Fact]
        public void Restart_ReFiresOnFirstTickForTheNewSession()
        {
            var app = new TestSimulationApp();
            app.OnTick(false);
            Assert.Equal(1, app.FirstTickCount);

            app.Restart();
            app.OnTick(false);

            // The restarted session gets its OnFirstTick again, so the app can attach its initial window.
            Assert.Equal(2, app.FirstTickCount);
        }

        [Fact]
        public void Restart_DropsQueuedCommands()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            foreach (var keyChar in "stale")
                app.InputManager.AddCharToInputBuffer(keyChar);
            app.InputManager.SendInputBufferAsCommand();

            app.Restart();

            // Attach a fresh window and form; the pre-restart command must not reach it.
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(TestForm));
            var form = (TestForm) window.CurrentForm;

            app.InputManager.OnTick(false);

            Assert.Empty(form.ReceivedInputs);
        }

        [Fact]
        public void Restart_AfterDestroy_ThrowsInvalidOperationException()
        {
            var app = new TestSimulationApp();
            app.Destroy();

            Assert.Throws<System.InvalidOperationException>(() => app.Restart());
        }

        [Fact]
        public void Destroy_SecondCall_DoesNotRefireOnPreDestroy()
        {
            var app = new TestSimulationApp();

            app.Destroy();
            app.Destroy();

            Assert.Equal(1, app.PreDestroyCount);
        }

        [Fact]
        public void Destroy_DuringOnFirstTick_DoesNotThrow()
        {
            // OnFirstTick fires between the counter increment and the spinner step; destroying there must not
            // crash the remainder of the simulation tick.
            var app = new SelfDestructingSimulationApp();

            app.OnTick(false);

            Assert.True(app.IsClosing);
            Assert.Null(app.WindowManager);
        }

        [Fact]
        public void Destroy_DuringInputDispatch_StopsTickPipelineCleanly()
        {
            // A command handler that destroys the simulation runs inside the InputManager module tick; the rest
            // of the tick (renderer, window manager, tick counters) must notice and bail out.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(DestroyOnInputForm));
            ((TestWindowData) ((WolfCurses.Window.IWindow) window).UserData).App = app;

            foreach (var keyChar in "boom")
                app.InputManager.AddCharToInputBuffer(keyChar);
            app.InputManager.SendInputBufferAsCommand();

            app.OnTick(false);

            Assert.True(app.IsClosing);
            // The tick died before reaching the counter, so OnFirstTick never fired on the destroyed sim.
            Assert.Equal(0, app.FirstTickCount);
        }
    }
}
