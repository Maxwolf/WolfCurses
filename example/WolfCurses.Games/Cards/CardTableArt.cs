// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     Composites rows of cards into a single picture of a table.
    ///     <para>
    ///         <b>One buffer, not one picture per card</b> — the same rule <see cref="Chess.ChessBoardArt" /> is
    ///         built on, and for the same reason: a true-pixel picture is one escape blob of zero visible width that
    ///         paints across many rows, tracked by the <see cref="AnsiGraphics" /> marker contract, and several of
    ///         those interleaved on one screen is not a thing that contract can express. Composing the whole table
    ///         and rendering it once sidesteps it and costs one resample instead of a dozen.
    ///     </para>
    ///     <para>
    ///         <b>Hands are fanned rather than laid out end to end</b>, which is not decoration: five cards side by
    ///         side is 375 pixels of width, and overlapping them so only the index corner of each shows brings a
    ///         five-card hand down to 195. That is the difference between a hand that fits on a terminal and one
    ///         that gets scaled until the pips disappear. The top card of each hand is always fully visible, because
    ///         that is the one being drawn to.
    ///     </para>
    /// </summary>
    public sealed class CardTableArt
    {
        /// <summary>How much of each card shows when they are fanned — enough for the corner index and its pip.</summary>
        public const int FanStep = 30;

        /// <summary>Blank space around the whole table.</summary>
        private const int Margin = 10;

        /// <summary>Space between one row of cards and the next.</summary>
        private const int RowGap = 14;

        /// <summary>Casino baize, which is the colour everybody pictures and the reason the cards need no border.</summary>
        private static readonly Rgba32 _felt = new(0x0B, 0x5C, 0x36, 0xFF);

        private readonly CardImages _images;

        /// <summary>Initializes a new instance of the <see cref="CardTableArt" /> class.</summary>
        /// <param name="images">The loaded artwork.</param>
        public CardTableArt(CardImages images)
        {
            _images = images ?? throw new ArgumentNullException(nameof(images));
        }

        /// <summary>Whether there is artwork to draw with.</summary>
        public bool IsAvailable => _images.IsAvailable;

        /// <summary>
        ///     Draws rows of cards onto the felt, one row per hand, each fanned left to right.
        /// </summary>
        /// <param name="rows">
        ///     The hands to draw, top row first. A card whose <c>FaceUp</c> is false is drawn as the back, which is
        ///     how the dealer's hole card stays a secret without this class knowing what a dealer is.
        /// </param>
        /// <returns>The finished table as pixels, ready for <see cref="AnsiImage.FromPixels" />.</returns>
        public PixelBuffer Compose(IReadOnlyList<IReadOnlyList<TableCard>> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var widest = 0;
            foreach (var row in rows)
                widest = Math.Max(widest, RowWidth(row?.Count ?? 0));

            var width = Math.Max(CardImages.CardWidth, widest) + 2*Margin;
            var height = Math.Max(1, rows.Count)*CardImages.CardHeight +
                         Math.Max(0, rows.Count - 1)*RowGap + 2*Margin;

            var table = new PixelBuffer(width, height);
            table.Fill(_felt);

            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row == null)
                    continue;

                // Centred, so a two-card hand and a five-card hand share a middle rather than both starting at the
                // left edge and looking like they belong to different games.
                var left = (width - RowWidth(row.Count)) / 2;
                var top = Margin + r*(CardImages.CardHeight + RowGap);

                for (var i = 0; i < row.Count; i++)
                {
                    var pixels = row[i].FaceUp ? _images.Face(row[i].Card) : _images.Back;
                    if (pixels == null)
                        continue;

                    // Left to right, so each card overlaps the one before it - drawing the other way round would put
                    // the corner indices underneath and hide the very thing the fan exists to show.
                    table.DrawImage(pixels, left + i*FanStep, top);
                }
            }

            return table;
        }

        /// <summary>How wide a fanned row of a given size comes out.</summary>
        /// <param name="cards">How many cards are in the row.</param>
        /// <returns>Its width in pixels.</returns>
        public static int RowWidth(int cards)
        {
            return cards <= 0 ? 0 : CardImages.CardWidth + (cards - 1)*FanStep;
        }
    }
}
