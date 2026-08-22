// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Calculator
{
    /// <summary>
    ///     One line of the paper tape: a number, and the mark that says what was done to it.
    ///     <para>
    ///         The tape is not decoration. A desk calculator works left to right with no precedence, so
    ///         <c>2 + 3 x 4</c> comes to twenty rather than fourteen, and the honest way to make that unsurprising
    ///         is to show the working rather than to explain it afterwards.
    ///     </para>
    /// </summary>
    public readonly struct CalculatorTapeLine
    {
        /// <summary>Initializes a new instance of the <see cref="CalculatorTapeLine" /> struct.</summary>
        /// <param name="value">The number, already formatted the way the display formats it.</param>
        /// <param name="mark">What was done to it: an operator, an equals, or a memory key's name.</param>
        /// <param name="isTotal">Whether this is an answer rather than something that went into one.</param>
        public CalculatorTapeLine(string value, string mark, bool isTotal = false)
        {
            Value = value ?? string.Empty;
            Mark = mark ?? string.Empty;
            IsTotal = isTotal;
        }

        /// <summary>The number, formatted.</summary>
        public string Value { get; }

        /// <summary>What was done to it.</summary>
        public string Mark { get; }

        /// <summary>
        ///     Whether this line is an answer. Kept as a flag rather than inferred from the mark, because the
        ///     renderer wants to pick answers out and "the mark is an equals sign" would stop being the same
        ///     question the moment a second kind of total existed.
        /// </summary>
        public bool IsTotal { get; }
    }
}
