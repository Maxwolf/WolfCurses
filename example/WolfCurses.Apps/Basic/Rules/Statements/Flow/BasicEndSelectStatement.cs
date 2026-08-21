// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     END SELECT: throw the selected value away.
    ///     <para>
    ///         Every arm of the construct is compiled to land here, including the path where no CASE matched at all,
    ///         so the value is discarded exactly once however the construct is left.
    ///     </para>
    /// </summary>
    public sealed class BasicEndSelectStatement : BasicStatement
    {
        /// <summary>Initializes a new instance of the <see cref="BasicEndSelectStatement" /> class.</summary>
        /// <param name="line">The source line.</param>
        public BasicEndSelectStatement(int line) : base(line)
        {
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            if (runtime.SelectValues.Count > 0)
                runtime.SelectValues.Pop();

            return index + 1;
        }
    }
}
