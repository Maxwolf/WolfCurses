// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     SHARED: the named variables mean the ones outside this procedure rather than fresh locals.
    ///     <para>
    ///         It takes effect when it runs rather than when it is read, which matches how the rest of this
    ///         interpreter works and is why it belongs at the top of a procedure: a SHARED after the variable has
    ///         already been written locally shares the name from that point on, and the local value written before
    ///         it is simply lost.
    ///     </para>
    /// </summary>
    public sealed class BasicSharedStatement : BasicStatement
    {
        /// <summary>The names to share.</summary>
        private readonly IReadOnlyList<string> _names;

        /// <summary>Initializes a new instance of the <see cref="BasicSharedStatement" /> class.</summary>
        /// <param name="names">The names to share.</param>
        /// <param name="line">The source line.</param>
        public BasicSharedStatement(IReadOnlyList<string> names, int line) : base(line)
        {
            _names = names;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var scope = runtime.CurrentScope;
            if (scope == null)
                throw new BasicError("SHARED is only meaningful inside a SUB or FUNCTION", Line);

            foreach (var name in _names)
                scope.Share(name);

            return index + 1;
        }
    }
}
