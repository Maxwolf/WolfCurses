// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     One thing a program does.
    ///     <para>
    ///         <b>A program is a flat list of these, not a tree, and that is the whole architecture.</b> BASIC has
    ///         GOTO, and a GOTO into the middle of what a tree-walking interpreter thinks of as a nested block
    ///         cannot be expressed at all. So every block construct is compiled down to conditional jumps over a
    ///         flat list, exactly as a real BASIC does, and running the program is a loop with a program counter.
    ///         GOTO then costs nothing: it sets the counter.
    ///     </para>
    /// </summary>
    public abstract class BasicStatement
    {
        /// <summary>Initializes a new instance of the <see cref="BasicStatement" /> class.</summary>
        /// <param name="line">The source line it came from.</param>
        protected BasicStatement(int line)
        {
            Line = line;
        }

        /// <summary>The source line it came from.</summary>
        public int Line { get; }

        /// <summary>Does the thing.</summary>
        /// <param name="runtime">The running program.</param>
        /// <param name="index">Where this statement sits in the list.</param>
        /// <returns>Which statement to run next, which is normally the one after this.</returns>
        public abstract int Execute(BasicRuntime runtime, int index);
    }
}
