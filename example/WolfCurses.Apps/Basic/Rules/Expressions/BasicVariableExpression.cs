// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Reading a variable.
    ///     <para>
    ///         <b>A variable that was never assigned reads as zero or as the empty string rather than failing.</b>
    ///         That is BASIC, and programs rely on it: a counter is used before it is ever set. Which of the two it
    ///         is comes from the name, since a trailing dollar is what makes a name a string.
    ///     </para>
    /// </summary>
    public sealed class BasicVariableExpression : BasicExpression
    {
        /// <summary>Initializes a new instance of the <see cref="BasicVariableExpression" /> class.</summary>
        /// <param name="name">The variable name, uppercased, dollar included when it has one.</param>
        /// <param name="line">The source line.</param>
        public BasicVariableExpression(string name, int line) : base(line)
        {
            Name = name;
        }

        /// <summary>The variable name.</summary>
        public string Name { get; }

        /// <inheritdoc />
        public override BasicValue Evaluate(BasicRuntime runtime)
        {
            return runtime.Read(Name);
        }
    }
}
