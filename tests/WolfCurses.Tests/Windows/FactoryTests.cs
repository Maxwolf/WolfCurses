using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    public class FactoryTests
    {
        [Fact]
        public void SimulationApp_DuplicateShortWindowTypeNames_Coexist()
        {
            // WindowFactory keys windows by full name (it once keyed by Type.Name, which made identically named
            // windows in different namespaces collide during app construction), so an explicit list naming both
            // CloneWindows constructs fine and each type is individually creatable.
            var app = new DuplicateNameSimulationApp();

            app.WindowManager.Add(typeof(TestDoubles.CloneNamespaceA.CloneWindow));
            Assert.IsType<TestDoubles.CloneNamespaceA.CloneWindow>(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Add_AbstractAllowedWindow_ThrowsArgumentException()
        {
            // WindowFactory refuses abstract types with a descriptive exception instead of returning null and
            // letting the window list get poisoned.
            var app = new AbstractWindowSimulationApp();

            Assert.Throws<ArgumentException>(() => app.WindowManager.Add(typeof(AbstractTestWindow)));

            // The failed add must not leave a broken entry behind.
            Assert.Equal(0, app.WindowManager.Count);
            Assert.Null(app.WindowManager.FocusedWindow);
        }

        [Fact]
        public void SetForm_AbstractRegisteredForm_ThrowsArgumentException()
        {
            // FormFactory refuses abstract form types instead of returning null for SetForm to dereference.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));

            Assert.Throws<ArgumentException>(
                () => app.WindowManager.FocusedWindow.SetForm(typeof(AbstractRegisteredForm)));
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

        [Fact]
        public void FormFactory_ReadsAdditionalFormAssemblies_ExactlyOnce_WithoutBreakingDiscovery()
        {
            // FormFactory must consult SimulationApp.AdditionalFormAssemblies while it builds (read exactly once),
            // fold those assemblies into discovery, and de-duplicate — here the hook returns the already-scanned test
            // assembly, so form discovery must still succeed with no duplicate-registration crash.
            var app = new AdditionalAssembliesSimulationApp();

            Assert.Equal(1, app.AdditionalFormAssembliesReadCount);

            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(TestForm));

            Assert.IsType<TestForm>(app.WindowManager.FocusedWindow.CurrentForm);
        }
    }
}
