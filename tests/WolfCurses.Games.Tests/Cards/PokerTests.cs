using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Games.Cards;
using WolfCurses.Games.Poker;
using Xunit;

namespace WolfCurses.Games.Tests.Cards
{
    /// <summary>
    ///     The hand evaluator, which is the only interesting code in the poker game, and the paytable it feeds.
    /// </summary>
    public class PokerTests
    {
        [Theory]
        [InlineData(PokerCategoryEnum.RoyalFlush, "10s", "Js", "Qs", "Ks", "As")]
        [InlineData(PokerCategoryEnum.StraightFlush, "5h", "6h", "7h", "8h", "9h")]
        [InlineData(PokerCategoryEnum.FourOfAKind, "9s", "9h", "9d", "9c", "2s")]
        [InlineData(PokerCategoryEnum.FullHouse, "3s", "3h", "3d", "8c", "8s")]
        [InlineData(PokerCategoryEnum.Flush, "2c", "5c", "9c", "Jc", "Kc")]
        [InlineData(PokerCategoryEnum.Straight, "4s", "5h", "6d", "7c", "8s")]
        [InlineData(PokerCategoryEnum.ThreeOfAKind, "Qs", "Qh", "Qd", "2c", "7s")]
        [InlineData(PokerCategoryEnum.TwoPair, "As", "Ah", "4d", "4c", "9s")]
        [InlineData(PokerCategoryEnum.Pair, "Js", "Jh", "3d", "6c", "9s")]
        [InlineData(PokerCategoryEnum.HighCard, "2s", "5h", "9d", "Jc", "Ks")]
        public void EveryCategoryIsRecognised(PokerCategoryEnum expected, params string[] cards)
        {
            var (category, _) = PokerHand.Evaluate(Hand(cards));

            Assert.Equal(expected, category);
        }

        [Fact]
        public void TheWheelIsAStraightAndItsHighCardIsTheFive()
        {
            // THE evaluator bug. A-2-3-4-5 is a straight with the ace playing low, and it is the LOWEST straight -
            // so reporting the ace as its high card would make it beat king-high, which is backwards.
            var (category, high) = PokerHand.Evaluate(Hand("As", "2h", "3d", "4c", "5s"));

            Assert.Equal(PokerCategoryEnum.Straight, category);
            Assert.Equal(5, high);
        }

        [Fact]
        public void TheWheelInOneSuitIsAStraightFlushAndNotARoyal()
        {
            // The same trap one level up: an ace-low straight flush is the worst straight flush there is, and
            // calling it a royal would pay it five times over.
            var (category, high) = PokerHand.Evaluate(Hand("Ah", "2h", "3h", "4h", "5h"));

            Assert.Equal(PokerCategoryEnum.StraightFlush, category);
            Assert.Equal(5, high);
        }

        [Fact]
        public void AceHighIsAStraightToo()
        {
            var (category, high) = PokerHand.Evaluate(Hand("10s", "Jh", "Qd", "Kc", "As"));

            Assert.Equal(PokerCategoryEnum.Straight, category);
            Assert.Equal(14, high);
        }

        [Fact]
        public void AlmostAStraightIsNotAStraight()
        {
            // The wraparound that is not one: K-A-2-3-4 is nothing at all, and an evaluator that treats the ace as
            // both ends at once calls it a straight.
            var (category, _) = PokerHand.Evaluate(Hand("Ks", "Ah", "2d", "3c", "4s"));

            Assert.Equal(PokerCategoryEnum.HighCard, category);

            var (gap, _) = PokerHand.Evaluate(Hand("2s", "3h", "4d", "5c", "7s"));
            Assert.Equal(PokerCategoryEnum.HighCard, gap);
        }

        [Fact]
        public void TheCategoryNamesTheRankThatMadeIt()
        {
            var (_, pair) = PokerHand.Evaluate(Hand("7s", "7h", "2d", "5c", "9s"));
            Assert.Equal(7, pair);

            // Two pair names the higher one, which is what a person would say and what breaks a tie.
            var (_, twoPair) = PokerHand.Evaluate(Hand("3s", "3h", "Jd", "Jc", "9s"));
            Assert.Equal(11, twoPair);

            var (_, trips) = PokerHand.Evaluate(Hand("Ks", "Kh", "Kd", "2c", "5s"));
            Assert.Equal(13, trips);

            // A full house names the three, not the pair - they are what beats another full house.
            var (_, boat) = PokerHand.Evaluate(Hand("4s", "4h", "4d", "Ac", "As"));
            Assert.Equal(4, boat);
        }

        [Fact]
        public void TheCategoriesAreOrderedFromWorstToBest()
        {
            // Comparing two hands is comparing two integers, and only because this order is right.
            var order = new[]
            {
                PokerCategoryEnum.HighCard, PokerCategoryEnum.Pair, PokerCategoryEnum.TwoPair,
                PokerCategoryEnum.ThreeOfAKind, PokerCategoryEnum.Straight, PokerCategoryEnum.Flush,
                PokerCategoryEnum.FullHouse, PokerCategoryEnum.FourOfAKind, PokerCategoryEnum.StraightFlush,
                PokerCategoryEnum.RoyalFlush
            };

            for (var i = 1; i < order.Length; i++)
                Assert.True((int) order[i] > (int) order[i - 1], $"{order[i]} does not beat {order[i - 1]}");
        }

        [Fact]
        public void AHandThatIsNotFiveCardsIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => PokerHand.Evaluate(null));
            Assert.Throws<ArgumentException>(() => PokerHand.Evaluate(Hand("As", "Kh")));
        }

        [Fact]
        public void EveryCategoryHasAName()
        {
            foreach (PokerCategoryEnum category in Enum.GetValues(typeof (PokerCategoryEnum)))
                Assert.False(string.IsNullOrWhiteSpace(PokerHand.Describe(category)), $"{category} has no name");
        }

        // ------------------------------------------------------------ the game

        [Fact]
        public void TheStakeIsTakenWhenTheHandIsDealt()
        {
            var game = new PokerGame(new Randomizer(1));

            Assert.Equal(PokerGame.StartingChips - PokerGame.BetSize, game.Chips);
            Assert.Equal(PokerHand.HandSize, game.Hand.Count);
            Assert.True(game.IsChoosing);
        }

        [Fact]
        public void HeldCardsSurviveTheDrawAndTheRestDoNot()
        {
            var game = new PokerGame(new Randomizer(2));
            var kept = game.Hand[0];
            var replaced = game.Hand[4];

            game.ToggleHold(0);
            Assert.True(game.IsHeld(0));
            Assert.False(game.IsHeld(4));

            game.Draw();

            Assert.Equal(kept, game.Hand[0]);
            Assert.NotEqual(replaced, game.Hand[4]);
            Assert.False(game.IsChoosing);
        }

        [Fact]
        public void HoldsAreIgnoredOnceTheHandIsFinished()
        {
            var game = new PokerGame(new Randomizer(3));
            game.Draw();

            game.ToggleHold(0);

            Assert.False(game.IsHeld(0));
        }

        [Fact]
        public void OnlyJacksOrBetterPays()
        {
            // The rule the game is named after, and the only place the paytable needs a rank rather than a category.
            var low = Score("7s", "7h", "2d", "5c", "9s");
            Assert.Equal(PokerCategoryEnum.Pair, low.Category);
            Assert.Equal(0, low.LastPayout);

            var jacks = Score("Js", "Jh", "2d", "5c", "9s");
            Assert.Equal(PokerCategoryEnum.Pair, jacks.Category);
            Assert.Equal(PokerGame.BetSize, jacks.LastPayout);
        }

        [Fact]
        public void APairOfAcesPaysDespiteAnAceBeingRankOne()
        {
            // The trap in the paytable rather than in the evaluator: the obvious "rank greater than ten" test pays
            // nothing at all for the best pair in the game.
            var game = Score("As", "Ah", "2d", "5c", "9s");

            Assert.Equal(PokerCategoryEnum.Pair, game.Category);
            Assert.Equal(PokerGame.BetSize, game.LastPayout);
        }

        [Theory]
        [InlineData(250, "10s", "Js", "Qs", "Ks", "As")]
        [InlineData(50, "5h", "6h", "7h", "8h", "9h")]
        [InlineData(25, "9s", "9h", "9d", "9c", "2s")]
        [InlineData(9, "3s", "3h", "3d", "8c", "8s")]
        [InlineData(6, "2c", "5c", "9c", "Jc", "Kc")]
        [InlineData(4, "4s", "5h", "6d", "7c", "8s")]
        [InlineData(3, "Qs", "Qh", "Qd", "2c", "7s")]
        [InlineData(2, "As", "Ah", "4d", "4c", "9s")]
        [InlineData(0, "2s", "5h", "9d", "Jc", "Ks")]
        public void ThePaytablePaysWhatItSays(int multiple, params string[] cards)
        {
            var game = Score(cards);

            Assert.Equal(multiple*PokerGame.BetSize, game.LastPayout);
        }

        [Fact]
        public void HoldingEverythingKeepsTheHandExactlyAsDealt()
        {
            var game = Stacked("As", "Ks", "Qs", "Js", "10s");
            for (var i = 0; i < PokerHand.HandSize; i++)
                game.ToggleHold(i);

            game.Draw();

            Assert.Equal(PokerCategoryEnum.RoyalFlush, game.Category);
        }

        [Fact]
        public void ChipsNeverGoNegativeAndTheTableCloses()
        {
            var game = new PokerGame(new Randomizer(6));

            for (var hand = 0; hand < 3000 && !game.IsBroke; hand++)
            {
                // Drawing five new cards every time is close to the worst strategy there is, which is the quickest
                // route to the thing being tested.
                game.Draw();
                Assert.True(game.Chips >= 0, "the player owes the house money");
                game.Deal();
            }

            Assert.True(game.IsBroke, $"still holding {game.Chips} chips after three thousand hands");
        }

        [Fact]
        public void ANullRandomSourceIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new PokerGame(null));
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        ///     Deals an exact hand, keeps every card, and draws — so the hand that gets scored is the hand that was
        ///     set up.
        ///     <para>
        ///         Holding is not optional here, and forgetting it does not fail loudly: a draw with nothing held
        ///         replaces all five cards, so the scored hand is whatever the shuffle produced next. Two of the
        ///         tests above passed that way by luck before this helper existed, which is the worst outcome
        ///         available — a green test measuring nothing.
        ///     </para>
        /// </summary>
        private static PokerGame Score(params string[] cards)
        {
            var game = Stacked(cards);

            for (var i = 0; i < PokerHand.HandSize; i++)
                game.ToggleHold(i);

            game.Draw();
            return game;
        }

        /// <summary>A game whose dealt hand is exactly the cards named.</summary>
        private static PokerGame Stacked(params string[] cards)
        {
            var game = new PokerGame(new Randomizer(1));
            game.StackDeck(Hand(cards).ToArray());
            game.Deal();
            return game;
        }

        /// <summary>Reads "As", "10h", "Qd" into cards, so a hand is one readable line.</summary>
        private static List<Card> Hand(params string[] cards)
        {
            var hand = new List<Card>(cards.Length);

            foreach (var text in cards)
            {
                var suit = text[^1] switch
                {
                    's' => CardSuitEnum.Spades,
                    'h' => CardSuitEnum.Hearts,
                    'd' => CardSuitEnum.Diamonds,
                    _ => CardSuitEnum.Clubs
                };

                var rankText = text[..^1];
                var rank = rankText switch
                {
                    "A" => CardRankEnum.Ace,
                    "J" => CardRankEnum.Jack,
                    "Q" => CardRankEnum.Queen,
                    "K" => CardRankEnum.King,
                    _ => (CardRankEnum) int.Parse(rankText, System.Globalization.CultureInfo.InvariantCulture)
                };

                hand.Add(new Card(rank, suit));
            }

            return hand;
        }
    }
}
