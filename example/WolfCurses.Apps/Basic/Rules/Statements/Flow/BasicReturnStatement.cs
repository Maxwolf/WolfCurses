// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     RETURN: go back to just after the GOSUB.
    ///     <para>
    ///         A RETURN with nothing to return to is an error rather than an end, because it means the program fell
    ///         into a subroutine it never called, which is a real and common BASIC mistake worth naming.
    ///     </para>
    /// </summary>
    public sealed class BasicReturnStatement : BasicStatement
    {
        /// <summary>Initializes a new instance of the <see cref="BasicReturnStatement" /> class.</summary>
        /// <param name="line">The source line.</param>
        public BasicReturnStatement(int line) : base(line)
        {
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            if (runtime.ReturnAddresses.Count == 0)
                throw new BasicError("RETURN without GOSUB", Line);

            return runtime.ReturnAddresses.Pop();
        }
    }
}
