// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     One playing card: a rank and a suit, and the two ways of writing it down.
    ///     <para>
    ///         <b>This knows the name of its own artwork file, and that is the one piece of coupling worth
    ///         having.</b> The alternative is a lookup table somewhere else that has to be kept in step with both
    ///         this enum pair and the folder on disk, and which fails at run time and in only one of the fifty-two
    ///         cases when it drifts. Deriving the name means a card that exists has a file name, and
    ///         <c>CardImagesTests</c> can walk all fifty-two and check every one of them is really there.
    ///     </para>
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        /// <summary>Initializes a new instance of the <see cref="Card" /> struct.</summary>
        /// <param name="rank">Its rank.</param>
        /// <param name="suit">Its suit.</param>
        public Card(CardRankEnum rank, CardSuitEnum suit)
        {
            Rank = rank;
            Suit = suit;
        }

        /// <summary>Its rank.</summary>
        public CardRankEnum Rank { get; }

        /// <summary>Its suit.</summary>
        public CardSuitEnum Suit { get; }

        /// <summary>Whether this card is drawn in red, which is the only thing the suit changes about how it looks.</summary>
        public bool IsRed => Suit == CardSuitEnum.Diamonds || Suit == CardSuitEnum.Hearts;

        /// <summary>Whether this is a jack, queen or king — a "picture card", which blackjack scores as ten.</summary>
        public bool IsFace => Rank >= CardRankEnum.Jack;

        /// <summary>The short way of writing it: rank letter or number, then the suit symbol. <c>A♠</c>, <c>10♦</c>.</summary>
        public string Label => RankLabel + SuitSymbol;

        /// <summary>The rank on its own, as it appears in the corner of a card.</summary>
        public string RankLabel
        {
            get
            {
                return Rank switch
                {
                    CardRankEnum.Ace => "A",
                    CardRankEnum.Jack => "J",
                    CardRankEnum.Queen => "Q",
                    CardRankEnum.King => "K",
                    _ => ((int) Rank).ToString(CultureInfo.InvariantCulture)
                };
            }
        }

        /// <summary>The suit as its pip character.</summary>
        public string SuitSymbol
        {
            get
            {
                return Suit switch
                {
                    CardSuitEnum.Clubs => "♣",
                    CardSuitEnum.Diamonds => "♦",
                    CardSuitEnum.Hearts => "♥",
                    _ => "♠"
                };
            }
        }

        /// <summary>
        ///     The name of this card's artwork file, without the folder — <c>spadeAce.png</c>, <c>heart10.png</c>.
        ///     The naming is the SVGCards deck's own, so this is a fact about that folder and not a choice.
        /// </summary>
        public string ImageFile
        {
            get
            {
                var suit = Suit switch
                {
                    CardSuitEnum.Clubs => "club",
                    CardSuitEnum.Diamonds => "diamond",
                    CardSuitEnum.Hearts => "heart",
                    _ => "spade"
                };

                var rank = Rank switch
                {
                    CardRankEnum.Ace => "Ace",
                    CardRankEnum.Jack => "Jack",
                    CardRankEnum.Queen => "Queen",
                    CardRankEnum.King => "King",
                    _ => ((int) Rank).ToString(CultureInfo.InvariantCulture)
                };

                return suit + rank + ".png";
            }
        }

        /// <summary>Whether two cards are the same rank and suit.</summary>
        public static bool operator ==(Card left, Card right)
        {
            return left.Equals(right);
        }

        /// <summary>Whether two cards differ.</summary>
        public static bool operator !=(Card left, Card right)
        {
            return !left.Equals(right);
        }

        /// <summary>Whether this is the same card as another.</summary>
        /// <param name="other">The card to compare against.</param>
        public bool Equals(Card other)
        {
            return Rank == other.Rank && Suit == other.Suit;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Card other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Rank, Suit);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Label;
        }
    }
}
