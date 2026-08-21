// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A parsed program, and the loop that runs it.
    ///     <para>
    ///         <b>Running is a program counter over a flat list</b>, which is all that is left once the parser has
    ///         compiled every block into jumps. There is no call stack to walk and no tree to descend: each
    ///         statement is asked to do its thing and to say which statement comes next.
    ///     </para>
    ///     <para>
    ///         <b><see cref="Step" /> is the interesting half, not <see cref="Run" />.</b> A BASIC program is
    ///         entitled to loop forever, which is what a game does between frames, so a screen that ran one to
    ///         completion would simply stop responding. Stepping a bounded number of statements and coming back is
    ///         what lets an editor stay alive while a program is running inside it.
    ///     </para>
    /// </summary>
    public sealed class BasicProgram
    {
        /// <summary>The statements, in the order they were compiled.</summary>
        private readonly IReadOnlyList<BasicStatement> _statements;

        /// <summary>Initializes a new instance of the <see cref="BasicProgram" /> class.</summary>
        /// <param name="statements">The compiled statements.</param>
        /// <param name="procedures">The SUBs and FUNCTIONs it declared.</param>
        /// <param name="data">The constants it wrote in DATA statements.</param>
        /// <param name="dataMarks">How much data had been written by each label.</param>
        public BasicProgram(IReadOnlyList<BasicStatement> statements,
            IReadOnlyDictionary<string, BasicProcedure> procedures = null,
            IReadOnlyList<BasicValue> data = null,
            IReadOnlyDictionary<string, int> dataMarks = null)
        {
            _statements = statements;
            Procedures = procedures ?? new Dictionary<string, BasicProcedure>(StringComparer.Ordinal);
            Data = data ?? new List<BasicValue>();
            DataMarks = dataMarks ?? new Dictionary<string, int>(StringComparer.Ordinal);
        }

        /// <summary>The SUBs and FUNCTIONs it declared, by name.</summary>
        public IReadOnlyDictionary<string, BasicProcedure> Procedures { get; }

        /// <summary>The constants it wrote in DATA statements, in source order.</summary>
        public IReadOnlyList<BasicValue> Data { get; }

        /// <summary>How much data had been written by the time each label was reached.</summary>
        public IReadOnlyDictionary<string, int> DataMarks { get; }

        /// <summary>How many statements it compiled to, which is not how many lines were written.</summary>
        public int Count => _statements.Count;

        /// <summary>Parses source into a program.</summary>
        /// <param name="source">The program text.</param>
        /// <returns>The program.</returns>
        public static BasicProgram Compile(string source)
        {
            return BasicParser.Parse(source);
        }

        /// <summary>Whether a counter still points at something to run.</summary>
        /// <param name="index">The counter.</param>
        /// <returns>TRUE when the program is still going.</returns>
        public bool IsRunning(int index)
        {
            return index >= 0 && index < _statements.Count;
        }

        /// <summary>The source line a counter is pointing at, for showing where a running program has got to.</summary>
        /// <param name="index">The counter.</param>
        /// <returns>The line, or zero when the program has finished.</returns>
        public int LineAt(int index)
        {
            return IsRunning(index) ? _statements[index].Line : 0;
        }

        /// <summary>
        ///     Runs up to a number of statements and says where it got to. The budget is what keeps a screen
        ///     responsive: a program in a tight loop hands control back rather than taking the machine with it.
        /// </summary>
        /// <param name="runtime">The running program's state.</param>
        /// <param name="index">Where to carry on from.</param>
        /// <param name="budget">How many statements to run before coming back.</param>
        /// <returns>Where it got to; past the end when the program finished.</returns>
        public int Step(BasicRuntime runtime, int index, int budget)
        {
            Attach(runtime);

            for (var spent = 0; spent < budget && IsRunning(index); spent++)
                index = _statements[index].Execute(runtime, index);

            return index;
        }

        /// <summary>
        ///     Runs the whole thing.
        ///     <para>
        ///         The step cap is a guard rather than a rule: a program that has not finished after this many
        ///         statements is almost certainly looping forever, and in a test that means a hung suite rather than
        ///         a failing one. A screen that wants a program to loop forever uses <see cref="Step" />.
        ///     </para>
        /// </summary>
        /// <param name="runtime">The running program's state.</param>
        /// <param name="maxSteps">The most statements to run before giving up.</param>
        public void Run(BasicRuntime runtime, int maxSteps = 2000000)
        {
            Attach(runtime);
            var index = 0;

            for (var spent = 0; spent < maxSteps; spent++)
            {
                if (!IsRunning(index))
                    return;

                index = _statements[index].Execute(runtime, index);
            }

            throw new BasicError("The program ran for too long without stopping", LineAt(index));
        }

        /// <summary>
        ///     Tells the runtime which program it is running, which a FUNCTION call in the middle of an expression
        ///     needs: it has to run the body it is calling, and an expression has no other way to reach it.
        /// </summary>
        /// <param name="runtime">The running program's state.</param>
        private void Attach(BasicRuntime runtime)
        {
            runtime.Program = this;
            runtime.Procedures = Procedures;
            runtime.Data = Data;
            runtime.DataMarks = DataMarks;
        }
    }
}
