// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     One running procedure: its own variables, which of them it has agreed to share with the program outside,
    ///     and where to go when it is finished.
    ///     <para>
    ///         <b>Variables in a procedure are local by default, and that is the whole reason this exists.</b> A SUB
    ///         that uses <c>I</c> for a loop must not stamp on the <c>I</c> the caller was in the middle of, which is
    ///         precisely the bug that makes procedures worth having. SHARED is the opt out, named one variable at a
    ///         time, so sharing is something a procedure asks for rather than something it gets by accident.
    ///     </para>
    /// </summary>
    public sealed class BasicScope
    {
        /// <summary>The names this procedure reads and writes outside itself.</summary>
        private readonly HashSet<string> _shared = new(StringComparer.Ordinal);

        /// <summary>Initializes a new instance of the <see cref="BasicScope" /> class.</summary>
        /// <param name="procedure">Which procedure is running.</param>
        /// <param name="returnTo">Which statement to carry on from when it finishes.</param>
        public BasicScope(BasicProcedure procedure, int returnTo)
        {
            Procedure = procedure;
            ReturnTo = returnTo;
        }

        /// <summary>Which procedure is running.</summary>
        public BasicProcedure Procedure { get; }

        /// <summary>Which statement to carry on from when it finishes.</summary>
        public int ReturnTo { get; }

        /// <summary>
        ///     How many loops and SELECT CASEs were open when this procedure started.
        ///     <para>
        ///         Remembered so that leaving can put them back. EXIT SUB from inside a FOR loop is ordinary BASIC,
        ///         and without this the loop's frame would be left on the stack for the caller to trip over: the
        ///         next NEXT anywhere in the program would step the abandoned loop instead of its own.
        ///     </para>
        /// </summary>
        public int LoopDepth { get; set; }

        /// <summary>How many SELECT CASE values were open when this procedure started.</summary>
        public int SelectDepth { get; set; }

        /// <summary>Its own variables.</summary>
        public Dictionary<string, BasicValue> Variables { get; } = new(StringComparer.Ordinal);

        /// <summary>Its own arrays, which a DIM inside a procedure makes.</summary>
        public Dictionary<string, BasicArray> Arrays { get; } = new(StringComparer.Ordinal);

        /// <summary>Declares a name to mean the one outside rather than a fresh local.</summary>
        /// <param name="name">The variable name.</param>
        public void Share(string name)
        {
            _shared.Add(name);
        }

        /// <summary>Whether a name has been shared out of this procedure.</summary>
        /// <param name="name">The variable name.</param>
        /// <returns>TRUE when it means the outer one.</returns>
        public bool IsShared(string name)
        {
            return _shared.Contains(name);
        }
    }
}
