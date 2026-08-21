// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     The statements that are just a name and some values: CLS, LOCATE, COLOR, BEEP, RANDOMIZE, END, STOP.
    ///     <para>
    ///         One type for all of them rather than a class each, because they have nothing in common to model and
    ///         nothing to remember: each evaluates its arguments and tells the host. A dozen near-empty classes
    ///         would be a dozen files saying the same thing.
    ///     </para>
    /// </summary>
    public sealed class BasicCommandStatement : BasicStatement
    {
        /// <summary>Its arguments.</summary>
        private readonly IReadOnlyList<BasicExpression> _arguments;

        /// <summary>Which command.</summary>
        private readonly string _name;

        /// <summary>Initializes a new instance of the <see cref="BasicCommandStatement" /> class.</summary>
        /// <param name="name">Which command, uppercased.</param>
        /// <param name="arguments">Its arguments.</param>
        /// <param name="line">The source line.</param>
        public BasicCommandStatement(string name, IReadOnlyList<BasicExpression> arguments, int line) : base(line)
        {
            _name = name;
            _arguments = arguments;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            switch (_name)
            {
                case "CLS":
                    runtime.Host.Clear();
                    break;
                case "BEEP":
                    runtime.Host.Beep();
                    break;
                case "END":
                case "STOP":

                    // Answering with the length of the program is how a stop is expressed without a flag: the
                    // interpreter's loop already ends when the counter runs past the last statement.
                    return int.MaxValue;
                case "RANDOMIZE":
                    runtime.Reseed(_arguments.Count > 0
                        ? (int) _arguments[0].Evaluate(runtime).AsNumber(Line)
                        : Environment.TickCount);
                    break;
                case "LOCATE":
                    runtime.Host.Locate(Argument(runtime, 0, 1), Argument(runtime, 1, 1));
                    break;
                case "COLOR":
                    runtime.Host.SetColor(Argument(runtime, 0, 7), Argument(runtime, 1, -1));
                    break;
                default:
                    throw new BasicError("Unknown statement " + _name, Line);
            }

            return index + 1;
        }

        /// <summary>One argument, or a stand-in when the program left it out, which BASIC lets it do.</summary>
        private int Argument(BasicRuntime runtime, int position, int fallback)
        {
            if (position >= _arguments.Count || _arguments[position] == null)
                return fallback;

            return (int) _arguments[position].Evaluate(runtime).AsNumber(Line);
        }
    }
}
