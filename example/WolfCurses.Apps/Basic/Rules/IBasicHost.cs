// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Everything a running BASIC program can reach outside itself.
    ///     <para>
    ///         <b>The interpreter talks to this and never to a console</b>, which is the same seam every other
    ///         application in this suite keeps between its rules and its rendering. It is what lets a whole program
    ///         be run in a test with its output collected into a list, and it is what will let a screen mode arrive
    ///         later without the interpreter learning anything about pixels.
    ///     </para>
    /// </summary>
    public interface IBasicHost
    {
        /// <summary>Writes text where the cursor is, without moving to a new line.</summary>
        /// <param name="text">The text to write.</param>
        void Write(string text);

        /// <summary>Moves to the start of the next line.</summary>
        void WriteLine();

        /// <summary>Clears the screen and puts the cursor back at the top left.</summary>
        void Clear();

        /// <summary>Moves the cursor.</summary>
        /// <param name="row">The row, counting from one as BASIC does.</param>
        /// <param name="column">The column, counting from one.</param>
        void Locate(int row, int column);

        /// <summary>Sets the colours used by later writing.</summary>
        /// <param name="foreground">The foreground, in the sixteen colour palette.</param>
        /// <param name="background">The background, or -1 to leave it as it is.</param>
        void SetColor(int foreground, int background);

        /// <summary>Asks the user for a line of input.</summary>
        /// <param name="prompt">What to show first.</param>
        /// <returns>What they typed.</returns>
        string ReadLine(string prompt);

        /// <summary>
        ///     The key waiting to be read, or an empty string when there is not one. Never blocks, which is what
        ///     makes it INKEY$ rather than INPUT.
        /// </summary>
        /// <returns>The key, or empty.</returns>
        string ReadKey();

        /// <summary>Makes the noise BEEP makes.</summary>
        void Beep();
    }
}
