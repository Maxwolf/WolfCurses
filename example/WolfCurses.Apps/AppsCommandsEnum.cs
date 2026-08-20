// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using WolfCurses.Utility;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     The suite menu. Each value becomes a numbered choice on <see cref="AppsWindow" />, and the
    ///     <see cref="DescriptionAttribute" /> is the line the user reads.
    ///     <para>
    ///         Only Quit so far. Applications are added <b>above</b> it and Quit is renumbered to stay last, because
    ///         the number printed beside a choice is the enum member's own value: appending after Quit would render
    ///         the menu as 1 2 3 4 5 7 6. Renumbering is allowed here and not in the library, since nothing persists
    ///         these values and the example apps are exempt from the library's enum contract tests.
    ///     </para>
    /// </summary>
    public enum AppsCommandsEnum
    {
        /// <summary>Closes the suite and returns to the operating system.</summary>
        [Description("Quit.")] Quit = 1
    }
}
