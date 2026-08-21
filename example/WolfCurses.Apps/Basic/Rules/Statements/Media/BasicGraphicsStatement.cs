// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     The drawing statements: SCREEN, PSET, PRESET, LINE, CIRCLE and PAINT.
    ///     <para>
    ///         One type for all of them because they have the same shape once the parser has finished with them:
    ///         some numbers and a name. The interesting part of drawing in BASIC is the syntax, which is unlike
    ///         anything else in the language (<c>LINE (0,0)-(9,9), 2, BF</c>), and that lives in the parser; by the
    ///         time it arrives here it is a flat list of expressions.
    ///     </para>
    ///     <para>
    ///         <b>A missing colour is -1 rather than a default number</b>, because "the colour COLOR last set" is
    ///         something only the screen knows, and passing 0 would silently draw everything in black.
    ///     </para>
    /// </summary>
    public sealed class BasicGraphicsStatement : BasicStatement
    {
        /// <summary>Its arguments, already flattened by the parser.</summary>
        private readonly IReadOnlyList<BasicExpression> _arguments;

        /// <summary>Which statement.</summary>
        private readonly string _name;

        /// <summary>B or BF for LINE, empty otherwise.</summary>
        private readonly string _option;

        /// <summary>Initializes a new instance of the <see cref="BasicGraphicsStatement" /> class.</summary>
        /// <param name="name">Which statement, uppercased.</param>
        /// <param name="arguments">Its arguments, with nulls where the program left one out.</param>
        /// <param name="option">B or BF for LINE.</param>
        /// <param name="line">The source line.</param>
        public BasicGraphicsStatement(string name, IReadOnlyList<BasicExpression> arguments, string option, int line)
            : base(line)
        {
            _name = name;
            _arguments = arguments;
            _option = option ?? string.Empty;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var host = runtime.Host;

            switch (_name)
            {
                case "SCREEN":
                    host.SetScreenMode(Argument(runtime, 0, 0));
                    break;
                case "PSET":
                    host.Plot(Argument(runtime, 0, 0), Argument(runtime, 1, 0), Argument(runtime, 2, -1));
                    break;
                case "PRESET":

                    // PRESET is PSET in the background colour, which is how a program rubs a pixel out.
                    host.Plot(Argument(runtime, 0, 0), Argument(runtime, 1, 0), Argument(runtime, 2, 0));
                    break;
                case "LINE":

                    // A LINE written without its first point starts where the last one finished, which is how a
                    // program draws a path without repeating a coordinate on every line.
                    host.DrawLine(Argument(runtime, 0, host.LastX), Argument(runtime, 1, host.LastY),
                        Argument(runtime, 2, 0), Argument(runtime, 3, 0), Argument(runtime, 4, -1), _option);
                    break;
                case "CIRCLE":
                    host.DrawCircle(Argument(runtime, 0, 0), Argument(runtime, 1, 0), Argument(runtime, 2, 0),
                        Argument(runtime, 3, -1));
                    break;
                case "PAINT":
                    host.Paint(Argument(runtime, 0, 0), Argument(runtime, 1, 0), Argument(runtime, 2, -1),
                        Argument(runtime, 3, -1));
                    break;
                default:
                    throw new BasicError("Unknown statement " + _name, Line);
            }

            return index + 1;
        }

        /// <summary>One argument, rounded to a pixel, or a stand-in when the program left it out.</summary>
        private int Argument(BasicRuntime runtime, int position, int fallback)
        {
            if (position >= _arguments.Count || _arguments[position] == null)
                return fallback;

            // Truncated rather than rounded, which is what BASIC does with a coordinate and is visible on any
            // program that steps by a fraction: rounding would make a slow diagonal wobble.
            return (int) Math.Truncate(_arguments[position].Evaluate(runtime).AsNumber(Line));
        }
    }
}
