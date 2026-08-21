// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     The named functions a formula may call.
    ///     <para>
    ///         A deliberately short list, and short in a particular direction: what is here is what a column of
    ///         figures wants. There is no IF, no lookup and no text handling, all of which are real spreadsheet
    ///         features and none of which the sample sheet needs to make its point.
    ///     </para>
    ///     <para>
    ///         Every one of these is handed a flat list of values, because a range was already flattened into one
    ///         by the evaluator. That is what keeps the functions from having to know what a range is, and it means
    ///         <c>SUM(B5:B16)</c> and <c>SUM(B5, B6, B7)</c> arrive here identically.
    ///     </para>
    /// </summary>
    internal static class FormulaFunctions
    {
        /// <summary>Applies a function by name.</summary>
        /// <param name="name">Its name, in whatever case it was typed.</param>
        /// <param name="arguments">The values it was given, ranges already flattened.</param>
        /// <returns>What it comes to, or an error saying why it does not.</returns>
        public static SheetValue Apply(string name, IReadOnlyList<SheetValue> arguments)
        {
            // An error anywhere in the arguments is the answer. A total that quietly skipped a broken cell would
            // be a number that looks right and is not, which is worse than showing the fault.
            foreach (var argument in arguments)
            {
                if (argument.IsError)
                    return argument;
            }

            switch (name.ToUpperInvariant())
            {
                case "SUM":
                    return SheetValue.FromNumber(Sum(arguments));

                case "AVERAGE":
                case "AVG":
                    return Average(arguments);

                case "MIN":
                    return Extreme(arguments, true);

                case "MAX":
                    return Extreme(arguments, false);

                case "COUNT":
                    return SheetValue.FromNumber(CountNumbers(arguments));

                case "COUNTA":
                    return SheetValue.FromNumber(CountFilled(arguments));

                case "ABS":
                    return One(arguments, Math.Abs);

                case "INT":
                    return One(arguments, Math.Floor);

                case "SQRT":
                    return One(arguments, value => value < 0d ? double.NaN : Math.Sqrt(value));

                case "ROUND":
                    return Round(arguments);

                default:
                    return SheetValue.FromError(FormulaErrors.Name);
            }
        }

        /// <summary>
        ///     Adds up the numbers.
        ///     <para>
        ///         Text and empty cells are passed over rather than refused, because a range almost always includes
        ///         a heading or a gap and a total that would not add up a column with a word at the top of it would
        ///         be useless. Arithmetic outside a function is stricter, and that difference is on purpose.
        ///     </para>
        /// </summary>
        /// <param name="arguments">The values.</param>
        /// <returns>Their total.</returns>
        private static double Sum(IReadOnlyList<SheetValue> arguments)
        {
            var total = 0d;

            foreach (var argument in arguments)
            {
                if (argument.IsNumber)
                    total += argument.Number;
            }

            return total;
        }

        /// <summary>The mean of the numbers, over how many numbers there were rather than how many cells.</summary>
        /// <param name="arguments">The values.</param>
        /// <returns>Their mean.</returns>
        private static SheetValue Average(IReadOnlyList<SheetValue> arguments)
        {
            var count = CountNumbers(arguments);

            // Dividing by the cell count instead would report a wrong average for any range with a gap in it, and
            // averaging nothing at all is not zero, it is a question with no answer.
            return count == 0
                ? SheetValue.FromError(FormulaErrors.DivideByZero)
                : SheetValue.FromNumber(Sum(arguments) / count);
        }

        /// <summary>The smallest or largest number.</summary>
        /// <param name="arguments">The values.</param>
        /// <param name="smallest">TRUE for the smallest, FALSE for the largest.</param>
        /// <returns>The extreme value.</returns>
        private static SheetValue Extreme(IReadOnlyList<SheetValue> arguments, bool smallest)
        {
            var found = false;
            var best = 0d;

            foreach (var argument in arguments)
            {
                if (!argument.IsNumber)
                    continue;

                if (!found || (smallest ? argument.Number < best : argument.Number > best))
                    best = argument.Number;

                found = true;
            }

            // Nothing to choose between is not zero: zero would be a plausible answer that is not in the data.
            return found ? SheetValue.FromNumber(best) : SheetValue.FromError(FormulaErrors.Value);
        }

        /// <summary>How many of the values are numbers.</summary>
        /// <param name="arguments">The values.</param>
        /// <returns>The count.</returns>
        private static int CountNumbers(IReadOnlyList<SheetValue> arguments)
        {
            var count = 0;

            foreach (var argument in arguments)
            {
                if (argument.IsNumber)
                    count++;
            }

            return count;
        }

        /// <summary>How many of the values have anything in them at all, numbers or text.</summary>
        /// <param name="arguments">The values.</param>
        /// <returns>The count.</returns>
        private static int CountFilled(IReadOnlyList<SheetValue> arguments)
        {
            var count = 0;

            foreach (var argument in arguments)
            {
                if (!argument.IsEmpty)
                    count++;
            }

            return count;
        }

        /// <summary>A function of exactly one number.</summary>
        /// <param name="arguments">The values, of which there must be one.</param>
        /// <param name="apply">What to do to it.</param>
        /// <returns>The result.</returns>
        private static SheetValue One(IReadOnlyList<SheetValue> arguments, Func<double, double> apply)
        {
            if (arguments.Count != 1 || !Numeric(arguments[0], out var value))
                return SheetValue.FromError(FormulaErrors.Value);

            var result = apply(value);

            // A square root of a negative number comes back as not-a-number, which would otherwise be drawn as the
            // word NaN in the middle of a column of figures.
            return double.IsNaN(result) || double.IsInfinity(result)
                ? SheetValue.FromError(FormulaErrors.Value)
                : SheetValue.FromNumber(result);
        }

        /// <summary>Rounds a number to a number of decimal places, none by default.</summary>
        /// <param name="arguments">The number, and optionally how many places.</param>
        /// <returns>The rounded number.</returns>
        private static SheetValue Round(IReadOnlyList<SheetValue> arguments)
        {
            if (arguments.Count < 1 || arguments.Count > 2 || !Numeric(arguments[0], out var value))
                return SheetValue.FromError(FormulaErrors.Value);

            var digits = 0;

            if (arguments.Count == 2)
            {
                if (!Numeric(arguments[1], out var places))
                    return SheetValue.FromError(FormulaErrors.Value);

                digits = (int) places;
            }

            // Fifteen is as many places as a double has to give, and asking for more throws rather than returning.
            if (digits < 0 || digits > 15)
                return SheetValue.FromError(FormulaErrors.Value);

            return SheetValue.FromNumber(Math.Round(value, digits, MidpointRounding.AwayFromZero));
        }

        /// <summary>The number a single argument counts as, where an empty cell is zero and text is not a number.</summary>
        /// <param name="value">The value.</param>
        /// <param name="number">Its number.</param>
        /// <returns>TRUE when it can be used as one.</returns>
        private static bool Numeric(SheetValue value, out double number)
        {
            number = value.Number;

            return value.IsNumber || value.IsEmpty;
        }
    }
}
