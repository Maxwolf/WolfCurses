// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A BASIC value: a number or a string, never both.
    ///     <para>
    ///         <b>Every number is a double, and that is the one deliberate simplification of the language.</b> Real
    ///         QBasic has integer, long, single and double, chosen by a type suffix, and programs lean on it in two
    ///         ways: integer division wrapping, and <c>%</c> variables truncating on assignment. Carrying four
    ///         numeric types through every expression would double the size of the evaluator to serve programs that
    ///         mostly do not care, so the suffix is parsed and dropped, and the two places the difference is visible
    ///         are handled explicitly instead: <c>\</c> is integer division and <c>MOD</c> truncates its operands.
    ///     </para>
    ///     <para>
    ///         <b>A string and a number are not interchangeable and no conversion is implied.</b> BASIC is quite
    ///         strict here and says "Type mismatch" rather than guessing, which is what makes <c>"1" + 1</c> an error
    ///         instead of a silent 2 or a silent "11".
    ///     </para>
    /// </summary>
    public readonly struct BasicValue : IEquatable<BasicValue>
    {
        /// <summary>The number zero, which is what an unset numeric variable reads as.</summary>
        public static readonly BasicValue Zero = new(0d);

        /// <summary>The empty string, which is what an unset string variable reads as.</summary>
        public static readonly BasicValue EmptyString = new(string.Empty);

        /// <summary>Initializes a new instance of the <see cref="BasicValue" /> struct holding a number.</summary>
        /// <param name="number">The number.</param>
        public BasicValue(double number)
        {
            Number = number;
            Text = null;
        }

        /// <summary>Initializes a new instance of the <see cref="BasicValue" /> struct holding a string.</summary>
        /// <param name="text">The string; null is the empty string.</param>
        public BasicValue(string text)
        {
            Number = 0d;
            Text = text ?? string.Empty;
        }

        /// <summary>Whether this holds a string.</summary>
        public bool IsString => Text != null;

        /// <summary>The number, meaningless when this holds a string.</summary>
        public double Number { get; }

        /// <summary>The string, null when this holds a number.</summary>
        public string Text { get; }

        /// <summary>
        ///     BASIC's own truth: <b>anything other than zero is true, and true is -1 rather than 1.</b> That is not
        ///     a quirk to paper over, because programs use the value arithmetically (<c>x = x + (a &gt; b)</c>
        ///     subtracts one), and because it is what makes <c>NOT</c> work as a bitwise complement.
        /// </summary>
        public bool IsTrue => !IsString && Math.Abs(Number) > double.Epsilon;

        /// <summary>
        ///     Reads as much of a number as a string starts with, answering zero when it starts with none. The same
        ///     lenience VAL has, and for the same reason: it is used on text somebody typed or wrote in a DATA
        ///     statement, where refusing would be less useful than reading what is there.
        /// </summary>
        /// <param name="text">The text to read.</param>
        /// <returns>The number.</returns>
        public static double ParseNumber(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0d;

            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0d;
        }

        /// <summary>The BASIC representation of a truth.</summary>
        /// <param name="value">The truth.</param>
        /// <returns>-1 for true, 0 for false.</returns>
        public static BasicValue FromBoolean(bool value)
        {
            return new BasicValue(value ? -1d : 0d);
        }

        /// <summary>The number in this value, or a type mismatch when it is a string.</summary>
        /// <param name="line">The line to blame.</param>
        /// <returns>The number.</returns>
        public double AsNumber(int line = 0)
        {
            if (IsString)
                throw new BasicError("Type mismatch, a number was expected", line);

            return Number;
        }

        /// <summary>The string in this value, or a type mismatch when it is a number.</summary>
        /// <param name="line">The line to blame.</param>
        /// <returns>The string.</returns>
        public string AsText(int line = 0)
        {
            if (!IsString)
                throw new BasicError("Type mismatch, a string was expected", line);

            return Text;
        }

        /// <summary>
        ///     How PRINT writes this value.
        ///     <para>
        ///         <b>A number is written with a space where its sign would be and a space after it</b>, which is
        ///         why BASIC listings line up in columns and why <c>PRINT 1; 2</c> reads " 1  2 ". Reproducing that
        ///         matters more than it sounds: programs lay their screens out by counting those spaces.
        ///     </para>
        /// </summary>
        /// <returns>The text PRINT would put on the screen.</returns>
        public string ToPrint()
        {
            if (IsString)
                return Text;

            return (Number < 0 ? string.Empty : " ") + ToDisplay() + " ";
        }

        /// <summary>
        ///     The value as text without PRINT's padding, which is what <c>STR$</c> minus its sign space, string
        ///     concatenation of a converted number, and error messages all want.
        /// </summary>
        /// <returns>The bare text.</returns>
        public string ToDisplay()
        {
            if (IsString)
                return Text;

            if (double.IsNaN(Number) || double.IsInfinity(Number))
                return Number.ToString(CultureInfo.InvariantCulture);

            // Seven significant digits is single precision, which is what QBasic prints by default, and trailing
            // zeros are dropped so a whole number prints as a whole number rather than as 3.000000.
            var rounded = Math.Round(Number, 6, MidpointRounding.AwayFromZero);
            if (Math.Abs(rounded - Math.Truncate(rounded)) < 1e-10 && Math.Abs(rounded) < 1e15)
                return Math.Truncate(rounded).ToString("0", CultureInfo.InvariantCulture);

            return rounded.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <inheritdoc />
        public bool Equals(BasicValue other)
        {
            if (IsString != other.IsString)
                return false;

            return IsString
                ? string.Equals(Text, other.Text, StringComparison.Ordinal)
                : Number.Equals(other.Number);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is BasicValue other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return IsString ? StringComparer.Ordinal.GetHashCode(Text) : Number.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return IsString ? "\"" + Text + "\"" : ToDisplay();
        }
    }
}
