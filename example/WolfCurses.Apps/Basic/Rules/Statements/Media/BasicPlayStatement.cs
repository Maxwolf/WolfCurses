// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>PLAY: a tune written as text, handed to the screen one note at a time.</summary>
    public sealed class BasicPlayStatement : BasicStatement
    {
        /// <summary>The tune.</summary>
        private readonly BasicExpression _music;

        /// <summary>Initializes a new instance of the <see cref="BasicPlayStatement" /> class.</summary>
        /// <param name="music">The tune.</param>
        /// <param name="line">The source line.</param>
        public BasicPlayStatement(BasicExpression music, int line) : base(line)
        {
            _music = music;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var music = _music.Evaluate(runtime).AsText(Line);

            // The state carries on into the next PLAY, which is why it belongs to the running program rather than
            // to this statement: a tune is very often set up on one line and played on another.
            foreach (var (frequency, milliseconds) in BasicMusic.Parse(music, runtime.Music, Line))
                runtime.Host.Sound(frequency, milliseconds);

            return index + 1;
        }
    }
}
