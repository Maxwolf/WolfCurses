// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     The value a SELECT CASE is testing, read back by each of its CASE tests.
    ///     <para>
    ///         <b>SELECT CASE works its expression out once</b>, which is what lets a program write
    ///         <c>SELECT CASE RND</c> or select on a function with a side effect and still have every CASE compare
    ///         the same number. So the value is pushed once and the tests read it, rather than each test evaluating
    ///         the expression again and possibly getting a different answer.
    ///     </para>
    ///     <para>
    ///         Having it as an expression is what makes CASE cost nothing else: a test compiles into ordinary
    ///         comparisons against this, so the existing conditional jump runs the whole construct and there is no
    ///         special matching machinery anywhere.
    ///     </para>
    /// </summary>
    public sealed class BasicSelectValueExpression : BasicExpression
    {
        /// <summary>Initializes a new instance of the <see cref="BasicSelectValueExpression" /> class.</summary>
        /// <param name="line">The source line.</param>
        public BasicSelectValueExpression(int line) : base(line)
        {
        }

        /// <inheritdoc />
        public override BasicValue Evaluate(BasicRuntime runtime)
        {
            if (runtime.SelectValues.Count == 0)
                throw new BasicError("CASE without SELECT CASE", Line);

            return runtime.SelectValues.Peek();
        }
    }
}
