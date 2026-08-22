// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     Where an entry in the planner came from, which decides whether it can be removed.
    /// </summary>
    public enum PlannerEventKindEnum
    {
        /// <summary>Somebody typed it in. It lives in the file and can be deleted.</summary>
        Personal = 0,

        /// <summary>
        ///     The program worked it out from the year. There is nothing to delete, because it was never stored:
        ///     next year's Easter is computed the same way and would come back.
        /// </summary>
        Holiday = 1
    }
}
