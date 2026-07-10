using System.Reflection;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    /// <summary>
    ///     Collection whose tests mutate the process-global entry assembly. Marked non-parallel so it never runs
    ///     alongside the entry-assembly canary or the entry-assembly form-discovery tests, which would otherwise
    ///     observe the temporary mutation and fail.
    /// </summary>
    [CollectionDefinition("EntryAssemblyMutation", DisableParallelization = true)]
    public sealed class EntryAssemblyMutationCollection
    {
    }

    /// <summary>
    ///     Reproduces the downstream failure that motivated scanning the SimulationApp's own assembly: when the app is
    ///     hosted (a test runner, a plugin, any process whose SimulationApp subclass is not the entry point) the entry
    ///     assembly holds no [ParentWindow] forms, so FormFactory used to discover zero forms and SetForm threw
    ///     "State factory cannot create state from type that does not exist in reference states!". FormFactory now also
    ///     scans simUnit.GetType().Assembly, so discovery keeps working.
    /// </summary>
    [Collection("EntryAssemblyMutation")]
    public sealed class FormDiscoveryHostedScenarioTests
    {
        [Fact]
        public void FormFactory_DiscoversForms_WhenAppAssemblyIsNotEntryAssembly()
        {
            var originalEntry = Assembly.GetEntryAssembly();
            try
            {
                // Point the entry assembly at the WolfCurses library, which defines no [ParentWindow] forms, so the
                // only way discovery can succeed is by scanning the app's (this test) assembly.
                Assembly.SetEntryAssembly(typeof(SimulationApp).Assembly);
                Assert.NotSame(typeof(TestForm).Assembly, Assembly.GetEntryAssembly());

                // FormFactory is built inside the SimulationApp constructor, reading the entry assembly here.
                var app = new TestSimulationApp();
                app.WindowManager.Add(typeof(TestWindow));
                var window = app.WindowManager.FocusedWindow;

                // The exact call that blew up downstream. It must attach the form rather than throw.
                window.SetForm(typeof(TestForm));

                Assert.IsType<TestForm>(window.CurrentForm);
            }
            finally
            {
                Assembly.SetEntryAssembly(originalEntry);
            }
        }
    }
}
