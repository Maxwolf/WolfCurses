// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A declared SUB or FUNCTION: where its body is, what it takes, and how it is finished with.
    ///     <para>
    ///         <b>A FUNCTION returns by assigning to its own name</b>, which is BASIC's way and not an oddity to be
    ///         translated away: inside <c>FUNCTION Double(N)</c> the statement <c>Double = N * 2</c> is the return.
    ///         So the result is simply whatever the local of that name holds when the body ends, and a function that
    ///         never assigns to itself returns zero or the empty string like any other unset variable.
    ///     </para>
    /// </summary>
    public sealed class BasicProcedure
    {
        /// <summary>Initializes a new instance of the <see cref="BasicProcedure" /> class.</summary>
        /// <param name="name">Its name, uppercased.</param>
        /// <param name="isFunction">Whether it hands a value back.</param>
        /// <param name="parameters">The names its arguments arrive under.</param>
        /// <param name="line">The line it was declared on.</param>
        public BasicProcedure(string name, bool isFunction, IReadOnlyList<string> parameters, int line)
        {
            Name = name;
            IsFunction = isFunction;
            Parameters = parameters;
            Line = line;
            BodyIndex = -1;
        }

        /// <summary>Its name.</summary>
        public string Name { get; }

        /// <summary>Whether it hands a value back.</summary>
        public bool IsFunction { get; }

        /// <summary>The names its arguments arrive under.</summary>
        public IReadOnlyList<string> Parameters { get; }

        /// <summary>The line it was declared on.</summary>
        public int Line { get; }

        /// <summary>The first statement of its body, filled in as the parser reads it.</summary>
        public int BodyIndex { get; set; }
    }
}
