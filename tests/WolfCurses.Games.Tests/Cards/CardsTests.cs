using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Games.Cards;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Games.Tests.Cards
{
    /// <summary>
    ///     The deck both card games are built on, and the artwork it names.
    /// </summary>
    public class CardsTests
    {
        [Fact]
        public void ADeckIsFiftyTwoDistinctCards()
        {
            var deck = new Deck(new Randomizer(1));
            var seen = new HashSet<Card>();

            Assert.Equal(52, deck.Count);
            Assert.Equal(52, deck.Remaining);

            for (var i = 0; i < 52; i++)
                Assert.True(seen.Add(deck.Deal()), "the deck dealt the same card twice");

            Assert.Equal(0, deck.Remaining);
        }

        [Fact]
        public void DealingPastTheEndReshufflesRatherThanThrowing()
        {
            // A game that runs out mid-hand has a bug in its own accounting, but the answer to that is not a crash
            // in front of the player.
            var deck = new Deck(new Randomizer(2));
            for (var i = 0; i < 52; i++)
                deck.Deal();

            var thrown = Record.Exception(() => deck.Deal());

            Assert.Null(thrown);
            Assert.Equal(51, deck.Remaining);
        }

        [Fact]
        public void TheSameSeedDealsTheSameDeck()
        {
            var first = new Deck(new Randomizer(9));
            var second = new Deck(new Randomizer(9));

            for (var i = 0; i < 52; i++)
                Assert.Equal(first.Deal(), second.Deal());
        }

        [Theory]
        [InlineData(CardRankEnum.Ace, CardSuitEnum.Spades, "A♠", "spadeAce.png", false)]
        [InlineData(CardRankEnum.Ten, CardSuitEnum.Hearts, "10♥", "heart10.png", true)]
        [InlineData(CardRankEnum.King, CardSuitEnum.Diamonds, "K♦", "diamondKing.png", true)]
        [InlineData(CardRankEnum.Two, CardSuitEnum.Clubs, "2♣", "club2.png", false)]
        [InlineData(CardRankEnum.Queen, CardSuitEnum.Clubs, "Q♣", "clubQueen.png", false)]
        public void ACardKnowsItsNameItsColourAndItsPicture(CardRankEnum rank, CardSuitEnum suit,
            string label, string file, bool red)
        {
            var card = new Card(rank, suit);

            Assert.Equal(label, card.Label);
            Assert.Equal(file, card.ImageFile);
            Assert.Equal(red, card.IsRed);
        }

        [Fact]
        public void OnlyJacksQueensAndKingsArePictureCards()
        {
            // Blackjack scores these as ten, so getting the boundary wrong is worth a point a hand.
            foreach (CardRankEnum rank in Enum.GetValues(typeof (CardRankEnum)))
            {
                var card = new Card(rank, CardSuitEnum.Spades);
                var expected = rank == CardRankEnum.Jack || rank == CardRankEnum.Queen || rank == CardRankEnum.King;

                Assert.Equal(expected, card.IsFace);
            }
        }

        [Fact]
        public void EveryCardInTheDeckHasArtworkOnDisk()
        {
            // The one coupling that matters between the enums and the folder. Fifty-two lookups rather than a
            // spot-check, because a naming rule that is wrong is usually wrong for exactly one rank.
            var images = new CardImages();

            Assert.True(images.IsAvailable, images.Error ?? "the card artwork did not load");
            Assert.NotNull(images.Back);

            foreach (CardSuitEnum suit in Enum.GetValues(typeof (CardSuitEnum)))
            foreach (CardRankEnum rank in Enum.GetValues(typeof (CardRankEnum)))
            {
                var card = new Card(rank, suit);
                var face = images.Face(card);

                Assert.True(face != null, $"no artwork for {card} (expected {card.ImageFile})");
                Assert.Equal(CardImages.CardWidth, face.Width);
                Assert.Equal(CardImages.CardHeight, face.Height);
            }
        }

        [Fact]
        public void MissingArtworkIsReportedRatherThanThrown()
        {
            // The games fall back to letters, which they do perfectly well - so a missing folder must not be fatal.
            var images = new CardImages("no-such-folder");

            Assert.False(images.IsAvailable);
            Assert.NotNull(images.Error);
            Assert.Null(images.Face(new Card(CardRankEnum.Ace, CardSuitEnum.Spades)));
        }

        [Fact]
        public void AFannedHandIsMuchNarrowerThanTheCardsLaidOut()
        {
            // The whole reason hands are fanned: five cards side by side is 375 pixels, which is more width than any
            // terminal has at half blocks. Overlapping brings it down to something that can be shown.
            Assert.Equal(CardImages.CardWidth, CardTableArt.RowWidth(1));
            Assert.Equal(0, CardTableArt.RowWidth(0));

            var laidOut = 5*CardImages.CardWidth;
            Assert.True(CardTableArt.RowWidth(5) < laidOut*0.6,
                $"a fanned five-card hand is {CardTableArt.RowWidth(5)} wide against {laidOut} laid out");
        }

        [Fact]
        public void TheTableIsOnePictureWithEveryCardOnIt()
        {
            // One buffer, not one picture per card - the AnsiGraphics marker contract cannot express several
            // true-pixel payloads interleaved on one screen.
            var images = new CardImages();
            Assert.True(images.IsAvailable, images.Error ?? "the card artwork did not load");

            var art = new CardTableArt(images);
            var table = art.Compose(new[]
            {
                Row(new Card(CardRankEnum.Ace, CardSuitEnum.Spades), new Card(CardRankEnum.King, CardSuitEnum.Hearts)),
                Row(new Card(CardRankEnum.Two, CardSuitEnum.Clubs))
            });

            Assert.True(table.Width >= CardTableArt.RowWidth(2));
            Assert.True(table.Height >= 2*CardImages.CardHeight);

            // Felt everywhere the cards are not - the top-left corner is margin, so it must be table colour rather
            // than the transparent black a bare buffer starts as.
            var corner = table.GetPixel(0, 0);
            Assert.Equal(255, corner.A);
            Assert.True(corner.G > corner.R && corner.G > corner.B, "the table is not green");
        }

        [Fact]
        public void AFaceDownCardIsDrawnAsTheBackAndNotAsItself()
        {
            var images = new CardImages();
            Assert.True(images.IsAvailable, images.Error ?? "the card artwork did not load");

            var art = new CardTableArt(images);
            var card = new Card(CardRankEnum.Ace, CardSuitEnum.Spades);

            var up = art.Compose(new[] {new[] {new TableCard(card)}});
            var down = art.Compose(new[] {new[] {new TableCard(card, false)}});

            Assert.Equal(up.Width, down.Width);
            Assert.NotEqual(Fingerprint(up), Fingerprint(down));
        }

        [Fact]
        public void TheTextViewShowsRankAndPipForEveryCardAndHidesTheFaceDownOnes()
        {
            var hand = new List<TableCard>
            {
                new(new Card(CardRankEnum.Ten, CardSuitEnum.Spades)),
                new(new Card(CardRankEnum.Ace, CardSuitEnum.Hearts)),
                new(new Card(CardRankEnum.Four, CardSuitEnum.Clubs), false)
            };

            var plain = AnsiText.StripEscapes(CardTableText.Render(hand));
            var rows = plain.Split(Environment.NewLine);

            Assert.Equal(CardTableText.CardRows, rows.Length);
            Assert.Contains("10♠", rows[1], StringComparison.Ordinal);
            Assert.Contains("A♥", rows[1], StringComparison.Ordinal);
            Assert.DoesNotContain("4♣", rows[1], StringComparison.Ordinal);

            // Every row the same width, or the boxes shear - which is what a ten does to a layout that assumed every
            // rank is one character.
            Assert.Equal(rows[0].Length, rows[1].Length);
            Assert.Equal(rows[0].Length, rows[2].Length);
        }

        [Fact]
        public void AnEmptyHandDrawsNothingAtAll()
        {
            Assert.Equal(string.Empty, CardTableText.Render(null));
            Assert.Equal(string.Empty, CardTableText.Render(new List<TableCard>()));
        }

        private static TableCard[] Row(params Card[] cards)
        {
            var row = new TableCard[cards.Length];
            for (var i = 0; i < cards.Length; i++)
                row[i] = new TableCard(cards[i]);

            return row;
        }

        /// <summary>A cheap summary of a picture, for asserting two of them differ.</summary>
        private static long Fingerprint(PixelBuffer pixels)
        {
            long sum = 0;
            for (var y = 0; y < pixels.Height; y += 3)
            for (var x = 0; x < pixels.Width; x += 3)
            {
                var pixel = pixels.GetPixel(x, y);
                sum += pixel.R + 3*pixel.G + 7*pixel.B;
            }

            return sum;
        }
    }
}
