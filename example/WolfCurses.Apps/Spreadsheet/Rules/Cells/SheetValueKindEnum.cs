// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     What a cell turned out to be once it was worked out. A cell holds text and its <i>value</i> is one of
    ///     these, which is the distinction a spreadsheet is built on: <c>=1+1</c> is stored as five characters and
    ///     is worth the number two.
    /// </summary>
    public enum SheetValueKindEnum
    {
        /// <summary>Nothing was typed in it. Adds as zero and counts as nothing, which are different answers.</summary>
        Empty = 0,

        /// <summary>A number, whether typed as one or worked out by a formula.</summary>
        Number = 1,

        /// <summary>Text, which is anything that is not a number and did not begin with an equals sign.</summary>
        Text = 2,

        /// <summary>
        ///     Something went wrong working it out, and the reason is shown in the cell the way every spreadsheet
        ///     shows it: a short word beginning with a hash. An error is a value rather than an exception, because
        ///     one bad cell must not stop the other four hundred being drawn.
        /// </summary>
        Error = 3
    }
}
