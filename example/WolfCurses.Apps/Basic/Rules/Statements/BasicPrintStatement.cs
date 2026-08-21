// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     PRINT, whose punctuation is not decoration.
    ///     <para>
    ///         A semicolon joins with nothing between, a comma moves to the next fourteen column zone, and
    ///         <b>whether the line ends at all depends on the last separator</b>: a PRINT ending in a semicolon
    ///         leaves the cursor where it is, which is how BASIC programs draw anything on one line.
    ///     </para>
    /// </summary>
    public sealed class BasicPrintStatement : BasicStatement
    {
        /// <summary>How wide a comma zone is, which has been fourteen columns since Dartmouth.</summary>
        private const int ZoneWidth = 14;

        /// <summary>What to print.</summary>
        private readonly IReadOnlyList<BasicExpression> _items;

        /// <summary>What followed each item: a semicolon, a comma, or nothing.</summary>
        private readonly IReadOnlyList<char> _separators;

        /// <summary>Initializes a new instance of the <see cref="BasicPrintStatement" /> class.</summary>
        /// <param name="items">What to print.</param>
        /// <param name="separators">What followed each item.</param>
        /// <param name="line">The source line.</param>
        public BasicPrintStatement(IReadOnlyList<BasicExpression> items, IReadOnlyList<char> separators, int line)
            : base(line)
        {
            _items = items;
            _separators = separators;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var column = 0;

            for (var i = 0; i < _items.Count; i++)
            {
                var text = _items[i].Evaluate(runtime).ToPrint();
                runtime.Host.Write(text);
                column += text.Length;

                if (i >= _separators.Count || _separators[i] != ',')
                    continue;

                // A comma jumps to the next zone, which is what lines a table up without anybody counting spaces.
                var padding = ZoneWidth - column % ZoneWidth;
                runtime.Host.Write(new string(' ', padding));
                column += padding;
            }

            // The line ends unless the statement trailed off with a separator, which is the entire difference
            // between PRINT "A" and PRINT "A";
            var trailing = _separators.Count > 0 && _separators.Count >= _items.Count
                ? _separators[_separators.Count - 1]
                : '\0';

            if (trailing != ';' && trailing != ',')
                runtime.Host.WriteLine();

            return index + 1;
        }
    }
}
