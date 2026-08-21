// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A dimensioned BASIC array.
    ///     <para>
    ///         <b>Bounds are inclusive and start at zero</b>, so <c>DIM A(10)</c> has eleven elements. That is not a
    ///         rounding of the truth: programs index <c>A(10)</c> after dimensioning it that way and would be one
    ///         element short otherwise.
    ///     </para>
    /// </summary>
    public sealed class BasicArray
    {
        /// <summary>The elements, laid out row-major.</summary>
        private readonly BasicValue[] _values;

        /// <summary>Initializes a new instance of the <see cref="BasicArray" /> class.</summary>
        /// <param name="upperBounds">The highest valid subscript on each dimension.</param>
        /// <param name="ofStrings">Whether the elements are strings.</param>
        public BasicArray(int[] upperBounds, bool ofStrings)
        {
            UpperBounds = upperBounds;

            var total = 1;
            foreach (var bound in upperBounds)
                total *= bound + 1;

            _values = new BasicValue[total];

            // Filled rather than left at default, because a default BasicValue is a number and a string array has
            // to read as empty strings from the moment it exists.
            var empty = ofStrings ? BasicValue.EmptyString : BasicValue.Zero;
            for (var i = 0; i < _values.Length; i++)
                _values[i] = empty;
        }

        /// <summary>The highest valid subscript on each dimension.</summary>
        public int[] UpperBounds { get; }

        /// <summary>Reads one element.</summary>
        /// <param name="subscripts">The subscripts.</param>
        /// <param name="line">The line to blame.</param>
        /// <returns>The element.</returns>
        public BasicValue Read(int[] subscripts, int line)
        {
            return _values[Offset(subscripts, line)];
        }

        /// <summary>Writes one element.</summary>
        /// <param name="subscripts">The subscripts.</param>
        /// <param name="value">The value.</param>
        /// <param name="line">The line to blame.</param>
        public void Write(int[] subscripts, BasicValue value, int line)
        {
            _values[Offset(subscripts, line)] = value;
        }

        /// <summary>Turns subscripts into a position, complaining about the ones that do not name an element.</summary>
        private int Offset(int[] subscripts, int line)
        {
            if (subscripts.Length != UpperBounds.Length)
                throw new BasicError("Wrong number of subscripts", line);

            var offset = 0;
            for (var i = 0; i < subscripts.Length; i++)
            {
                if (subscripts[i] < 0 || subscripts[i] > UpperBounds[i])
                    throw new BasicError("Subscript out of range", line);

                offset = offset * (UpperBounds[i] + 1) + subscripts[i];
            }

            return offset;
        }
    }
}
