// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     What a cell shows when working it out went wrong.
    ///     <para>
    ///         The spellings are the ones every spreadsheet since VisiCalc has used, hash and all, because they are
    ///         what somebody looking at the screen will recognize. They are short on purpose too: an error has to
    ///         fit in a column that was sized for a number.
    ///     </para>
    /// </summary>
    internal static class FormulaErrors
    {
        /// <summary>The formula could not be read at all.</summary>
        public const string Syntax = "#ERROR!";

        /// <summary>A function nobody has heard of, or a name that is not a cell.</summary>
        public const string Name = "#NAME?";

        /// <summary>Arithmetic on something that is not a number, or a function given the wrong sort of argument.</summary>
        public const string Value = "#VALUE!";

        /// <summary>Divided by zero.</summary>
        public const string DivideByZero = "#DIV/0!";

        /// <summary>A cell outside the sheet.</summary>
        public const string Reference = "#REF!";

        /// <summary>
        ///     A cell that needs its own value to work out its own value, directly or round a longer loop. Shown
        ///     rather than thrown, and it has to be caught somewhere: without this the evaluator would call itself
        ///     until the stack ran out, which takes the whole program down rather than one cell.
        /// </summary>
        public const string Circular = "#CIRC!";
    }
}
