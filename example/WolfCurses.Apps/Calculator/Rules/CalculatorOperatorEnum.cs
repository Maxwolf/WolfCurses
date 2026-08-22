// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Calculator
{
    /// <summary>
    ///     The four things a desk calculator can be part way through doing.
    ///     <para>
    ///         <see cref="None" /> is a real member rather than a null, because "nothing is pending" is a state the
    ///         machine is in half the time and asking a nullable about it at every turn reads worse than naming it.
    ///     </para>
    /// </summary>
    public enum CalculatorOperatorEnum
    {
        /// <summary>Nothing is waiting for a second number.</summary>
        None = 0,

        /// <summary>Addition.</summary>
        Add = 1,

        /// <summary>Subtraction.</summary>
        Subtract = 2,

        /// <summary>Multiplication.</summary>
        Multiply = 3,

        /// <summary>Division.</summary>
        Divide = 4
    }
}
