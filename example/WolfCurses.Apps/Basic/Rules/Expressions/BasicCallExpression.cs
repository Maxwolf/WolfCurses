// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A name followed by a bracketed list, which in BASIC is genuinely ambiguous: <c>A(1)</c> is either an
    ///     array element or a call to a function called A, and the source does not say which.
    ///     <para>
    ///         <b>So it is not decided until it runs.</b> Anything that has been dimensioned is an array, anything
    ///         with a built-in of that name is a function, and nothing else is an error naming the thing it could
    ///         not find. Deciding at parse time would mean the parser had to see every DIM first, which is not true
    ///         of a program that dimensions inside a branch.
    ///     </para>
    /// </summary>
    public sealed class BasicCallExpression : BasicExpression
    {
        /// <summary>The bracketed arguments, which may be subscripts.</summary>
        private readonly IReadOnlyList<BasicExpression> _arguments;

        /// <summary>Initializes a new instance of the <see cref="BasicCallExpression" /> class.</summary>
        /// <param name="name">The name in front of the bracket, uppercased.</param>
        /// <param name="arguments">What is inside the bracket.</param>
        /// <param name="line">The source line.</param>
        public BasicCallExpression(string name, IReadOnlyList<BasicExpression> arguments, int line) : base(line)
        {
            Name = name;
            _arguments = arguments;
        }

        /// <summary>The name in front of the bracket.</summary>
        public string Name { get; }

        /// <inheritdoc />
        public override BasicValue Evaluate(BasicRuntime runtime)
        {
            if (runtime.IsArray(Name))
                return runtime.ReadElement(Name, Subscripts(runtime), Line);

            var procedure = runtime.FindProcedure(Name);
            if (procedure is {IsFunction: true})
                return CallFunction(procedure, runtime);

            // Not an array and not a function, so it was meant to be an array: that is far and away the likelier
            // mistake, and "undefined function" would send somebody looking for a spelling error in a name they
            // never intended as a function.
            if (!BasicFunctions.Exists(Name))
                throw new BasicError("Array " + Name + " has not been dimensioned", Line);

            var values = new List<BasicValue>(_arguments.Count);
            foreach (var argument in _arguments)
                values.Add(argument.Evaluate(runtime));

            return BasicFunctions.Call(Name, values, runtime, Line);
        }

        /// <summary>
        ///     Runs a user FUNCTION and hands back what it assigned to its own name.
        ///     <para>
        ///         <b>This runs the body to completion, which a SUB call deliberately does not.</b> An expression is
        ///         evaluated in the middle of a statement and there is nowhere to suspend to: the value has to be
        ///         there before the statement it appears in can finish. So a FUNCTION is the one thing here that
        ///         cannot be stepped, and an endless loop inside one would hang the screen. That is what the step
        ///         cap is for, and it is why a program's main loop belongs in a SUB.
        ///     </para>
        /// </summary>
        /// <param name="procedure">The function being called.</param>
        /// <param name="runtime">The running program.</param>
        /// <returns>Its result.</returns>
        private BasicValue CallFunction(BasicProcedure procedure, BasicRuntime runtime)
        {
            if (runtime.Program == null)
                throw new BasicError("Cannot call " + Name + " here", Line);

            // Bound before the scope is pushed, so the arguments mean what they mean at the call.
            var scope = runtime.EnterProcedure(procedure, BasicRuntime.Bind(procedure, _arguments, runtime, Line), -1,
                Line);

            var index = procedure.BodyIndex;
            var spent = 0;

            while (runtime.CurrentScope == scope && runtime.Program.IsRunning(index))
            {
                if (spent++ > StepLimit)
                {
                    runtime.LeaveProcedure();
                    throw new BasicError(procedure.Name + " ran for too long without returning", Line);
                }

                index = runtime.Program.Step(runtime, index, 1);
            }

            // The body may have run off its own end rather than returning, which leaves the scope open.
            if (runtime.CurrentScope == scope)
                runtime.LeaveProcedure();

            // Its result is whatever it last assigned to its own name, which is how BASIC returns a value. The
            // scope object is still held here after being popped, which is what makes that readable.
            return scope.Variables.TryGetValue(procedure.Name, out var result)
                ? result
                : BasicRuntime.IsStringName(procedure.Name) ? BasicValue.EmptyString : BasicValue.Zero;
        }

        /// <summary>How many statements a single function call may run before it is called a mistake.</summary>
        private const int StepLimit = 500000;

        /// <summary>The arguments as array subscripts.</summary>
        private int[] Subscripts(BasicRuntime runtime)
        {
            var subscripts = new int[_arguments.Count];
            for (var i = 0; i < _arguments.Count; i++)
                subscripts[i] = (int) _arguments[i].Evaluate(runtime).AsNumber(Line);

            return subscripts;
        }
    }
}
