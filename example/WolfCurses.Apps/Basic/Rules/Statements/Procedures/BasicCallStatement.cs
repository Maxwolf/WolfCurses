// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Calling a SUB.
    ///     <para>
    ///         <b>It jumps rather than running the procedure to completion</b>, which matters more than it sounds. A
    ///         QBasic program's main loop is very often a SUB that never returns, and a call that ran the body out
    ///         before coming back would take the screen with it. Jumping means the statements inside a SUB are
    ///         stepped exactly like the ones outside, so ESC still works and the interface stays alive.
    ///     </para>
    ///     <para>
    ///         Arguments are worked out in the caller's scope and then bound in the new one, in that order, which is
    ///         what makes <c>Swap A, B</c> pass the caller's values rather than the callee's uninitialised locals.
    ///     </para>
    /// </summary>
    public sealed class BasicCallStatement : BasicStatement
    {
        /// <summary>What to pass.</summary>
        private readonly IReadOnlyList<BasicExpression> _arguments;

        /// <summary>What to call.</summary>
        private readonly string _name;

        /// <summary>Initializes a new instance of the <see cref="BasicCallStatement" /> class.</summary>
        /// <param name="name">What to call, uppercased.</param>
        /// <param name="arguments">What to pass.</param>
        /// <param name="line">The source line.</param>
        public BasicCallStatement(string name, IReadOnlyList<BasicExpression> arguments, int line) : base(line)
        {
            _name = name;
            _arguments = arguments;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var procedure = runtime.FindProcedure(_name);
            if (procedure == null)
                throw new BasicError("Undefined subprogram " + _name, Line);

            runtime.EnterProcedure(procedure, BasicRuntime.Bind(procedure, _arguments, runtime, Line), index + 1,
                Line);

            return procedure.BodyIndex;
        }
    }
}
