// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Somewhere a value can be put: a variable, or one element of an array. Shared by assignment and by INPUT,
    ///     because "where does this go" is the same question in both and writing it twice is how the two drift.
    /// </summary>
    public sealed class BasicTarget
    {
        /// <summary>The subscripts, or null for a plain variable.</summary>
        private readonly IReadOnlyList<BasicExpression> _subscripts;

        /// <summary>
        ///     The subscripts as written, or null. Read by the parser, because <c>Foo(1, 2)</c> with no equals sign
        ///     after it is not an array element at all: it is a call, and what looked like subscripts are its
        ///     arguments. Nothing can tell the two apart until the equals sign is or is not there.
        /// </summary>
        public IReadOnlyList<BasicExpression> Subscripts => _subscripts;

        /// <summary>Initializes a new instance of the <see cref="BasicTarget" /> class.</summary>
        /// <param name="name">The variable or array name, uppercased.</param>
        /// <param name="subscripts">The subscripts, or null for a plain variable.</param>
        public BasicTarget(string name, IReadOnlyList<BasicExpression> subscripts)
        {
            Name = name;
            _subscripts = subscripts;
        }

        /// <summary>The variable or array name.</summary>
        public string Name { get; }

        /// <summary>Whether the name means a string, which is a fact about the name itself.</summary>
        public bool IsString => BasicRuntime.IsStringName(Name);

        /// <summary>Puts a value here.</summary>
        /// <param name="runtime">The running program.</param>
        /// <param name="value">The value.</param>
        /// <param name="line">The line to blame.</param>
        public void Assign(BasicRuntime runtime, BasicValue value, int line)
        {
            if (_subscripts == null)
            {
                runtime.Write(Name, value, line);
                return;
            }

            var subscripts = new int[_subscripts.Count];
            for (var i = 0; i < _subscripts.Count; i++)
                subscripts[i] = (int) _subscripts[i].Evaluate(runtime).AsNumber(line);

            runtime.WriteElement(Name, subscripts, value, line);
        }
    }
}
