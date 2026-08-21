// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>GOSUB: remember where we are, then go.</summary>
    public sealed class BasicGosubStatement : BasicStatement
    {
        /// <summary>Initializes a new instance of the <see cref="BasicGosubStatement" /> class.</summary>
        /// <param name="line">The source line.</param>
        public BasicGosubStatement(int line) : base(line)
        {
            Target = -1;
        }

        /// <summary>Where to jump to, patched once every label is known.</summary>
        public int Target { get; set; }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            runtime.ReturnAddresses.Push(index + 1);
            return Target;
        }
    }
}
