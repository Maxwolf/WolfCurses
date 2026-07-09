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
    }
}
