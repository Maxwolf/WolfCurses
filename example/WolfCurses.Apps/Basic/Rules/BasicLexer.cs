// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Turns BASIC source into tokens. Pure: text in, a list out, no console and no state kept between calls.
    ///     <para>
    ///         <b>Every token remembers the physical line it came from</b>, which is what every error message has
    ///         to quote: the line number is what the user is looking at in the editor next door, and an interpreter
    ///         that loses it can only say that something went wrong somewhere.
    ///     </para>
    ///     <para>
    ///         <b>Blank lines and comments still emit their end of line.</b> Throwing them away entirely would be
    ///         tidier and would silently renumber the program, and the number beside a line is the one thing a BASIC
    ///         user navigates by.
    ///     </para>
    /// </summary>
    public static class BasicLexer
    {
        /// <summary>Reads a whole program.</summary>
        /// <param name="source">The program text.</param>
        /// <returns>Its tokens, ending with an end-of-file token.</returns>
        public static IReadOnlyList<BasicToken> Tokenize(string source)
        {
            var tokens = new List<BasicToken>();
            source ??= string.Empty;

            var line = 1;
            var at = 0;

            while (at < source.Length)
            {
                var character = source[at];

                if (character == '\r')
                {
                    at++;
                    continue;
                }

                if (character == '\n')
                {
                    tokens.Add(new BasicToken(BasicTokenKindEnum.EndOfLine, "\n", 0, line));
                    line++;
                    at++;
                    continue;
                }

                if (character is ' ' or '\t')
                {
                    at++;
                    continue;
                }

                // An apostrophe comment runs to the end of the line, and so does REM. Both are dropped here rather
                // than kept as tokens, because nothing above this has any use for them.
                if (character == '\'')
                {
                    at = SkipToEndOfLine(source, at);
                    continue;
                }

                // A colon stays a symbol rather than becoming an end of line, even though it separates statements
                // like one. A label is written "name:", so if the colon were folded into the line break there
                // would be no way to tell a label from a statement that is a bare word, and CLS would be a label.
                // The parser knows which of the two roles it is in and nothing else has to.
                if (character == ':')
                {
                    tokens.Add(new BasicToken(BasicTokenKindEnum.Symbol, ":", 0, line));
                    at++;
                    continue;
                }

                if (character == '"')
                {
                    at = ReadString(source, at, line, tokens);
                    continue;
                }

                if (char.IsDigit(character) || (character == '.' && at + 1 < source.Length && char.IsDigit(source[at + 1])))
                {
                    at = ReadNumber(source, at, line, tokens);
                    continue;
                }

                if (character == '&' && at + 1 < source.Length && (source[at + 1] is 'H' or 'h' or 'O' or 'o'))
                {
                    at = ReadRadixNumber(source, at, line, tokens);
                    continue;
                }

                if (char.IsLetter(character) || character == '_')
                {
                    at = ReadWord(source, at, line, tokens, out var wasComment);
                    if (wasComment)
                        at = SkipToEndOfLine(source, at);

                    continue;
                }

                at = ReadSymbol(source, at, line, tokens);
            }

            tokens.Add(new BasicToken(BasicTokenKindEnum.EndOfLine, "\n", 0, line));
            tokens.Add(new BasicToken(BasicTokenKindEnum.EndOfFile, string.Empty, 0, line));

            return tokens;
        }

        /// <summary>Walks to just before the next line break.</summary>
        private static int SkipToEndOfLine(string source, int at)
        {
            while (at < source.Length && source[at] != '\n')
                at++;

            return at;
        }

        /// <summary>
        ///     Reads a quoted string. <b>An unterminated one ends at the line break rather than throwing</b>, which
        ///     is what every BASIC has done: the program is being typed in the editor next door, and refusing to
        ///     tokenize the rest of the file over one missing quote reports the mistake in the wrong place.
        /// </summary>
        private static int ReadString(string source, int at, int line, List<BasicToken> tokens)
        {
            var text = new StringBuilder();
            at++;

            while (at < source.Length && source[at] != '"' && source[at] != '\n')
            {
                text.Append(source[at]);
                at++;
            }

            if (at < source.Length && source[at] == '"')
                at++;

            tokens.Add(new BasicToken(BasicTokenKindEnum.String, text.ToString(), 0, line));
            return at;
        }

        /// <summary>Reads a decimal number, exponent and type suffix included.</summary>
        private static int ReadNumber(string source, int at, int line, List<BasicToken> tokens)
        {
            var start = at;

            while (at < source.Length && char.IsDigit(source[at]))
                at++;

            if (at < source.Length && source[at] == '.')
            {
                at++;
                while (at < source.Length && char.IsDigit(source[at]))
                    at++;
            }

            // D is BASIC's double-precision exponent and means exactly what E means; both are read the same way.
            if (at < source.Length && (source[at] is 'E' or 'e' or 'D' or 'd'))
            {
                var exponent = at + 1;
                if (exponent < source.Length && (source[exponent] is '+' or '-'))
                    exponent++;

                if (exponent < source.Length && char.IsDigit(source[exponent]))
                {
                    at = exponent;
                    while (at < source.Length && char.IsDigit(source[at]))
                        at++;
                }
            }

            var text = source.Substring(start, at - start).Replace('D', 'E').Replace('d', 'e');

            // The type suffix is consumed and discarded: every number is held as a double, which is documented on
            // BasicValue as the one deliberate simplification of the numeric tower.
            if (at < source.Length && (source[at] is '!' or '#' or '%' or '&'))
                at++;

            tokens.Add(new BasicToken(BasicTokenKindEnum.Number, text,
                double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture), line));

            return at;
        }

        /// <summary>Reads <c>&amp;H</c> hexadecimal and <c>&amp;O</c> octal, which BASIC programs use for colours and masks.</summary>
        private static int ReadRadixNumber(string source, int at, int line, List<BasicToken> tokens)
        {
            var hex = source[at + 1] is 'H' or 'h';
            at += 2;

            var digits = new StringBuilder();
            while (at < source.Length && IsRadixDigit(source[at], hex))
            {
                digits.Append(source[at]);
                at++;
            }

            if (at < source.Length && (source[at] is '%' or '&'))
                at++;

            var value = 0L;
            foreach (var digit in digits.ToString())
                value = value * (hex ? 16 : 8) + Convert.ToInt32(digit.ToString(), hex ? 16 : 8);

            tokens.Add(new BasicToken(BasicTokenKindEnum.Number, digits.ToString(), value, line));
            return at;
        }

        /// <summary>Whether a character is a digit in the given radix.</summary>
        private static bool IsRadixDigit(char character, bool hex)
        {
            if (character is >= '0' and <= '7')
                return true;

            if (!hex)
                return false;

            return character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
        }

        /// <summary>
        ///     Reads a word and its type suffix. The suffix is kept as part of the name, because <c>A$</c> and
        ///     <c>A</c> really are two different variables in BASIC and dropping it would merge them.
        /// </summary>
        private static int ReadWord(string source, int at, int line, List<BasicToken> tokens, out bool wasComment)
        {
            var start = at;

            while (at < source.Length && (char.IsLetterOrDigit(source[at]) || source[at] is '_' or '.'))
                at++;

            var name = source.Substring(start, at - start).ToUpperInvariant();

            if (at < source.Length && (source[at] is '$' or '%' or '&' or '!' or '#'))
            {
                // Only the string marker changes what a name means to everything above; the numeric ones are read
                // and dropped for the same reason a number's suffix is.
                if (source[at] == '$')
                    name += "$";

                at++;
            }

            wasComment = string.Equals(name, "REM", StringComparison.Ordinal);
            if (!wasComment)
                tokens.Add(new BasicToken(BasicTokenKindEnum.Word, name, 0, line));

            return at;
        }

        /// <summary>Reads punctuation, taking the two-character operators before the one-character ones.</summary>
        private static int ReadSymbol(string source, int at, int line, List<BasicToken> tokens)
        {
            if (at + 1 < source.Length)
            {
                var pair = source.Substring(at, 2);
                if (pair is "<=" or ">=" or "<>" or "=<" or "=>")
                {
                    // The reversed spellings are what BASIC accepted and plenty of listings use; normalising them
                    // here means nothing above has to know they exist.
                    var normalized = pair switch
                    {
                        "=<" => "<=",
                        "=>" => ">=",
                        _ => pair
                    };

                    tokens.Add(new BasicToken(BasicTokenKindEnum.Symbol, normalized, 0, line));
                    return at + 2;
                }
            }

            tokens.Add(new BasicToken(BasicTokenKindEnum.Symbol, source[at].ToString(), 0, line));
            return at + 1;
        }
    }
}
