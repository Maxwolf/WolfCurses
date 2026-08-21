// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

namespace WolfCurses.Apps
{
    /// <summary>
    ///     Implemented by an application that sometimes has something of its own to dismiss with ESC.
    ///     <para>
    ///         <see cref="OfficeWindow" /> claims ESC for every application at once, which is what stops each of them
    ///         writing the same handler. That is right until an application grows something nested, like an open
    ///         menu: pressing ESC then should shut the menu rather than leave the program, and only the application
    ///         knows whether it has anything open.
    ///     </para>
    ///     <para>
    ///         So the window asks first and leaves only when the answer is no. Deliberately an interface in this
    ///         application rather than anything in the library: the library ships no ESC handling at all, on purpose,
    ///         and this is five lines of the pattern it declined to make everyone's business.
    ///     </para>
    /// </summary>
    internal interface IHandlesEscape
    {
        /// <summary>Dismisses whatever is open, if anything is.</summary>
        /// <returns>TRUE when ESC was used up and the application should stay open.</returns>
        bool TryHandleEscape();
    }
}
