using WolfCurses.Core;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     SceneGraph is ticked directly (not through SimulationApp.OnTick) so TickPhase never advances between
    ///     renders, making dirty-event assertions deterministic.
    /// </summary>
    public class SceneGraphTests
    {
        [Fact]
        public void OnTick_FirstRender_RaisesScreenBufferDirtyEvent()
        {
            var app = new TestSimulationApp();
            string rendered = null;
            var fireCount = 0;
            app.SceneGraph.ScreenBufferDirtyEvent += content =>
            {
                rendered = content;
                fireCount++;
            };

            app.SceneGraph.OnTick(false);

            Assert.Equal(1, fireCount);
            // Composed render: tick phase bracket, window census, app pre-render, no-window placeholder.
            Assert.StartsWith("[ ", rendered);
            Assert.Contains("Window(0):", rendered);
            Assert.Contains(TestSimulationApp.PRERENDER_TEXT, rendered);
            Assert.Contains("[NO WINDOW ATTACHED]", rendered);
        }

        [Fact]
        public void OnTick_UnchangedOutput_DoesNotRefireEvent()
        {
            var app = new TestSimulationApp();
            var fireCount = 0;
            app.SceneGraph.ScreenBufferDirtyEvent += _ => fireCount++;

            app.SceneGraph.OnTick(false);
            app.SceneGraph.OnTick(false);
            app.SceneGraph.OnTick(false);

            Assert.Equal(1, fireCount);
        }

        [Fact]
        public void OnTick_AfterStateChange_RefiresEvent()
        {
            var app = new TestSimulationApp();
            var fireCount = 0;
            app.SceneGraph.ScreenBufferDirtyEvent += _ => fireCount++;
            app.SceneGraph.OnTick(false);

            app.WindowManager.Add(typeof(TestWindow));
            app.SceneGraph.OnTick(false);

            Assert.Equal(2, fireCount);
        }

        [Fact]
        public void Render_WithFocusedWindow_ContainsCountNameDefaultTextAndPrompt()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            string rendered = null;
            app.SceneGraph.ScreenBufferDirtyEvent += content => rendered = content;

            app.SceneGraph.OnTick(false);

            Assert.Contains("Window(1): TestWindow()", rendered);
            // TestWindow has no commands, so the window renders blank and the default placeholder is used.
            Assert.Contains("[DEFAULT WINDOW TEXT]", rendered);
            // The window accepts input, so the prompt trails the render.
            Assert.Contains(SceneGraph.PROMPT_TEXT_DEFAULT, rendered);
        }

        [Fact]
        public void Render_WithFormAttached_ContainsFormNameInWindowCensus()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(TestForm));
            string rendered = null;
            app.SceneGraph.ScreenBufferDirtyEvent += content => rendered = content;

            app.SceneGraph.OnTick(false);

            Assert.Contains("Window(1): TestWindow(TestForm)", rendered);
            Assert.Contains(TestForm.RENDER_TEXT, rendered);
        }

        [Fact]
        public void Render_IncludesTypedInputBufferAfterPrompt()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.InputManager.AddCharToInputBuffer('h');
            app.InputManager.AddCharToInputBuffer('i');
            string rendered = null;
            app.SceneGraph.ScreenBufferDirtyEvent += content => rendered = content;

            app.SceneGraph.OnTick(false);

            Assert.EndsWith(SceneGraph.PROMPT_TEXT_DEFAULT + " hi", rendered);
        }

        [Fact]
        public void OnTick_CaseOnlyChange_RefiresEvent()
        {
            // The back-buffer diff is case-sensitive; a screen differing only by letter case still redraws.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            var fireCount = 0;
            app.SceneGraph.ScreenBufferDirtyEvent += _ => fireCount++;

            window.PromptText = "make a choice";
            app.SceneGraph.OnTick(false);
            Assert.Equal(1, fireCount);

            window.PromptText = "MAKE A CHOICE";
            app.SceneGraph.OnTick(false);

            Assert.Equal(2, fireCount);
        }

        [Fact]
        public void PromptTextDefault_ConstantPinned()
        {
            Assert.Equal("What is your choice?", SceneGraph.PROMPT_TEXT_DEFAULT);
        }
    }
}
