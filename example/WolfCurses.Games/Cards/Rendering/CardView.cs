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
        ///     How tall the picture has to come out, in pixels, before a corner index survives being drawn in half
        ///     blocks. Eighty-eight is forty-four rows of the two pixels that renderer stacks in a cell, which is
        ///     what this used to be written as directly.
        /// </summary>
        private const int BlockPixelFloor = 88;

        /// <summary>
        ///     The same floor for a renderer painting real pixels. Two hundred is ten rows of a default ten by
        ///     twenty sixel cell, again the number this used to be written as. It is more than twice the half-block
        ///     floor because the two are not the same unit: see <see cref="HasPixelsForACornerIndex" />.
        /// </summary>
        private const int TruePixelFloor = 200;

        /// <summary>
        ///     Whether a picture of the table is worth showing at this terminal size, or whether letters say more.
        ///     <para>
        ///         The console questions only. Colour, and virtual-terminal processing: without the latter the
        ///         presenter blanks the payload row and the table is an empty rectangle. Both are the library's own
        ///         facts, so it is the library that answers. Everything else is arithmetic and lives in
        ///         <see cref="HasPixelsForACornerIndex" />, which is what makes that half testable on a machine
        ///         with no console.
        ///     </para>
        /// </summary>
        /// <param name="rows">How many rows the table has been given.</param>
        /// <returns>Whether to draw the picture.</returns>
        public static bool PictureIsLegible(int rows)
        {
            if (!AnsiConsole.SupportsPictures())
                return false;

            // Asked of the renderer rather than type-tested against the built-in classes, which gets the wrong
            // answer for exactly the third-party renderers the seam exists to allow.
            return HasPixelsForACornerIndex(rows, ImageRenderers.Default);
        }

        /// <summary>
        ///     Whether a table drawn into this many rows by this renderer has the pixels for a corner index.
        ///     <para>
        ///         <b>A row is not a pixel, and how many pixels a row is worth is the renderer's own number.</b>
        ///         What you read off a card is the rank and pip in its corner: measured off the artwork, the rank
        ///         glyph is 11 by 14 pixels of a 75 by 113 card and the pip under it is 15 square. Whether that
        ///         arrives as a letter or as a stain depends on how far the fit scaled the card, and the only
        ///         thing standing between a row count and that scale is the renderer's cell size. This used to
        ///         compare the row count itself against 44 and 10, which is the same test with the two multipliers
        ///         written into the constants instead of asked for.
        ///     </para>
        ///     <para>
        ///         <b>That mattered because the multiplier is a constructor knob.</b> Sixel and kitty default to a
        ///         ten by twenty cell and both take it as an argument, so a host installing
        ///         <c>new SixelImageRenderer(6, 13)</c> hands the table a third less picture per row while
        ///         "10 rows" goes on claiming it is enough, and the table quietly becomes a smear. That is the
        ///         finding Battlezone and Missile Command each wrote down about sizing a canvas, arriving a third
        ///         time from the other end: there the assumption made every stroke too fat, here it makes the
        ///         cards too small. Both floors are the old thresholds multiplied out, so every renderer in use
        ///         today answers exactly what it answered before; what changes is the terminals that were being
        ///         got wrong.
        ///     </para>
        ///     <para>
        ///         <b>Two floors rather than one, because the pixels are not the same size.</b> A half-block pixel
        ///         is a whole character cell painted one colour and a sixel pixel is about one pixel of screen, and
        ///         nothing on <see cref="IImageRenderer" /> can say by how much those differ. So
        ///         <see cref="IImageRenderer.DrawsTruePixels" /> keeps its job of picking the measuring stick,
        ///         which is what it is for. Against a one-row table, which is a card plus the felt margin either
        ///         side and so 133 pixels tall, 88 pixels puts the rank glyph on 9 of them and 200 puts it on 21.
        ///     </para>
        ///     <para>
        ///         <b>Height and not width, and that is a real limitation rather than an oversight.</b> A contain
        ///         fit takes the smaller of the two scales, and on a tall narrow terminal it is the columns that
        ///         bind. The columns are not consulted because there is no nominal shape to test them against: a
        ///         poker table is one fanned row and comes out wider than it is tall, a blackjack table is two rows
        ///         and comes out taller than it is wide, and they grow along different axes as cards are dealt.
        ///         Handing this the hand would answer the two tables differently from the one place that exists so
        ///         they cannot be answered differently. Note the same asymmetry inside the floors: blackjack's two
        ///         rows are twice as tall for the same budget, so it gets half the index poker does.
        ///     </para>
        /// </summary>
        /// <param name="rows">How many rows the table has been given.</param>
        /// <param name="renderer">The renderer that will draw it, asked for its cell size.</param>
        /// <returns>Whether the picture has the pixels to be read.</returns>
        internal static bool HasPixelsForACornerIndex(int rows, IImageRenderer renderer)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));

            // Clamped rather than trusted, and the clamp is on the renderer's answer rather than on the product:
            // a cell claiming no pixels at all collapses the multiplication to zero, which refuses the picture on
            // every terminal however many rows it was given - so a renderer drawing perfectly good pictures would
            // never get to draw one. The built-in true-pixel renderers refuse such a cell size in their own
            // constructors, so this can only ever arrive from the third parties the seam exists to allow.
            var pixels = Math.Max(0, rows)*Math.Max(1, renderer.CellPixelHeight);

            return pixels >= (renderer.DrawsTruePixels ? TruePixelFloor : BlockPixelFloor);
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
