// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>DIM: make an array exist.</summary>
    public sealed class BasicDimStatement : BasicStatement
    {
        /// <summary>The highest subscript on each dimension.</summary>
        private readonly IReadOnlyList<BasicExpression> _bounds;

        /// <summary>The array's name.</summary>
        private readonly string _name;

        /// <summary>Initializes a new instance of the <see cref="BasicDimStatement" /> class.</summary>
        /// <param name="name">The array's name.</param>
        /// <param name="bounds">The highest subscript on each dimension.</param>
        /// <param name="line">The source line.</param>
        public BasicDimStatement(string name, IReadOnlyList<BasicExpression> bounds, int line) : base(line)
        {
            _name = name;
            _bounds = bounds;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var bounds = new int[_bounds.Count];
            for (var i = 0; i < _bounds.Count; i++)
                bounds[i] = (int) _bounds[i].Evaluate(runtime).AsNumber(Line);

            runtime.Dimension(_name, bounds, Line);
            return index + 1;
        }
    }
}
