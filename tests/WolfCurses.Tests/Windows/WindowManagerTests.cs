using System.Collections.Generic;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    public class WindowManagerTests
    {
        [Fact]
        public void Add_AllowedType_CreatesWindow_SetsFocus_FiresPostCreate()
        {
            var app = new TestSimulationApp();

            app.WindowManager.Add(typeof(TestWindow));

            Assert.Equal(1, app.WindowManager.Count);
            var window = Assert.IsType<TestWindow>(app.WindowManager.FocusedWindow);
            Assert.Equal(1, window.PostCreateCount);
        }

        [Fact]
        public void Add_SecondType_FocusMovesToLastAdded_FirstIsNotified()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var first = (TestWindow) app.WindowManager.FocusedWindow;

            app.WindowManager.Add(typeof(SecondTestWindow));

            Assert.Equal(2, app.WindowManager.Count);
            Assert.IsType<SecondTestWindow>(app.WindowManager.FocusedWindow);
            Assert.Equal(1, first.AddedCount);
        }

        [Fact]
        public void Add_ExistingType_DoesNotDuplicate_FiresOnWindowActivate()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            var activationsBefore = window.ActivateCount;

            app.WindowManager.Add(typeof(TestWindow));

            Assert.Equal(1, app.WindowManager.Count);
            Assert.Same(window, app.WindowManager.FocusedWindow);
            Assert.Equal(activationsBefore + 1, window.ActivateCount);
        }

        [Fact]
        public void Add_TypeNotInAllowedWindows_ThrowsKeyNotFoundException()
        {
            var app = new TestSimulationApp();

            Assert.Throws<KeyNotFoundException>(() => app.WindowManager.Add(typeof(BadCommandsWindow)));
        }

        [Fact]
        public void OnTick_TicksOnlyFocusedWindow()
        {
            // Window.OnTick only forwards to its form, so tick observation happens through attached forms.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var background = (TestWindow) app.WindowManager.FocusedWindow;
            background.SetForm(typeof(TestForm));
            var backgroundForm = (TestForm) background.CurrentForm;

            app.WindowManager.Add(typeof(SecondTestWindow));
            var focused = (SecondTestWindow) app.WindowManager.FocusedWindow;
            focused.SetForm(typeof(SecondTestForm));
            var focusedForm = (TestForm) focused.CurrentForm;

            app.WindowManager.OnTick(false);

            Assert.Equal(1, focusedForm.TickCount);
            Assert.Equal(0, backgroundForm.TickCount);
        }

        [Fact]
        public void OnTick_RemovesFlaggedWindow_ActivatesNewFocus()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var survivor = (TestWindow) app.WindowManager.FocusedWindow;
            app.WindowManager.Add(typeof(SecondTestWindow));
            var doomed = (SecondTestWindow) app.WindowManager.FocusedWindow;
            var survivorActivations = survivor.ActivateCount;

            doomed.RemoveWindowNextTick();
            app.WindowManager.OnTick(false);

            Assert.Equal(1, app.WindowManager.Count);
            Assert.Same(survivor, app.WindowManager.FocusedWindow);
            Assert.Equal(survivorActivations + 1, survivor.ActivateCount);
        }

        [Fact]
        public void AcceptingInput_NoWindows_IsFalse()
        {
            var app = new TestSimulationApp();

            Assert.False(app.WindowManager.AcceptingInput);
        }

        [Fact]
        public void AcceptingInput_FocusedWindowWithoutForm_IsTrue()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));

            Assert.True(app.WindowManager.AcceptingInput);
        }

        [Fact]
        public void AcceptingInput_WindowFlaggedForRemoval_IsFalse()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.RemoveWindowNextTick();

            Assert.False(app.WindowManager.AcceptingInput);
        }

        [Fact]
        public void Clear_RemovesAllWindows()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.Add(typeof(SecondTestWindow));

            app.WindowManager.Clear();

            Assert.Equal(0, app.WindowManager.Count);
            Assert.Null(app.WindowManager.FocusedWindow);
        }
    }
}
