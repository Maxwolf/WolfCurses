// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Controls
{
    /// <summary>
    ///     The <see cref="SelectListWindow" /> carries no numbered menu of its own — its form does all rendering and
    ///     input — so this enum exists only to satisfy the <see cref="WolfCurses.Window.Window{TCommands,TData}" />
    ///     generic constraint.
    /// </summary>
    public enum SelectListCommandsEnum
    {
        /// <summary>Unused placeholder.</summary>
        None = 0
    }
}
