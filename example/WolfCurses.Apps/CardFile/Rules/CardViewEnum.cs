// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     Which way the card file is being looked at. Two ways rather than the planner's four, because a card file
    ///     only has two questions in it: what is on this card, and which cards are there.
    /// </summary>
    public enum CardViewEnum
    {
        /// <summary>One card, laid out as a card, with room for a note.</summary>
        Card = 0,

        /// <summary>All of them at once as a table, a chosen few fields wide.</summary>
        List = 1
    }
}
