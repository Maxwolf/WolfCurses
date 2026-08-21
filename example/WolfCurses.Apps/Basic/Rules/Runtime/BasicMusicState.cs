// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     What PLAY remembers between one string and the next: which octave it is in, how long a note lasts by
    ///     default, and how fast it is going.
    ///     <para>
    ///         <b>It survives across statements on purpose.</b> Programs are written as <c>PLAY "T120 L8"</c> and
    ///         then <c>PLAY "CDEFG"</c> further down, so a parser that started fresh each time would play the second
    ///         line at the wrong speed and the wrong length. That is why this lives on the runtime rather than
    ///         inside the parser.
    ///     </para>
    /// </summary>
    public sealed class BasicMusicState
    {
        /// <summary>Which octave, where four holds middle C.</summary>
        public int Octave { get; set; } = 4;

        /// <summary>The default note length, as a fraction of a whole note: four is a quarter note.</summary>
        public int Length { get; set; } = 4;

        /// <summary>Quarter notes per minute.</summary>
        public int Tempo { get; set; } = 120;
    }
}
