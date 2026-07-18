using System.Linq;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;
using CloneA = WolfCurses.Tests.TestDoubles.CloneNamespaceA;
using CloneB = WolfCurses.Tests.TestDoubles.CloneNamespaceB;

namespace WolfCurses.Tests.Windows
{
    /// <summary>
    ///     Pins the default AllowedWindows reflection scan: what it finds (concrete windows from the app assembly plus
    ///     the library's built-in control windows), what it refuses (abstract types and wrong constructor shapes),
    ///     that the scan runs once per app instance, and that an explicit override still replaces it entirely.
    /// </summary>
    public class WindowDiscoveryTests
    {
        [Fact]
        public void DefaultAllowedWindows_DiscoversConcreteWindowsFromAppAssembly()
        {
            var app = new AutoDiscoverySimulationApp();

            Assert.Contains(typeof(TestWindow), app.AllowedWindows);
            Assert.Contains(typeof(SecondTestWindow), app.AllowedWindows);

            app.Destroy();
        }

        [Fact]
        public void DefaultAllowedWindows_DiscoversBuiltInControlWindows()
        {
            // The library assembly is always in the discovery set, so the controls need no opt-in at all.
            var app = new AutoDiscoverySimulationApp();

            Assert.Contains(typeof(FileDialogWindow), app.AllowedWindows);
            Assert.Contains(typeof(SelectListWindow), app.AllowedWindows);
            Assert.Contains(typeof(MessageBoxWindow), app.AllowedWindows);
            Assert.Contains(typeof(TextInputWindow), app.AllowedWindows);

            app.Destroy();
        }

        [Fact]
        public void DefaultAllowedWindows_ExcludesAbstractGenericAndWrongConstructorTypes()
        {
            var app = new AutoDiscoverySimulationApp();
            var allowed = app.AllowedWindows.ToList();

            // Abstract windows cannot be instantiated.
            Assert.DoesNotContain(typeof(AbstractTestWindow), allowed);

            // A window without the factory's (SimulationApp) constructor shape would explode in CreateWindow.
            Assert.DoesNotContain(typeof(WrongConstructorWindow), allowed);

            // A constructor demanding one specific app subclass is excluded on purpose: discovery promises every
            // listed window is creatable by whichever app is running, and this one is not.
            Assert.DoesNotContain(typeof(ConcreteAppConstructorWindow), allowed);

            // Nothing abstract or open-generic (the Window<,> base itself) may sneak in from any scanned assembly.
            Assert.DoesNotContain(allowed, t => t.IsAbstract || t.IsGenericTypeDefinition);

            app.Destroy();
        }

        [Fact]
        public void DefaultAllowedWindows_ScansOnce_LaterReadsReturnTheCachedList()
        {
            // The built-in controls re-check AllowedWindows on every Show, so the reflection walk must not repeat.
            var app = new AutoDiscoverySimulationApp();

            Assert.Same(app.AllowedWindows, app.AllowedWindows);

            app.Destroy();
        }

        [Fact]
        public void DefaultAllowedWindows_DiscoveredWindowIsCreatable()
        {
            var app = new AutoDiscoverySimulationApp();

            app.WindowManager.Add(typeof(TestWindow));

            Assert.IsType<TestWindow>(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void DefaultAllowedWindows_SameShortNameInDifferentNamespaces_BothCreatable()
        {
            // Discovery sweeps whole assemblies the app does not control the contents of, so the factory keys by
            // full name — both CloneWindows register, and each request resolves to the exact type asked for.
            var app = new AutoDiscoverySimulationApp();

            app.WindowManager.Add(typeof(CloneA.CloneWindow));
            Assert.IsType<CloneA.CloneWindow>(app.WindowManager.FocusedWindow);

            app.WindowManager.Add(typeof(CloneB.CloneWindow));
            Assert.IsType<CloneB.CloneWindow>(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void SameWindowTypeListedTwice_CollapsesInsteadOfThrowing()
        {
            // A hand-curated override that repeats an entry registers the type once, mirroring how stacked
            // [ParentWindow] attributes are tolerated; only two DISTINCT types sharing a full name are refused.
            var app = new RepeatedListingSimulationApp();

            app.WindowManager.Add(typeof(TestWindow));
            Assert.IsType<TestWindow>(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void OverriddenAllowedWindows_FullyReplacesDiscovery()
        {
            // An explicit override is the curation escape hatch — discovery must not fold extras into it.
            // TestSimulationApp lists two windows and gets exactly those two.
            var app = new TestSimulationApp();

            Assert.Equal(new[] { typeof(TestWindow), typeof(SecondTestWindow) }, app.AllowedWindows);

            app.Destroy();
        }

        [Fact]
        public void BuiltInControl_WorksWithoutAnyListing()
        {
            // The point of the whole feature: a message box on an app that never mentioned MessageBoxWindow.
            var app = new AutoDiscoverySimulationApp();
            var dismissed = false;

            MessageBox.Show(app, "Hello.", () => dismissed = true);
            app.OnTick(false);

            app.InputManager.SendInputBufferAsCommand(); // ENTER with no text dismisses the OK box.
            app.OnTick(false);

            Assert.True(dismissed);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }
    }
}
