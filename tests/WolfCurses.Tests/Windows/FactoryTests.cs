using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    public class FactoryTests
    {
        [Fact]
        public void SimulationApp_DuplicateShortWindowTypeNames_ThrowsArgumentException()
        {
            // Documents current behavior: WindowFactory keys windows by Type.Name, so identically named windows
            // in different namespaces collide while the app is still constructing.
            Assert.Throws<ArgumentException>(() => new DuplicateNameSimulationApp());
        }

        [Fact]
        public void Add_AbstractAllowedWindow_ThrowsNullReferenceException()
        {
            // Documents current behavior: WindowFactory returns null for abstract types, WindowManager stores the
            // null anyway, and the OnWindowAdded notification then dereferences FocusedWindow.
            var app = new AbstractWindowSimulationApp();

            Assert.Throws<NullReferenceException>(() => app.WindowManager.Add(typeof(AbstractTestWindow)));
        }

        [Fact]
        public void SetForm_FormWithoutParentWindowAttribute_ThrowsArgumentException()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));

            Assert.Throws<ArgumentException>(
                () => app.WindowManager.FocusedWindow.SetForm(typeof(OrphanForm)));
        }

        [Fact]
        public void SetForm_RegisteredForm_AttachesAndFiresPostCreate()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = app.WindowManager.FocusedWindow;

            window.SetForm(typeof(YesNoDialogForm));

            var form = Assert.IsType<YesNoDialogForm>(window.CurrentForm);
            // OnFormPostCreate ran during SetForm, so the dialog prompt is already built.
            Assert.Equal(RecordingDialogForm.PROMPT_TEXT, form.OnRenderForm());
        }

        [Fact]
        public void SetForm_ReplacesExistingForm()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = app.WindowManager.FocusedWindow;
            window.SetForm(typeof(TestForm));

            window.SetForm(typeof(YesNoDialogForm));

            Assert.IsType<YesNoDialogForm>(window.CurrentForm);
        }
    }
}
