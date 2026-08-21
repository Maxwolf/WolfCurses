// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A name followed by a bracketed list, which in BASIC is genuinely ambiguous: <c>A(1)</c> is either an
    ///     array element or a call to a function called A, and the source does not say which.
    ///     <para>
    ///         <b>So it is not decided until it runs.</b> Anything that has been dimensioned is an array, anything
    ///         with a built-in of that name is a function, and nothing else is an error naming the thing it could
    ///         not find. Deciding at parse time would mean the parser had to see every DIM first, which is not true
    ///         of a program that dimensions inside a branch.
    ///     </para>
    /// </summary>
    public sealed class BasicCallExpression : BasicExpression
    {
        /// <summary>The bracketed arguments, which may be subscripts.</summary>
        private readonly IReadOnlyList<BasicExpression> _arguments;

        /// <summary>Initializes a new instance of the <see cref="BasicCallExpression" /> class.</summary>
        /// <param name="name">The name in front of the bracket, uppercased.</param>
        /// <param name="arguments">What is inside the bracket.</param>
        /// <param name="line">The source line.</param>
        public BasicCallExpression(string name, IReadOnlyList<BasicExpression> arguments, int line) : base(line)
        {
            Name = name;
            _arguments = arguments;
        }

        /// <summary>The name in front of the bracket.</summary>
        public string Name { get; }

        /// <inheritdoc />
        public override BasicValue Evaluate(BasicRuntime runtime)
        {
            if (runtime.IsArray(Name))
                return runtime.ReadElement(Name, Subscripts(runtime), Line);

            // Not an array and not a function, so it was meant to be an array: that is far and away the likelier
            // mistake, and "undefined function" would send somebody looking for a spelling error in a name they
            // never intended as a function.
            if (!BasicFunctions.Exists(Name))
                throw new BasicError("Array " + Name + " has not been dimensioned", Line);

            var values = new List<BasicValue>(_arguments.Count);
            foreach (var argument in _arguments)
                values.Add(argument.Evaluate(runtime));

            return BasicFunctions.Call(Name, values, runtime, Line);
        }

        /// <summary>The arguments as array subscripts.</summary>
        private int[] Subscripts(BasicRuntime runtime)
        {
            var subscripts = new int[_arguments.Count];
            for (var i = 0; i < _arguments.Count; i++)
                subscripts[i] = (int) _arguments[i].Evaluate(runtime).AsNumber(Line);

            return subscripts;
        }
    }
}
