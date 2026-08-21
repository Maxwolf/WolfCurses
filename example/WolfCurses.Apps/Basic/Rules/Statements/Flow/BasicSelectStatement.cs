// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>SELECT CASE: work the value out once and put it where the CASE tests can find it.</summary>
    public sealed class BasicSelectStatement : BasicStatement
    {
        /// <summary>What is being selected on.</summary>
        private readonly BasicExpression _value;

        /// <summary>Initializes a new instance of the <see cref="BasicSelectStatement" /> class.</summary>
        /// <param name="value">What is being selected on.</param>
        /// <param name="line">The source line.</param>
        public BasicSelectStatement(BasicExpression value, int line) : base(line)
        {
            _value = value;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            // A stack rather than a slot, so that a SELECT inside a SELECT tests its own value. The two are pushed
            // and popped in step by the parser, which is the same arrangement FOR loops use.
            runtime.SelectValues.Push(_value.Evaluate(runtime));
            return index + 1;
        }
    }
}
