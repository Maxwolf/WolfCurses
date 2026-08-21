// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Two operands and an operator.
    ///     <para>
    ///         <b>Plus is the only operator that means two things</b>, and it does not mean either of them loosely:
    ///         with two strings it joins, with two numbers it adds, and with one of each it is a type mismatch
    ///         rather than a guess. That strictness is what stops a program silently producing "11" where it meant 2.
    ///     </para>
    /// </summary>
    public sealed class BasicBinaryExpression : BasicExpression
    {
        /// <summary>The left operand.</summary>
        private readonly BasicExpression _left;

        /// <summary>Which operator.</summary>
        private readonly string _operator;

        /// <summary>The right operand.</summary>
        private readonly BasicExpression _right;

        /// <summary>Initializes a new instance of the <see cref="BasicBinaryExpression" /> class.</summary>
        /// <param name="op">The operator, uppercased.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="line">The source line.</param>
        public BasicBinaryExpression(string op, BasicExpression left, BasicExpression right, int line) : base(line)
        {
            _operator = op;
            _left = left;
            _right = right;
        }

        /// <inheritdoc />
        public override BasicValue Evaluate(BasicRuntime runtime)
        {
            var left = _left.Evaluate(runtime);

            // AND and OR do NOT short circuit, deliberately. In BASIC they are bitwise operators that happen to
            // work on truth values, so both sides are always evaluated and "IF x <> 0 AND 10 / x > 1" divides by
            // zero exactly as it would in QBasic rather than quietly working.
            var right = _right.Evaluate(runtime);

            switch (_operator)
            {
                case "+":
                    if (left.IsString && right.IsString)
                        return new BasicValue(left.Text + right.Text);

                    return new BasicValue(left.AsNumber(Line) + right.AsNumber(Line));
                case "-":
                    return new BasicValue(left.AsNumber(Line) - right.AsNumber(Line));
                case "*":
                    return new BasicValue(left.AsNumber(Line) * right.AsNumber(Line));
                case "/":
                    return new BasicValue(Divide(left.AsNumber(Line), right.AsNumber(Line)));
                case "^":
                    return new BasicValue(Math.Pow(left.AsNumber(Line), right.AsNumber(Line)));

                // Integer division and MOD both truncate their operands first, which is the one place the missing
                // integer type would otherwise show: 7.9 \ 2 is 3 in BASIC and would be 3.95 without this.
                case "BACKSLASH":
                    return new BasicValue(Math.Truncate(Divide(Math.Truncate(left.AsNumber(Line)),
                        Math.Truncate(right.AsNumber(Line)))));
                case "MOD":
                    return new BasicValue(Modulo(Math.Truncate(left.AsNumber(Line)),
                        Math.Truncate(right.AsNumber(Line))));
                case "AND":
                    return new BasicValue((long) left.AsNumber(Line) & (long) right.AsNumber(Line));
                case "OR":
                    return new BasicValue((long) left.AsNumber(Line) | (long) right.AsNumber(Line));
                case "XOR":
                    return new BasicValue((long) left.AsNumber(Line) ^ (long) right.AsNumber(Line));
                default:
                    return Compare(left, right);
            }
        }

        /// <summary>Division that reports BASIC's own fault rather than handing back an infinity.</summary>
        private double Divide(double left, double right)
        {
            if (Math.Abs(right) < double.Epsilon)
                throw new BasicError("Division by zero", Line);

            return left / right;
        }

        /// <summary>Remainder, with the same complaint on a zero divisor.</summary>
        private double Modulo(double left, double right)
        {
            if (Math.Abs(right) < double.Epsilon)
                throw new BasicError("Division by zero", Line);

            return left % right;
        }

        /// <summary>
        ///     The comparisons, which work on both kinds of value. Strings compare ordinally, which is what BASIC
        ///     did (it compared character codes) and what keeps a sort stable regardless of anybody's locale.
        /// </summary>
        private BasicValue Compare(BasicValue left, BasicValue right)
        {
            if (left.IsString != right.IsString)
                throw new BasicError("Type mismatch, cannot compare a string with a number", Line);

            var order = left.IsString
                ? string.CompareOrdinal(left.Text, right.Text)
                : left.Number.CompareTo(right.Number);

            return _operator switch
            {
                "=" => BasicValue.FromBoolean(order == 0),
                "<>" => BasicValue.FromBoolean(order != 0),
                "<" => BasicValue.FromBoolean(order < 0),
                ">" => BasicValue.FromBoolean(order > 0),
                "<=" => BasicValue.FromBoolean(order <= 0),
                ">=" => BasicValue.FromBoolean(order >= 0),
                _ => throw new BasicError("Unknown operator " + _operator, Line)
            };
        }
    }
}
