using Xunit;

namespace WolfCurses.Apps.Tests.Support
{
    /// <summary>
    ///     Every test that starts the suite belongs here.
    ///     <para>
    ///         <c>AppsSimulationApp</c> is a singleton that throws rather than be created twice, and the library
    ///         carries process-wide state besides: the cached colour mode, the once-per-process renderer probe. Two
    ///         of these running at once is not a race that shows up as a wrong answer, it is one that shows up as
    ///         "an instance already exists" in whichever test lost, which is the kind of failure people rerun until
    ///         it passes. Same reasoning as the sibling <c>GamesApp</c> and <c>DemoApp</c> collections, and as the
    ///         library's own ColorModeMutation and RendererDefaultMutation.
    ///     </para>
    ///     <para>
    ///         Tests of pure application logic touch nothing global and deliberately do <b>not</b> join this
    ///         collection, so they keep running in parallel.
    ///     </para>
    /// </summary>
    [CollectionDefinition("AppsApp", DisableParallelization = true)]
    public class AppsAppCollection
    {
    }
}
