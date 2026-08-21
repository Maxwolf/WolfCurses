// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Something that produces a value. Expressions are kept as a tree rather than evaluated as they are read,
    ///     because a loop evaluates the same expression thousands of times and re-parsing it each go round is the
    ///     difference between a BASIC that feels like BASIC and one that crawls.
    /// </summary>
    public abstract class BasicExpression
    {
        /// <summary>Initializes a new instance of the <see cref="BasicExpression" /> class.</summary>
        /// <param name="line">The source line it was written on, for blaming.</param>
        protected BasicExpression(int line)
        {
            Line = line;
        }

        /// <summary>The source line it was written on.</summary>
        public int Line { get; }

        /// <summary>Works out the value.</summary>
        /// <param name="runtime">Where variables live and what the program can talk to.</param>
        /// <returns>The value.</returns>
        public abstract BasicValue Evaluate(BasicRuntime runtime);
    }
}
