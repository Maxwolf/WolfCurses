// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     A card on the table, and whether it is face up.
    ///     <para>
    ///         The pair exists so <see cref="CardTableArt" /> and <see cref="CardTableText" /> can draw a hand
    ///         without knowing whose it is: the dealer's hole card is face down in blackjack and a held card is face
    ///         up in poker, and neither renderer has to learn either rule to draw both.
    ///     </para>
    /// </summary>
    public readonly struct TableCard
    {
        /// <summary>Initializes a new instance of the <see cref="TableCard" /> struct.</summary>
        /// <param name="card">Which card it is.</param>
        /// <param name="faceUp">Whether the player may see it.</param>
        public TableCard(Card card, bool faceUp = true)
        {
            Card = card;
            FaceUp = faceUp;
        }

        /// <summary>Which card it is.</summary>
        public Card Card { get; }

        /// <summary>Whether the player may see it.</summary>
        public bool FaceUp { get; }
    }
}
