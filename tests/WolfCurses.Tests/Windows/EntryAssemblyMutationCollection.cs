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
}
