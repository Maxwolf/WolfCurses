// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A FOR loop that is currently running.
    ///     <para>
    ///         <b>The limit and the step are held here because BASIC works them out once</b>, when the loop starts,
    ///         and not again on each turn. <c>FOR I = 1 TO N</c> where the body changes N still runs the number of
    ///         times N said at the beginning, and a loop that re-read them would quietly do something else.
    ///     </para>
    /// </summary>
    public sealed class BasicLoopFrame
    {
        /// <summary>Initializes a new instance of the <see cref="BasicLoopFrame" /> class.</summary>
        /// <param name="variable">The loop variable's name.</param>
        /// <param name="limit">The value it counts to.</param>
        /// <param name="step">How much it moves each turn.</param>
        /// <param name="bodyIndex">The first statement of the body.</param>
        public BasicLoopFrame(string variable, double limit, double step, int bodyIndex)
        {
            Variable = variable;
            Limit = limit;
            Step = step;
            BodyIndex = bodyIndex;
        }

        /// <summary>The loop variable's name.</summary>
        public string Variable { get; }

        /// <summary>The value it counts to.</summary>
        public double Limit { get; }

        /// <summary>How much it moves each turn.</summary>
        public double Step { get; }

        /// <summary>The first statement of the body.</summary>
        public int BodyIndex { get; }
    }
}
