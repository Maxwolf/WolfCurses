// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     The functions built into the language. One table rather than a type each, because every one of them is
    ///     the same shape: values in, a value out, no state of its own.
    ///     <para>
    ///         <b>The dollar on a name is part of the name.</b> <c>LEFT$</c> is not <c>LEFT</c>, and the lexer keeps
    ///         the marker for exactly this reason, so the table can be looked up by what was written.
    ///     </para>
    /// </summary>
    public static class BasicFunctions
    {
        /// <summary>Whether a name is one of these, which is how a bare word is told from an undimensioned array.</summary>
        /// <param name="name">The name, uppercased.</param>
        /// <returns>TRUE when it is a built-in function.</returns>
        public static bool Exists(string name)
        {
            return name switch
            {
                "ABS" or "INT" or "FIX" or "SGN" or "SQR" or "SIN" or "COS" or "TAN" or "ATN" or "LOG" or "EXP"
                    or "RND" or "TIMER" or "LEN" or "LEFT$" or "RIGHT$" or "MID$" or "INSTR" or "CHR$" or "ASC"
                    or "STR$" or "VAL" or "UCASE$" or "LCASE$" or "LTRIM$" or "RTRIM$" or "SPACE$" or "STRING$"
                    or "INKEY$" => true,
                _ => false
            };
        }

        /// <summary>
        ///     The ones that are written without brackets, like variables. <c>TIMER</c> and <c>INKEY$</c> are read
        ///     as bare names in every program that uses them, so the parser has to know which words to treat that
        ///     way rather than as an undefined variable.
        /// </summary>
        /// <param name="name">The name, uppercased.</param>
        /// <returns>TRUE when it needs no brackets.</returns>
        public static bool IsBare(string name)
        {
            return name is "TIMER" or "INKEY$" or "RND";
        }

        /// <summary>Calls one.</summary>
        /// <param name="name">The function name, uppercased.</param>
        /// <param name="arguments">Its arguments, already evaluated.</param>
        /// <param name="runtime">The running program.</param>
        /// <param name="line">The line to blame.</param>
        /// <returns>The result.</returns>
        public static BasicValue Call(string name, IReadOnlyList<BasicValue> arguments, BasicRuntime runtime, int line)
        {
            switch (name)
            {
                case "ABS":
                    return new BasicValue(Math.Abs(One(name, arguments, line)));
                case "SGN":
                    return new BasicValue(Math.Sign(One(name, arguments, line)));
                case "SQR":
                    var root = One(name, arguments, line);
                    if (root < 0)
                        throw new BasicError("Square root of a negative number", line);

                    return new BasicValue(Math.Sqrt(root));
                case "SIN":
                    return new BasicValue(Math.Sin(One(name, arguments, line)));
                case "COS":
                    return new BasicValue(Math.Cos(One(name, arguments, line)));
                case "TAN":
                    return new BasicValue(Math.Tan(One(name, arguments, line)));
                case "ATN":
                    return new BasicValue(Math.Atan(One(name, arguments, line)));
                case "LOG":
                    var log = One(name, arguments, line);
                    if (log <= 0)
                        throw new BasicError("Logarithm of a number that is not positive", line);

                    return new BasicValue(Math.Log(log));
                case "EXP":
                    return new BasicValue(Math.Exp(One(name, arguments, line)));

                // INT rounds down and FIX rounds toward zero, which are the same for positives and are not for
                // negatives: INT(-2.5) is -3 and FIX(-2.5) is -2. Programs use the difference.
                case "INT":
                    return new BasicValue(Math.Floor(One(name, arguments, line)));
                case "FIX":
                    return new BasicValue(Math.Truncate(One(name, arguments, line)));
                case "RND":
                    return Rnd(arguments, runtime, line);
                case "TIMER":
                    return new BasicValue(DateTime.Now.TimeOfDay.TotalSeconds);
                case "INKEY$":
                    return new BasicValue(runtime.Host.ReadKey() ?? string.Empty);
                case "LEN":
                    return new BasicValue(Text(name, arguments, 0, line).Length);
                case "CHR$":
                    return new BasicValue(((char) (int) One(name, arguments, line)).ToString());
                case "ASC":
                    var asc = Text(name, arguments, 0, line);
                    if (asc.Length == 0)
                        throw new BasicError("ASC of an empty string", line);

                    return new BasicValue((double) asc[0]);
                case "STR$":
                    return new BasicValue(new BasicValue(One(name, arguments, line)).ToPrint().TrimEnd());
                case "VAL":
                    return new BasicValue(Val(Text(name, arguments, 0, line)));
                case "UCASE$":
                    return new BasicValue(Text(name, arguments, 0, line).ToUpperInvariant());
                case "LCASE$":
                    return new BasicValue(Text(name, arguments, 0, line).ToLowerInvariant());
                case "LTRIM$":
                    return new BasicValue(Text(name, arguments, 0, line).TrimStart());
                case "RTRIM$":
                    return new BasicValue(Text(name, arguments, 0, line).TrimEnd());
                case "SPACE$":
                    return new BasicValue(new string(' ', Count(One(name, arguments, line), line)));
                case "STRING$":
                    return StringOf(arguments, line);
                case "LEFT$":
                    return Left(arguments, line);
                case "RIGHT$":
                    return Right(arguments, line);
                case "MID$":
                    return Mid(arguments, line);
                case "INSTR":
                    return Instr(arguments, line);
                default:
                    throw new BasicError("Undefined function " + name, line);
            }
        }

        /// <summary>
        ///     RND, with the three behaviours BASIC gives it: a positive argument or none is the next number, zero
        ///     repeats the last one, and a negative reseeds. The middle one exists so a program can look at what it
        ///     just got without drawing again, and losing it makes a subtly different game.
        /// </summary>
        private static BasicValue Rnd(IReadOnlyList<BasicValue> arguments, BasicRuntime runtime, int line)
        {
            if (arguments.Count > 0)
            {
                var argument = arguments[0].AsNumber(line);

                if (Math.Abs(argument) < double.Epsilon)
                    return new BasicValue(runtime.LastRandom);

                if (argument < 0)
                    runtime.Reseed((int) argument);
            }

            runtime.LastRandom = runtime.Random.NextDouble();
            return new BasicValue(runtime.LastRandom);
        }

        /// <summary>LEFT$, clamped rather than throwing when asked for more than there is.</summary>
        private static BasicValue Left(IReadOnlyList<BasicValue> arguments, int line)
        {
            Expect("LEFT$", arguments, 2, line);

            var text = arguments[0].AsText(line);
            var take = Math.Clamp(Count(arguments[1].AsNumber(line), line), 0, text.Length);

            return new BasicValue(text.Substring(0, take));
        }

        /// <summary>RIGHT$, clamped the same way.</summary>
        private static BasicValue Right(IReadOnlyList<BasicValue> arguments, int line)
        {
            Expect("RIGHT$", arguments, 2, line);

            var text = arguments[0].AsText(line);
            var take = Math.Clamp(Count(arguments[1].AsNumber(line), line), 0, text.Length);

            return new BasicValue(text.Substring(text.Length - take, take));
        }

        /// <summary>
        ///     MID$, whose start counts from one rather than zero. Everything else in this file is arithmetic; this
        ///     is the one where an off-by-one silently returns the wrong characters instead of failing.
        /// </summary>
        private static BasicValue Mid(IReadOnlyList<BasicValue> arguments, int line)
        {
            if (arguments.Count is not (2 or 3))
                throw new BasicError("MID$ takes two or three arguments", line);

            var text = arguments[0].AsText(line);
            var start = Count(arguments[1].AsNumber(line), line);

            if (start < 1)
                throw new BasicError("MID$ starts counting at one", line);

            if (start > text.Length)
                return BasicValue.EmptyString;

            var available = text.Length - (start - 1);
            var take = arguments.Count == 3
                ? Math.Clamp(Count(arguments[2].AsNumber(line), line), 0, available)
                : available;

            return new BasicValue(text.Substring(start - 1, take));
        }

        /// <summary>INSTR, with the optional start position BASIC allows in front of the string.</summary>
        private static BasicValue Instr(IReadOnlyList<BasicValue> arguments, int line)
        {
            if (arguments.Count is not (2 or 3))
                throw new BasicError("INSTR takes two or three arguments", line);

            var offset = arguments.Count == 3 ? 1 : 0;
            var start = arguments.Count == 3 ? Count(arguments[0].AsNumber(line), line) : 1;

            var haystack = arguments[offset].AsText(line);
            var needle = arguments[offset + 1].AsText(line);

            if (start < 1)
                throw new BasicError("INSTR starts counting at one", line);

            if (start > haystack.Length)
                return BasicValue.Zero;

            // Found or not found, the answer is a position counting from one, and zero means not found. That is why
            // a BASIC program tests INSTR against zero rather than against minus one.
            var at = haystack.IndexOf(needle, start - 1, StringComparison.Ordinal);
            return new BasicValue(at < 0 ? 0 : at + 1);
        }

        /// <summary>STRING$, which takes either a character code or a string to take the first character of.</summary>
        private static BasicValue StringOf(IReadOnlyList<BasicValue> arguments, int line)
        {
            Expect("STRING$", arguments, 2, line);

            var count = Count(arguments[0].AsNumber(line), line);
            var second = arguments[1];

            var character = second.IsString
                ? second.Text.Length > 0 ? second.Text[0] : ' '
                : (char) (int) second.Number;

            return new BasicValue(new string(character, count));
        }

        /// <summary>
        ///     VAL, which reads as much of a number as the string starts with and answers zero when it starts with
        ///     none. It does not fail on trailing rubbish, because that is what makes it useful for reading input.
        /// </summary>
        private static double Val(string text)
        {
            var at = 0;
            while (at < text.Length && char.IsWhiteSpace(text[at]))
                at++;

            var start = at;
            if (at < text.Length && (text[at] == '-' || text[at] == '+'))
                at++;

            while (at < text.Length && char.IsDigit(text[at]))
                at++;

            if (at < text.Length && text[at] == '.')
            {
                at++;
                while (at < text.Length && char.IsDigit(text[at]))
                    at++;
            }

            var number = text.Substring(start, at - start);
            return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0d;
        }

        /// <summary>The single numeric argument of a one-argument function.</summary>
        private static double One(string name, IReadOnlyList<BasicValue> arguments, int line)
        {
            Expect(name, arguments, 1, line);
            return arguments[0].AsNumber(line);
        }

        /// <summary>One string argument of a function.</summary>
        private static string Text(string name, IReadOnlyList<BasicValue> arguments, int index, int line)
        {
            Expect(name, arguments, index + 1, line);
            return arguments[index].AsText(line);
        }

        /// <summary>Refuses the wrong number of arguments by name, so the message says which function.</summary>
        private static void Expect(string name, IReadOnlyList<BasicValue> arguments, int count, int line)
        {
            if (arguments.Count != count)
                throw new BasicError("Wrong number of arguments to " + name, line);
        }

        /// <summary>A count, refused when it is negative.</summary>
        private static int Count(double value, int line)
        {
            var count = (int) Math.Truncate(value);
            if (count < 0)
                throw new BasicError("Illegal function call, a count cannot be negative", line);

            return count;
        }
    }
}
