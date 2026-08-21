// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     EXIT FOR and EXIT DO: leave the loop rather than finishing it.
    ///     <para>
    ///         <b>A jump on its own would not do</b>, which is the only interesting thing here. A running FOR has a
    ///         frame on the stack holding its limit and step, and NEXT is what normally takes it off; leaving by
    ///         jumping past the NEXT would leave that frame behind for the next NEXT anywhere in the program to
    ///         step instead of its own. A DO has no frame, so EXIT DO really is only a jump.
    ///     </para>
    /// </summary>
    public sealed class BasicExitLoopStatement : BasicStatement
    {
        /// <summary>Whether a FOR frame has to come off the stack on the way out.</summary>
        private readonly bool _popsLoop;

        /// <summary>Initializes a new instance of the <see cref="BasicExitLoopStatement" /> class.</summary>
        /// <param name="popsLoop">TRUE for EXIT FOR, FALSE for EXIT DO.</param>
        /// <param name="line">The source line.</param>
        public BasicExitLoopStatement(bool popsLoop, int line) : base(line)
        {
            _popsLoop = popsLoop;
            Target = -1;
        }

        /// <summary>Where to land, patched when the parser reaches the end of the loop.</summary>
        public int Target { get; set; }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            if (_popsLoop && runtime.Loops.Count > 0)
                runtime.Loops.Pop();

            return Target;
        }
    }
}
