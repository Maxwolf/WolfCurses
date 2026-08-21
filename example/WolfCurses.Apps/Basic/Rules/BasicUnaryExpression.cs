// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Basic
{
    /// <summary>A prefix operator: negation, or NOT.</summary>
    public sealed class BasicUnaryExpression : BasicExpression
    {
        /// <summary>What it applies to.</summary>
        private readonly BasicExpression _operand;

        /// <summary>Which operator.</summary>
        private readonly string _operator;

        /// <summary>Initializes a new instance of the <see cref="BasicUnaryExpression" /> class.</summary>
        /// <param name="op">The operator, uppercased.</param>
        /// <param name="operand">What it applies to.</param>
        /// <param name="line">The source line.</param>
        public BasicUnaryExpression(string op, BasicExpression operand, int line) : base(line)
        {
            _operator = op;
            _operand = operand;
        }

        /// <inheritdoc />
        public override BasicValue Evaluate(BasicRuntime runtime)
        {
            var value = _operand.Evaluate(runtime);

            return _operator switch
            {
                "-" => new BasicValue(-value.AsNumber(Line)),
                "+" => new BasicValue(value.AsNumber(Line)),

                // NOT is a bitwise complement rather than a logical flip, which is exactly why BASIC's true is -1:
                // complementing 0 gives -1 and complementing -1 gives 0, so the two behave as a logical NOT for
                // free while still working on bit masks.
                "NOT" => new BasicValue(~(long) value.AsNumber(Line)),
                _ => throw new BasicError("Unknown operator " + _operator, Line)
            };
        }
    }
}
