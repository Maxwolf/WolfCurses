// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     Draws a hand as characters, for a terminal that cannot show a picture worth looking at.
    ///     <para>
    ///         <b>This is not a consolation prize, and for card games it is arguably the better view.</b> Chess
    ///         learned that a 48-pixel knight resampled into three rows is a smudge; a playing card is worse, because
    ///         what you need off it is a rank and a suit in the corner, which is the first thing to disappear when it
    ///         is scaled. <c>A♠</c> is the whole card in two characters and survives any terminal.
    ///     </para>
    ///     <para>
    ///         Cards are boxed rather than listed so a hand reads as a row of cards at a glance, and the suit is
    ///         drawn in its own colour — but <b>the rank and the pip are both always there</b>, so a mono terminal
    ///         loses nothing but the red. Same rule as everywhere else here.
    ///     </para>
    /// </summary>
    public static class CardTableText
    {
        private static readonly TextStyle _redStyle = new(ConsoleColor.Red, bold: true);
        private static readonly TextStyle _blackStyle = new(ConsoleColor.White, bold: true);
        private static readonly TextStyle _backStyle = new(ConsoleColor.DarkBlue);
        private static readonly TextStyle _frameStyle = new(ConsoleColor.DarkGray);

        /// <summary>How wide one drawn card is, in characters.</summary>
        public const int CardColumns = 5;

        /// <summary>How tall one drawn card is, in rows.</summary>
        public const int CardRows = 3;

        /// <summary>
        ///     How many columns sit inside a card's frame: <see cref="CardColumns" /> less its two edges. Stated
        ///     once so the width the index is fitted to and the width the frame is drawn at cannot drift apart.
        /// </summary>
        private const int FaceColumns = CardColumns - 2;

        /// <summary>
        ///     Draws a row of cards as three lines of text: a top edge, the index, and a bottom edge.
        /// </summary>
        /// <param name="cards">The hand, left to right. Face-down cards are drawn as a hatched back.</param>
        /// <returns>Three lines, joined with the platform newline and none trailing.</returns>
        public static string Render(IReadOnlyList<TableCard> cards)
        {
            if (cards == null || cards.Count == 0)
                return string.Empty;

            var top = new StringBuilder();
            var middle = new StringBuilder();
            var bottom = new StringBuilder();

            foreach (var entry in cards)
            {
                if (top.Length > 0)
                {
                    top.Append(' ');
                    middle.Append(' ');
                    bottom.Append(' ');
                }

                var frameOpen = _frameStyle.Apply("┌───┐");
                top.Append(frameOpen);
                bottom.Append(_frameStyle.Apply("└───┘"));

                if (!entry.FaceUp)
                {
                    middle.Append(_frameStyle.Apply("│")).Append(_backStyle.Apply("▒▒▒")).Append(_frameStyle.Apply("│"));
                    continue;
                }

                // Fitted to the face width so a ten does not push the frame out and shear the row below it. This was
                // a measure-then-pad pair, and the Math.Max(0, ...) that clamped the pad permitted exactly the
                // failure the padding is here to prevent: a label wider than the face computed a negative pad,
                // emitted no spaces at all, and drew a card a column too wide. Fit trims instead, so the interior is
                // FaceColumns wide whatever arrives. Today Card.Label tops out at "10♠", so that was latent rather
                // than live - it goes live the moment anything grows a second pip character or a joker turns up.
                //
                // Fit and not PadRight because the string being measured is already styled: two visible characters
                // arrive wrapped in a few dozen bytes of SGR, and PadRight counts those bytes as columns and so pads
                // by nothing at all. Fitting after styling rather than before is deliberate here - the padding then
                // sits outside the card's own run, which costs nothing while the rank styles carry no background,
                // and keeps the emitted bytes identical to what this drew before.
                var style = entry.Card.IsRed ? _redStyle : _blackStyle;

                middle.Append(_frameStyle.Apply("│"))
                    .Append(AnsiText.Fit(style.Apply(entry.Card.Label), FaceColumns))
                    .Append(_frameStyle.Apply("│"));
            }

            var sb = new StringBuilder();
            sb.AppendLine(top.ToString());
            sb.AppendLine(middle.ToString());
            sb.Append(bottom.ToString());
            return sb.ToString();
        }
    }
}
