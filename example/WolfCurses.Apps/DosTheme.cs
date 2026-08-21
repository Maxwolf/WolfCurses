// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using WolfCurses.Graphics;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     The MS-DOS Editor's colours, which cost nothing to reproduce because of one accident of history: the
    ///     sixteen console colours <i>are</i> the EGA palette that editor was drawn in. Gray really is the silver its
    ///     menu bar was painted with, dark blue really is the field, dark cyan really is the status strip along the
    ///     bottom.
    ///     <para>
    ///         Named colours rather than exact <see cref="Rgb24" /> values, deliberately and for the same reason
    ///         Minesweeper's Windows 95 panel uses them: a named colour is resolved by the terminal, so the screen
    ///         still follows whatever theme the user has chosen instead of insisting on 1991 in the middle of it.
    ///         The shape is authentic; the exact hue is the terminal's business.
    ///     </para>
    ///     <para>
    ///         Every one of these degrades on its own. At a resolved colour mode of none the styles emit nothing at
    ///         all and the editor is plain text with a box drawn round it, which is the library's standing rule
    ///         rather than anything arranged here.
    ///     </para>
    /// </summary>
    internal static class DosTheme
    {
        /// <summary>The document field: light text on the deep blue everyone remembers.</summary>
        public static TextStyle Field { get; } = new(ConsoleColor.Gray, ConsoleColor.DarkBlue);

        /// <summary>The caret and the selection, which is the field's colours turned round.</summary>
        public static TextStyle Selection { get; } = new(ConsoleColor.DarkBlue, ConsoleColor.Gray);

        /// <summary>
        ///     The cell the mouse pointer is over. A terminal draws no pointer of its own once mouse reporting is
        ///     on, so the editor has to draw one, exactly as the MS-DOS Editor did: a lit block that follows the
        ///     mouse. It is deliberately a different colour from the caret, or there would be no telling which of
        ///     the two lit cells the keyboard is about to type into.
        /// </summary>
        public static TextStyle Pointer { get; } = new(ConsoleColor.Black, ConsoleColor.DarkCyan);

        /// <summary>The frame around the field, brighter than the text inside it.</summary>
        public static TextStyle Frame { get; } = new(ConsoleColor.White, ConsoleColor.DarkBlue);

        /// <summary>The file name, which sits in a lit tab notched into the top of the frame.</summary>
        public static TextStyle Title { get; } = new(ConsoleColor.DarkBlue, ConsoleColor.Gray);

        /// <summary>
        ///     A column letter or a row number down the side of a grid. The same silver as the menu bar, which is
        ///     what a spreadsheet's headings have always been: raised, and plainly not part of the data.
        /// </summary>
        public static TextStyle Header { get; } = new(ConsoleColor.Black, ConsoleColor.Gray);

        /// <summary>
        ///     The heading of the row or column the cursor is in. Worth picking out because on a wide grid the
        ///     cursor is the only thing saying where you are, and it is one lit cell among several hundred.
        /// </summary>
        public static TextStyle HeaderActive { get; } = new(ConsoleColor.White, ConsoleColor.Black);

        /// <summary>
        ///     A cell inside a swept selection but not the one the keyboard is on. Deliberately a different colour
        ///     from <see cref="Selection" /> rather than the same one: a range and the cell within it that will
        ///     receive the next keystroke are different things, and drawing them alike hides the second.
        /// </summary>
        public static TextStyle Highlight { get; } = new(ConsoleColor.White, ConsoleColor.DarkCyan);

        /// <summary>The menu bar across the top.</summary>
        public static TextStyle MenuBar { get; } = new(ConsoleColor.Black, ConsoleColor.Gray);

        /// <summary>The open menu's title, and the entry under the cursor.</summary>
        public static TextStyle MenuHighlight { get; } = new(ConsoleColor.White, ConsoleColor.Black);

        /// <summary>A dropped panel, which is the same silver as the bar it came from.</summary>
        public static TextStyle MenuPanel { get; } = new(ConsoleColor.Black, ConsoleColor.Gray);

        /// <summary>
        ///     A menu entry that cannot be chosen just now. The same silver panel with the text greyed, which is
        ///     what every menu since the first one has done and the only thing that explains why Cut does nothing
        ///     when nothing is selected.
        /// </summary>
        public static TextStyle MenuDisabled { get; } = new(ConsoleColor.DarkGray, ConsoleColor.Gray);

        /// <summary>The key-hint strip along the bottom.</summary>
        public static TextStyle Status { get; } = new(ConsoleColor.Black, ConsoleColor.DarkCyan);

        /// <summary>A scrollbar's empty track, dithered against the field.</summary>
        public static TextStyle ScrollTrack { get; } = new(ConsoleColor.DarkGray, ConsoleColor.Gray);

        /// <summary>A scrollbar's thumb.</summary>
        public static TextStyle ScrollThumb { get; } = new(ConsoleColor.Black, ConsoleColor.Gray);

        /// <summary>A scrollbar's arrow caps.</summary>
        public static TextStyle ScrollArrow { get; } = new(ConsoleColor.Black, ConsoleColor.Gray);
    }
}
