// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     RESTORE: start reading the DATA again, either from the beginning or from a named line.
    ///     <para>
    ///         Restoring to a label means "the DATA written from that line onward", which is why the parser notes
    ///         how much data it had collected each time it defined one. It is not a jump: nothing about where the
    ///         program is running changes.
    ///     </para>
    /// </summary>
    public sealed class BasicRestoreStatement : BasicStatement
    {
        /// <summary>Which label to restore to, or null for the very beginning.</summary>
        private readonly string _label;

        /// <summary>Initializes a new instance of the <see cref="BasicRestoreStatement" /> class.</summary>
        /// <param name="label">Which label, or null.</param>
        /// <param name="line">The source line.</param>
        public BasicRestoreStatement(string label, int line) : base(line)
        {
            _label = label;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            if (_label == null)
            {
                runtime.DataPointer = 0;
                return index + 1;
            }

            if (!runtime.DataMarks.TryGetValue(_label, out var mark))
                throw new BasicError("Cannot find line or label " + _label, Line);

            runtime.DataPointer = mark;
            return index + 1;
        }
    }
}
