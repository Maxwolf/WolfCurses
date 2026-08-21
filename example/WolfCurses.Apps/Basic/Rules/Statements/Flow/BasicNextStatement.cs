// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>NEXT: move the variable on and go round again, or fall out of the loop.</summary>
    public sealed class BasicNextStatement : BasicStatement
    {
        /// <summary>The variable the program named, or null when it just wrote NEXT.</summary>
        private readonly string _variable;

        /// <summary>Initializes a new instance of the <see cref="BasicNextStatement" /> class.</summary>
        /// <param name="variable">The variable the program named, or null.</param>
        /// <param name="line">The source line.</param>
        public BasicNextStatement(string variable, int line) : base(line)
        {
            _variable = variable;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            if (runtime.Loops.Count == 0)
                throw new BasicError("NEXT without FOR", Line);

            var frame = runtime.Loops.Peek();

            // A NEXT naming the wrong variable is a real mistake rather than a harmless one: it means the loops are
            // crossed over, and running it anyway would produce nonsense somewhere else entirely.
            if (_variable != null && !string.Equals(_variable, frame.Variable, System.StringComparison.Ordinal))
                throw new BasicError("NEXT " + _variable + " does not match FOR " + frame.Variable, Line);

            var value = runtime.Read(frame.Variable).AsNumber(Line) + frame.Step;
            runtime.Write(frame.Variable, new BasicValue(value), Line);

            if (BasicForStatement.Finished(value, frame.Limit, frame.Step))
            {
                runtime.Loops.Pop();
                return index + 1;
            }

            return frame.BodyIndex;
        }
    }
}
