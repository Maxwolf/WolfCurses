// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     READ: take the next constants the program wrote in its DATA statements.
    ///     <para>
    ///         <b>The DATA is gathered when the program is parsed, not when it runs</b>, and that is what makes it
    ///         work at all: a READ near the top may read DATA written at the bottom, and every listing that uses
    ///         DATA does exactly that. So the values are collected in the order they appear in the source and READ
    ///         simply walks them.
    ///     </para>
    /// </summary>
    public sealed class BasicReadStatement : BasicStatement
    {
        /// <summary>Where the values go.</summary>
        private readonly IReadOnlyList<BasicTarget> _targets;

        /// <summary>Initializes a new instance of the <see cref="BasicReadStatement" /> class.</summary>
        /// <param name="targets">Where the values go.</param>
        /// <param name="line">The source line.</param>
        public BasicReadStatement(IReadOnlyList<BasicTarget> targets, int line) : base(line)
        {
            _targets = targets;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            foreach (var target in _targets)
            {
                if (runtime.DataPointer >= runtime.Data.Count)
                    throw new BasicError("Out of DATA", Line);

                var value = runtime.Data[runtime.DataPointer];
                runtime.DataPointer++;

                // DATA is written without types, so a bare word is a string until something reads it into a number.
                // Converting here rather than refusing is what lets DATA 1, 2, 3 be read into numeric variables.
                if (!target.IsString && value.IsString)
                    value = new BasicValue(BasicValue.ParseNumber(value.Text));
                else if (target.IsString && !value.IsString)
                    value = new BasicValue(value.ToDisplay());

                target.Assign(runtime, value, Line);
            }

            return index + 1;
        }
    }
}
