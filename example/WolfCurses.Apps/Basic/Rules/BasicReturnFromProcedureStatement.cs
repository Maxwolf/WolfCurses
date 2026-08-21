// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     END SUB, END FUNCTION, and what EXIT SUB jumps to: throw the locals away and carry on where the call was.
    ///     <para>
    ///         A body that is simply fallen into rather than called has nothing to return to, which happens when a
    ///         program runs off the end of itself into its own procedures. The parser jumps over every body to stop
    ///         that, so reaching one of these with no scope open means the program jumped somewhere it should not
    ///         have, and saying so is better than quietly ending.
    ///     </para>
    /// </summary>
    public sealed class BasicReturnFromProcedureStatement : BasicStatement
    {
        /// <summary>Initializes a new instance of the <see cref="BasicReturnFromProcedureStatement" /> class.</summary>
        /// <param name="line">The source line.</param>
        public BasicReturnFromProcedureStatement(int line) : base(line)
        {
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var scope = runtime.CurrentScope;
            if (scope == null)
                throw new BasicError("END SUB or END FUNCTION reached without a call", Line);

            runtime.LeaveProcedure();
            return scope.ReturnTo;
        }
    }
}
