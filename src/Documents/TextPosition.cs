// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Globalization;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     A place in a <see cref="TextBuffer" />: a zero-based line and a zero-based column within it. A column may
    ///     equal the line's length, which is the position after the last character and where typing appends.
    ///     <para>
    ///         A readonly struct because rendering a screenful asks for these by the hundred every frame, and
    ///         comparable because that is what a selection is made of: two positions whose order decides which is the
    ///         start and which the end. Comparing by line first and column second is the reading order of the
    ///         document, which is the only order that makes "everything between these two" mean what a person expects.
    ///     </para>
    /// </summary>
    public readonly struct TextPosition : IEquatable<TextPosition>, IComparable<TextPosition>
    {
        /// <summary>Initializes a new instance of the <see cref="TextPosition" /> struct.</summary>
        /// <param name="line">Zero-based line index.</param>
        /// <param name="column">Zero-based column within that line.</param>
        public TextPosition(int line, int column)
        {
            Line = line;
            Column = column;
        }

        /// <summary>The zero-based line.</summary>
        public int Line { get; }

        /// <summary>The zero-based column, which may equal the line's length (the position after the last character).</summary>
        public int Column { get; }

        /// <summary>The very start of any document, which is also <c>default(TextPosition)</c>.</summary>
        public static TextPosition Start => default;

        /// <inheritdoc />
        public bool Equals(TextPosition other)
        {
            return Line == other.Line && Column == other.Column;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is TextPosition other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Line, Column);
        }

        /// <summary>Orders by line and then by column, which is the order the text reads in.</summary>
        /// <param name="other">The position to compare against.</param>
        /// <returns>Negative when this comes first, zero when they are the same place, positive otherwise.</returns>
        public int CompareTo(TextPosition other)
        {
            return Line != other.Line ? Line.CompareTo(other.Line) : Column.CompareTo(other.Column);
        }

        public static bool operator ==(TextPosition left, TextPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextPosition left, TextPosition right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) >= 0;
        }

        /// <summary>One-based line and column, which is how every editor reports a position to a person.</summary>
        /// <returns>The position as "line:column", counting from one.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", Line + 1, Column + 1);
        }
    }
}
