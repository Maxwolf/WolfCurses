// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>A number or a string written out in the program.</summary>
    public sealed class BasicLiteralExpression : BasicExpression
    {
        /// <summary>The value.</summary>
        private readonly BasicValue _value;

        /// <summary>Initializes a new instance of the <see cref="BasicLiteralExpression" /> class.</summary>
        /// <param name="value">The value.</param>
        /// <param name="line">The source line.</param>
        public BasicLiteralExpression(BasicValue value, int line) : base(line)
        {
            _value = value;
        }

        /// <inheritdoc />
        public override BasicValue Evaluate(BasicRuntime runtime)
        {
            return _value;
        }
    }
}
