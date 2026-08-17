using Xunit;

namespace WolfCurses.Games.Tests.Support
{
    /// <summary>
    ///     Every test that starts the arcade belongs here.
    ///     <para>
    ///         <c>GamesSimulationApp</c> is a singleton that throws rather than be created twice, and the library
    ///         carries process-wide state besides — the cached colour mode, the once-per-process renderer probe. Two
    ///         of these running at once is not a race that shows up as a wrong answer; it is a race that shows up as
    ///         "an instance already exists" in whichever test lost, which is the kind of failure people rerun until
    ///         it passes. Same reasoning as the library's own ColorModeMutation and RendererDefaultMutation
    ///         collections.
    ///     </para>
    ///     <para>
    ///         Tests of the pure game logic — the chess rules, the snake board, the minefield — deliberately do
    ///         <b>not</b> join this collection. They touch nothing global and there are enough of them that running
    ///         them in parallel is worth having.
    ///     </para>
    /// </summary>
    [CollectionDefinition("GamesApp", DisableParallelization = true)]
    public class GamesAppCollection
    {
    }
}
