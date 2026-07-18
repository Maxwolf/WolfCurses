using System.Linq;
using WolfCurses.Tests.TestDoubles;
using WolfCurses.Utility;
using WolfCurses.Window.Form;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    /// <summary>
    ///     Covers <see cref="SimulationApp.DiscoveryAssemblies" />: the deduped assembly set form discovery scanned,
    ///     published so an application running attribute discovery of its own (a plugin registry, an event catalog)
    ///     can walk the same set instead of faking the process entry assembly by reflection when hosted.
    /// </summary>
    public class DiscoveryAssembliesTests
    {
        [Fact]
        public void ContainsTheLibraryAndTheAppSubclassAssembly()
        {
            var app = new TestSimulationApp();

            Assert.Contains(typeof(SimulationApp).Assembly, app.DiscoveryAssemblies);
            Assert.Contains(typeof(TestSimulationApp).Assembly, app.DiscoveryAssemblies);

            app.Destroy();
        }

        [Fact]
        public void HoldsNoNullsAndNoDuplicates()
        {
            // AdditionalAssembliesSimulationApp deliberately contributes the test assembly a second time; the
            // published set must still list every assembly exactly once, or a consumer scanning it would register
            // duplicate types — the same crash FormFactory's own dedupe exists to prevent.
            var app = new AdditionalAssembliesSimulationApp();

            Assert.DoesNotContain(null, app.DiscoveryAssemblies);
            Assert.Equal(app.DiscoveryAssemblies.Count, app.DiscoveryAssemblies.Distinct().Count());
            Assert.Contains(typeof(AdditionalAssembliesSimulationApp).Assembly, app.DiscoveryAssemblies);

            app.Destroy();
        }

        [Fact]
        public void FeedsAttributeDiscoveryTheSameWayFormScanningConsumed()
        {
            // The intended consumption: hand the published set straight to GetTypesWith. Scanning it must find the
            // test assembly's own [ParentWindow] forms — proof the set is the one form discovery actually used.
            var app = new TestSimulationApp();

            var found = AttributeExtensions.GetTypesWith<ParentWindowAttribute>(app.DiscoveryAssemblies, false)
                .ToList();

            Assert.Contains(typeof(TestForm), found);

            app.Destroy();
        }

        [Fact]
        public void EmptyAfterDestroySoNothingDanglesFromATornDownSimulation()
        {
            var app = new TestSimulationApp();
            app.Destroy();

            Assert.Empty(app.DiscoveryAssemblies);
        }
    }
}
