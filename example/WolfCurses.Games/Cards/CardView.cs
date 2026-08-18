// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     The two questions every card game asks about drawing a table: is a picture worth showing here, and what
    ///     does the picture look like as text.
    ///     <para>
    ///         Shared rather than written twice, which is the whole reason this folder exists — blackjack and poker
    ///         disagree about everything except how a table is drawn.
    ///     </para>
    /// </summary>
    public static class CardView
    {
        /// <summary>
        ///     Whether a picture of the table is worth showing at this terminal size, or whether letters say more.
        ///     <para>
        ///         The threshold is far higher for half blocks than for real pixels, and higher again than chess
        ///         needs. Half blocks get one pixel per column, so a seventy-five pixel card is seventy-five columns
        ///         — a two-card hand does not fit an eighty-column terminal at native size and has to be scaled until
        ///         the corner index, which is the only part of a card anybody reads, is a smear. Sixel and kitty draw
        ///         ten by twenty real pixels per cell and have room for a whole table in the same rows.
        ///     </para>
        /// </summary>
        /// <param name="rows">How many rows the table has been given.</param>
        /// <returns>Whether to draw the picture.</returns>
        public static bool PictureIsLegible(int rows)
        {
            if (AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None)
                return false;

            // Asked of the renderer rather than type-tested against the built-in classes, which gets the wrong
            // answer for exactly the third-party renderers the seam exists to allow.
            return ImageRenderers.Default.DrawsTruePixels ? rows >= 10 : rows >= 44;
        }

        /// <summary>
        ///     Composites the hands into one picture and renders it.
        /// </summary>
        /// <param name="art">The table painter.</param>
        /// <param name="rows">How many rows the picture may claim.</param>
        /// <param name="hands">The hands, top row first.</param>
        /// <returns>The table as one block of ANSI, ready to return from a form's render.</returns>
        public static string Render(CardTableArt art, int rows, params IReadOnlyList<TableCard>[] hands)
        {
            if (art == null)
                throw new ArgumentNullException(nameof(art));

            var options = new AnsiImageOptions
            {
                MaxRows = Math.Max(4, rows),
                MaxColumns = Math.Max(20, AnsiConsole.SafeWindowWidth() - 2),
                Fit = AnsiImageFitEnum.Contain
            };

            return AnsiImage.FromPixels(art.Compose(hands)).ToAnsi(options);
        }
    }
}
