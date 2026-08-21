// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using WolfCurses.Window;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     State shared between the menu and the applications attached to it. A window's data object outlives its
    ///     forms, which is what makes it the right home for anything one application hands to another.
    /// </summary>
    public sealed class AppsWindowInfo : WindowData
    {
        /// <summary>
        ///     The suite clipboard: what was last cut or copied, waiting to be pasted.
        ///     <para>
        ///         It lives here rather than inside the editor precisely because a clipboard whose contents die with
        ///         the screen that filled it is not a clipboard. A paragraph copied in the word processor has to
        ///         survive that editor being closed before it can land in a spreadsheet cell, and this object is the
        ///         only thing in the suite that outlives both of them.
        ///     </para>
        ///     <para>
        ///         The suite's own, not the operating system's. Reaching the real clipboard means platform interop,
        ///         which the library has none of on purpose, so text copied here does not leave the program.
        ///     </para>
        /// </summary>
        public string Clipboard { get; set; }

        /// <summary>Whether there is anything to paste, which is what a Paste menu entry asks before lighting up.</summary>
        public bool HasClipboard => !string.IsNullOrEmpty(Clipboard);
    }
}
