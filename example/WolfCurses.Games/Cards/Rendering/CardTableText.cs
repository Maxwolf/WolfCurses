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

                // Padded to three so a ten does not push the frame out and shear the row below it.
                var label = entry.Card.Label;
                var pad = 3 - AnsiText.VisibleLength(label);
                var style = entry.Card.IsRed ? _redStyle : _blackStyle;

                middle.Append(_frameStyle.Apply("│"))
                    .Append(style.Apply(label))
                    .Append(new string(' ', Math.Max(0, pad)))
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
