// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     One piece of BASIC source, with where it came from.
    ///     <para>
    ///         <b>It carries its source line, and that is the whole reason it is a type rather than a string.</b>
    ///         Every error a BASIC program can produce has to name a line, because the line is what the user is
    ///         looking at in the editor; an interpreter that loses track of it can only say that something went
    ///         wrong somewhere.
    ///     </para>
    /// </summary>
    public readonly struct BasicToken : IEquatable<BasicToken>
    {
        /// <summary>Initializes a new instance of the <see cref="BasicToken" /> struct.</summary>
        /// <param name="kind">What it is.</param>
        /// <param name="text">Its text, uppercased for a word so comparisons need no culture.</param>
        /// <param name="number">Its value, for a number.</param>
        /// <param name="line">The source line it came from, counting from one.</param>
        public BasicToken(BasicTokenKindEnum kind, string text, double number, int line)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            Number = number;
            Line = line;
        }

        /// <summary>What it is.</summary>
        public BasicTokenKindEnum Kind { get; }

        /// <summary>
        ///     Its text. <b>A word is stored uppercased</b>, which is what BASIC has always displayed and what makes
        ///     every later comparison an ordinal one rather than a culture-aware guess; a string keeps exactly the
        ///     characters it was written with.
        /// </summary>
        public string Text { get; }

        /// <summary>Its value, for a number.</summary>
        public double Number { get; }

        /// <summary>The source line it came from, counting from one.</summary>
        public int Line { get; }

        /// <summary>Whether this is a word with the given spelling.</summary>
        /// <param name="word">The keyword to test, in upper case.</param>
        /// <returns>TRUE when it matches.</returns>
        public bool IsWord(string word)
        {
            return Kind == BasicTokenKindEnum.Word && string.Equals(Text, word, StringComparison.Ordinal);
        }

        /// <summary>Whether this is the given punctuation.</summary>
        /// <param name="symbol">The symbol to test.</param>
        /// <returns>TRUE when it matches.</returns>
        public bool IsSymbol(string symbol)
        {
            return Kind == BasicTokenKindEnum.Symbol && string.Equals(Text, symbol, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public bool Equals(BasicToken other)
        {
            return Kind == other.Kind && string.Equals(Text, other.Text, StringComparison.Ordinal) &&
                   Number.Equals(other.Number) && Line == other.Line;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is BasicToken other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Text, Number, Line);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Kind switch
            {
                BasicTokenKindEnum.Number => Number.ToString(CultureInfo.InvariantCulture),
                BasicTokenKindEnum.String => "\"" + Text + "\"",
                BasicTokenKindEnum.EndOfLine => "end of line",
                BasicTokenKindEnum.EndOfFile => "end of program",
                _ => Text
            };
        }
    }
}
