// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>An assignment, whether or not the program bothered to write LET in front of it.</summary>
    public sealed class BasicAssignStatement : BasicStatement
    {
        /// <summary>What to work out.</summary>
        private readonly BasicExpression _value;

        /// <summary>Where to put it.</summary>
        private readonly BasicTarget _target;

        /// <summary>Initializes a new instance of the <see cref="BasicAssignStatement" /> class.</summary>
        /// <param name="target">Where to put it.</param>
        /// <param name="value">What to work out.</param>
        /// <param name="line">The source line.</param>
        public BasicAssignStatement(BasicTarget target, BasicExpression value, int line) : base(line)
        {
            _target = target;
            _value = value;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            _target.Assign(runtime, _value.Evaluate(runtime), Line);
            return index + 1;
        }
    }
}
