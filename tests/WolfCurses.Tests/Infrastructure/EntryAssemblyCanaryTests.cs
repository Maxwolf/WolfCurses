using System.Reflection;
using Xunit;

namespace WolfCurses.Tests.Infrastructure
{
    /// <summary>
    ///     The library discovers [ParentWindow] forms by scanning Assembly.GetEntryAssembly(). These tests prove the
    ///     test host runs with this assembly as the entry assembly (true for xunit.v3, which runs tests in the test
    ///     project's own process); if they fail, form discovery tests cannot work and the runner setup must change.
    /// </summary>
    public class EntryAssemblyCanaryTests
    {
        [Fact]
        public void EntryAssembly_IsTestAssembly()
        {
            Assert.Same(typeof(EntryAssemblyCanaryTests).Assembly, Assembly.GetEntryAssembly());
        }
    }
}
