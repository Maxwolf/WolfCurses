// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using WolfCurses.Utility;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     The suite menu. Each value becomes a numbered choice on <see cref="OfficeWindow" />, and the
    ///     <see cref="DescriptionAttribute" /> is the line the user reads.
    ///     <para>
    ///         Only Quit so far. Applications are added <b>above</b> it and Quit is renumbered to stay last, because
    ///         the number printed beside a choice is the enum member's own value: appending after Quit would render
    ///         the menu as 1 2 3 4 5 7 6. Renumbering is allowed here and not in the library, since nothing persists
    ///         these values and the example apps are exempt from the library's enum contract tests.
    ///     </para>
    /// </summary>
    public enum OfficeCommandsEnum
    {
        /// <summary>
        ///     A full-screen text editor over the library's <see cref="WolfCurses.Documents.TextBuffer" />: the only
        ///     screen in this repository with a caret in it, and the reason that type exists at all.
        /// </summary>
        [Description("Word processor - edit a document.")] WordProcessor = 1,

        /// <summary>
        ///     A BASIC environment after the one that shipped with MS-DOS: a program in an editor, and the screen it
        ///     draws on when you run it. The editing is the library's, the language is this application's own.
        /// </summary>
        [Description("BASIC - write and run a program.")] Basic = 2,

        /// <summary>
        ///     A grid of cells with formulas in some of them, scrollbars round it and a chart a keystroke away.
        ///     The editing is the library's text buffer, the scrolling is its table viewport, and the charts are
        ///     its own widgets; what is this application's is what the cells mean.
        /// </summary>
        [Description("Spreadsheet - a grid, some sums and a chart.")] Spreadsheet = 3,

        /// <summary>
        ///     A desk calculator with a paper tape. The screen in the suite that is about the mouse as labelled
        ///     buttons: its keys are not all the same width, so the layout has to be remembered rather than
        ///     recomputed, which is what the library's keypad control is for.
        /// </summary>
        [Description("Calculator - a desk calculator with a tape.")] Calculator = 4,

        /// <summary>
        ///     A calendar and planner. The screen in the suite that is about time passing: the holidays are worked
        ///     out for whichever year you page into rather than looked up, and the clock moves while you watch it.
        /// </summary>
        [Description("Planner - a calendar with a clock in it.")] Planner = 5,

        /// <summary>
        ///     A card index of contacts. The screen in the suite that has to distrust a file it wrote itself: what
        ///     it saves can be hand-edited in the word processor two menu items up and opened here again, so it
        ///     reads its own columns by name rather than by position.
        /// </summary>
        [Description("Card file - an address book you can flip through.")] CardFile = 6,

        /// <summary>
        ///     A media player, over whatever ffmpeg the machine already has. The screen in the suite that has to
        ///     keep up with a clock nothing here controls: a frame belongs at a moment, and a terminal that was
        ///     busy when the moment came has to skip to the one that belongs now rather than show the late one.
        /// </summary>
        [Description("Media player - watch a film in the terminal.")] MediaPlayer = 7,

        /// <summary>Closes the suite and returns to the operating system.</summary>
        [Description("Quit.")] Quit = 8
    }
}
