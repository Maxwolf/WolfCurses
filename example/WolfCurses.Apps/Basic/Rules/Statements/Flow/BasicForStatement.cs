// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     FOR: set the variable going and decide whether the body runs at all.
    ///     <para>
    ///         <b>A loop whose start is already past its limit runs zero times</b>, which is the behaviour every
    ///         program depends on when it loops over an empty list. Testing at the bottom instead would run the
    ///         body once, which is a bug that hides until the day the list is empty.
    ///     </para>
    /// </summary>
    public sealed class BasicForStatement : BasicStatement
    {
        /// <summary>The value to count to.</summary>
        private readonly BasicExpression _limit;

        /// <summary>The value to start at.</summary>
        private readonly BasicExpression _start;

        /// <summary>How much to move each turn, or null for one.</summary>
        private readonly BasicExpression _step;

        /// <summary>The loop variable's name.</summary>
        private readonly string _variable;

        /// <summary>Initializes a new instance of the <see cref="BasicForStatement" /> class.</summary>
        /// <param name="variable">The loop variable's name.</param>
        /// <param name="start">The value to start at.</param>
        /// <param name="limit">The value to count to.</param>
        /// <param name="step">How much to move each turn, or null for one.</param>
        /// <param name="line">The source line.</param>
        public BasicForStatement(string variable, BasicExpression start, BasicExpression limit, BasicExpression step,
            int line) : base(line)
        {
            _variable = variable;
            _start = start;
            _limit = limit;
            _step = step;
            ExitIndex = -1;
        }

        /// <summary>Where to go when the loop is finished before it began, patched when NEXT is reached.</summary>
        public int ExitIndex { get; set; }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var start = _start.Evaluate(runtime).AsNumber(Line);
            var limit = _limit.Evaluate(runtime).AsNumber(Line);
            var step = _step?.Evaluate(runtime).AsNumber(Line) ?? 1d;

            if (Math.Abs(step) < double.Epsilon)
                throw new BasicError("FOR step cannot be zero", Line);

            runtime.Write(_variable, new BasicValue(start), Line);
            runtime.Loops.Push(new BasicLoopFrame(_variable, limit, step, index + 1));

            if (Finished(start, limit, step))
            {
                runtime.Loops.Pop();
                return ExitIndex;
            }

            return index + 1;
        }

        /// <summary>
        ///     Whether counting has gone past the end, which depends on which way it is counting. A loop with a
        ///     negative step finishes when it drops below its limit, and testing only one direction is what makes a
        ///     countdown run forever.
        /// </summary>
        /// <param name="value">Where the variable is.</param>
        /// <param name="limit">Where it stops.</param>
        /// <param name="step">Which way and how fast.</param>
        /// <returns>TRUE when the loop is over.</returns>
        public static bool Finished(double value, double limit, double step)
        {
            return step > 0 ? value > limit : value < limit;
        }
    }
}
