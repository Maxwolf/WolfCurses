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

        /// <summary>
        ///     Switches to a graphics mode, or back to text with mode zero.
        ///     <para>
        ///         The mode numbers are the ones BASIC programs write, and each carries its own size: what a program
        ///         means by <c>PSET (319, 199)</c> depends entirely on which SCREEN it asked for, so the mode has to
        ///         set the coordinate space rather than the screen guessing at one.
        ///     </para>
        /// </summary>
        /// <param name="mode">The screen mode.</param>
        void SetScreenMode(int mode);

        /// <summary>
        ///     Where the last drawing statement finished. BASIC lets a program write <c>LINE -(x, y)</c> and carry
        ///     on from wherever it left off, so the screen has to remember it: nothing else can.
        /// </summary>
        int LastX { get; }

        /// <summary>Where the last drawing statement finished, down the screen.</summary>
        int LastY { get; }

        /// <summary>How many pixels across the current mode is.</summary>
        int ScreenWidth { get; }

        /// <summary>How many pixels down the current mode is.</summary>
        int ScreenHeight { get; }

        /// <summary>Sets one pixel.</summary>
        /// <param name="x">Across.</param>
        /// <param name="y">Down.</param>
        /// <param name="color">The colour number, or -1 for the current foreground.</param>
        void Plot(int x, int y, int color);

        /// <summary>Draws a line, a box, or a filled box.</summary>
        /// <param name="x0">Where it starts, across.</param>
        /// <param name="y0">Where it starts, down.</param>
        /// <param name="x1">Where it ends, across.</param>
        /// <param name="y1">Where it ends, down.</param>
        /// <param name="color">The colour number, or -1 for the current foreground.</param>
        /// <param name="box">Empty for a line, B for a box, BF for a filled one.</param>
        void DrawLine(int x0, int y0, int x1, int y1, int color, string box);

        /// <summary>Draws the outline of a circle.</summary>
        /// <param name="x">Its centre, across.</param>
        /// <param name="y">Its centre, down.</param>
        /// <param name="radius">Its radius in pixels.</param>
        /// <param name="color">The colour number, or -1 for the current foreground.</param>
        void DrawCircle(int x, int y, int radius, int color);

        /// <summary>
        ///     What colour a pixel is, or -1 for one that is off the screen. GET needs it, and it is the only way
        ///     anything reads the screen back rather than writing to it.
        /// </summary>
        /// <param name="x">Across.</param>
        /// <param name="y">Down.</param>
        /// <returns>The colour number, or -1.</returns>
        int PixelAt(int x, int y);

        /// <summary>
        ///     A pitch for a length of time.
        ///     <para>
        ///         A frequency of zero is a rest, which is how PLAY expresses a pause without a second kind of note.
        ///     </para>
        /// </summary>
        /// <param name="frequency">The pitch in hertz.</param>
        /// <param name="milliseconds">How long it lasts.</param>
        void Sound(double frequency, double milliseconds);

        /// <summary>Floods an area with colour, stopping at a border.</summary>
        /// <param name="x">Where to start, across.</param>
        /// <param name="y">Where to start, down.</param>
        /// <param name="fill">The colour to flood with, or -1 for the current foreground.</param>
        /// <param name="border">The colour to stop at, or -1 to stop at the fill colour.</param>
        void Paint(int x, int y, int fill, int border);
    }
}
