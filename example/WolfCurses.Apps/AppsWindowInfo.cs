// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using WolfCurses.Window;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     State shared between the menu and the applications attached to it. A window's data object outlives its
    ///     forms, which is what makes it the right home for anything one application hands to another: a suite
    ///     clipboard is the obvious candidate, since a copy taken in a spreadsheet has to survive that spreadsheet
    ///     being closed before it can be pasted into an editor.
    ///     <para>
    ///         Empty until there is something to share. It exists now because
    ///         <see cref="WolfCurses.Window.Window{TCommands,TData}" /> takes a <see cref="WindowData" /> as its
    ///         second type argument, so the menu cannot be declared without one.
    ///     </para>
    /// </summary>
    public sealed class AppsWindowInfo : WindowData
    {
    }
}
