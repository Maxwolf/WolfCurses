using System;
using WolfCurses.Core;
using WolfCurses.Games.Blackjack;
using WolfCurses.Games.Cards;
using Xunit;

namespace WolfCurses.Games.Tests.Cards
{
    /// <summary>
    ///     Blackjack's rules, which are almost entirely about the ace and the dealer's fixed strategy.
    /// </summary>
    public class BlackjackTests
    {
        [Theory]
        [InlineData(21, CardRankEnum.Ace, CardRankEnum.King)] // ace high
        [InlineData(13, CardRankEnum.Ace, CardRankEnum.Two)] // ace high, room to spare
        [InlineData(12, CardRankEnum.Ace, CardRankEnum.Ace)] // eleven plus one - NOT twenty-two
        [InlineData(13, CardRankEnum.Ace, CardRankEnum.Ace, CardRankEnum.Ace)] // still only one of them is high
        [InlineData(21, CardRankEnum.Ace, CardRankEnum.Nine, CardRankEnum.Ace)] // 11 + 9 + 1
        [InlineData(20, CardRankEnum.King, CardRankEnum.Queen)] // pictures are ten each
        [InlineData(16, CardRankEnum.Ace, CardRankEnum.Five, CardRankEnum.King)] // ace demoted by the king
        [InlineData(22, CardRankEnum.King, CardRankEnum.Queen, CardRankEnum.Two)] // bust
        public void AHandIsWorthWhatTheAceRuleSays(int expected, params CardRankEnum[] ranks)
        {
            // The whole difficulty of blackjack. "Each ace picks its own value" scores two aces as twenty-two before
            // anything else is dealt; the rule is "count them all low, then add ten once if it fits".
            var hand = new BlackjackHand();
            foreach (var rank in ranks)
                hand.Add(new Card(rank, CardSuitEnum.Spades));

            Assert.Equal(expected, hand.Value);
        }

        [Fact]
        public void ASoftHandIsOneWhoseAceIsStillCountingHigh()
        {
            var soft = new BlackjackHand();
            soft.Add(new Card(CardRankEnum.Ace, CardSuitEnum.Spades));
            soft.Add(new Card(CardRankEnum.Six, CardSuitEnum.Hearts));
            Assert.True(soft.IsSoft);
            Assert.Equal(17, soft.Value);

            // The same cards plus a ten: the ace has to come down, so the hand is no longer soft.
            soft.Add(new Card(CardRankEnum.Ten, CardSuitEnum.Clubs));
            Assert.False(soft.IsSoft);
            Assert.Equal(17, soft.Value);
        }

        [Fact]
        public void OnlyTwentyOneOnTwoCardsIsABlackjack()
        {
            // A three-card twenty-one pays even money and loses to a natural, so the distinction is worth real
            // chips and is not merely cosmetic.
            var natural = new BlackjackHand();
            natural.Add(new Card(CardRankEnum.Ace, CardSuitEnum.Spades));
            natural.Add(new Card(CardRankEnum.Jack, CardSuitEnum.Hearts));
            Assert.True(natural.IsBlackjack);

            var made = new BlackjackHand();
            made.Add(new Card(CardRankEnum.Seven, CardSuitEnum.Spades));
            made.Add(new Card(CardRankEnum.Seven, CardSuitEnum.Hearts));
            made.Add(new Card(CardRankEnum.Seven, CardSuitEnum.Clubs));
            Assert.Equal(21, made.Value);
            Assert.False(made.IsBlackjack);
        }

        [Fact]
        public void TheHoleCardStaysHiddenUntilThePlayerHasFinished()
        {
            // The single most important thing on the screen. Turning it up early gives away the one piece of
            // information the whole game is played against.
            var game = Stacked(
                new Card(CardRankEnum.Nine, CardSuitEnum.Spades), // player
                new Card(CardRankEnum.Six, CardSuitEnum.Hearts), // dealer up
                new Card(CardRankEnum.Seven, CardSuitEnum.Clubs), // player
                new Card(CardRankEnum.King, CardSuitEnum.Diamonds)); // dealer hole

            var table = game.DealerTable();
            Assert.True(table[0].FaceUp);
            Assert.False(table[1].FaceUp);
            Assert.Equal(6, game.DealerShowing);

            game.Stand();

            Assert.True(game.DealerTable()[1].FaceUp);
            Assert.Equal(16 + game.Dealer.Value - 16, game.DealerShowing);
        }

        [Fact]
        public void TheDealerDrawsToSixteenAndStandsOnSeventeen()
        {
            var game = Stacked(
                new Card(CardRankEnum.Ten, CardSuitEnum.Spades),
                new Card(CardRankEnum.Six, CardSuitEnum.Hearts),
                new Card(CardRankEnum.Nine, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Ten, CardSuitEnum.Diamonds));

            game.Stand();

            Assert.True(game.Dealer.Value >= BlackjackGame.DealerStandsOn || game.Dealer.IsBust,
                $"the dealer stopped on {game.Dealer.Value}");
        }

        [Fact]
        public void TheDealerStandsOnASoftSeventeenToo()
        {
            // Which way a house plays soft seventeen is the one variation worth knowing about, and standing is the
            // player-friendlier of the two - so it is worth pinning rather than leaving to be discovered.
            var game = Stacked(
                new Card(CardRankEnum.Ten, CardSuitEnum.Spades),
                new Card(CardRankEnum.Ace, CardSuitEnum.Hearts),
                new Card(CardRankEnum.Nine, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Six, CardSuitEnum.Diamonds));

            game.Stand();

            Assert.Equal(17, game.Dealer.Value);
            Assert.True(game.Dealer.IsSoft);
            Assert.Equal(2, game.Dealer.Count);
        }

        [Fact]
        public void ANaturalPaysThreeToTwo()
        {
            var game = Stacked(
                new Card(CardRankEnum.Ace, CardSuitEnum.Spades),
                new Card(CardRankEnum.Six, CardSuitEnum.Hearts),
                new Card(CardRankEnum.King, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Nine, CardSuitEnum.Diamonds));

            Assert.True(game.Player.IsBlackjack);
            Assert.Equal(BlackjackStateEnum.RoundOver, game.State);
            Assert.Equal(BlackjackGame.BetSize*3/2, game.LastPayout);
            Assert.Equal(BlackjackGame.StartingChips + BlackjackGame.BetSize*3/2, game.Chips);
        }

        [Fact]
        public void ANaturalEndsTheRoundBeforeThePlayerGetsATurn()
        {
            var game = Stacked(
                new Card(CardRankEnum.Ace, CardSuitEnum.Spades),
                new Card(CardRankEnum.Six, CardSuitEnum.Hearts),
                new Card(CardRankEnum.King, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Nine, CardSuitEnum.Diamonds));

            Assert.False(game.CanAct);

            var chips = game.Chips;
            game.Hit();
            Assert.Equal(chips, game.Chips);
            Assert.Equal(2, game.Player.Count);
        }

        [Fact]
        public void BothNaturalsIsAPush()
        {
            var game = Stacked(
                new Card(CardRankEnum.Ace, CardSuitEnum.Spades),
                new Card(CardRankEnum.Ace, CardSuitEnum.Hearts),
                new Card(CardRankEnum.King, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Queen, CardSuitEnum.Diamonds));

            Assert.Equal(0, game.LastPayout);
            Assert.Equal(BlackjackGame.StartingChips, game.Chips);
        }

        [Fact]
        public void GoingBustLosesEvenWhenTheDealerWouldHaveBustToo()
        {
            // The house edge in one sentence: the player acts first, and a bust is settled the moment it happens.
            var game = Stacked(
                new Card(CardRankEnum.Ten, CardSuitEnum.Spades),
                new Card(CardRankEnum.Six, CardSuitEnum.Hearts),
                new Card(CardRankEnum.Six, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Ten, CardSuitEnum.Diamonds),
                new Card(CardRankEnum.King, CardSuitEnum.Spades));

            game.Hit();

            Assert.True(game.Player.IsBust);
            Assert.Equal(-BlackjackGame.BetSize, game.LastPayout);
            Assert.Equal(BlackjackStateEnum.RoundOver, game.State);
            Assert.Equal(0, game.Dealer.Count - 2);
        }

        [Fact]
        public void HittingToTwentyOneStandsAutomatically()
        {
            var game = Stacked(
                new Card(CardRankEnum.Ten, CardSuitEnum.Spades),
                new Card(CardRankEnum.Ten, CardSuitEnum.Hearts),
                new Card(CardRankEnum.Six, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Nine, CardSuitEnum.Diamonds),
                new Card(CardRankEnum.Five, CardSuitEnum.Spades));

            game.Hit();

            Assert.Equal(21, game.Player.Value);
            Assert.Equal(BlackjackStateEnum.RoundOver, game.State);
        }

        [Fact]
        public void APushReturnsTheStake()
        {
            var game = Stacked(
                new Card(CardRankEnum.Ten, CardSuitEnum.Spades),
                new Card(CardRankEnum.Ten, CardSuitEnum.Hearts),
                new Card(CardRankEnum.Eight, CardSuitEnum.Clubs),
                new Card(CardRankEnum.Eight, CardSuitEnum.Diamonds));

            game.Stand();

            Assert.Equal(18, game.Player.Value);
            Assert.Equal(18, game.Dealer.Value);
            Assert.Equal(0, game.LastPayout);
            Assert.Equal(BlackjackGame.StartingChips, game.Chips);
        }

        [Fact]
        public void ChipsOnlyEverMoveByTheStakeOrTheNaturalBonus()
        {
            // Two hundred rounds of hitting until seventeen. Nothing here checks strategy - what it checks is that
            // the accounting never invents or loses chips, over every combination the shoe throws up.
            var game = new BlackjackGame(new Randomizer(5));
            var chips = game.Chips;

            for (var round = 0; round < 200 && !game.IsBroke; round++)
            {
                while (game.CanAct && game.Player.Value < 17)
                    game.Hit();

                if (game.CanAct)
                    game.Stand();

                var moved = game.Chips - chips;
                Assert.Contains(moved, new[] {-BlackjackGame.BetSize, 0, BlackjackGame.BetSize, BlackjackGame.BetSize*3/2});
                Assert.Equal(moved, game.LastPayout);

                chips = game.Chips;
                Assert.True(chips >= 0, "the player owes the house money");

                game.Deal();
            }
        }

        [Fact]
        public void RunningOutOfChipsStopsTheTable()
        {
            var game = new BlackjackGame(new Randomizer(4));

            for (var round = 0; round < 2000 && !game.IsBroke; round++)
            {
                // Hitting until twenty is a losing strategy on purpose - this test is about the table closing, and
                // the fastest way there is to play badly.
                while (game.CanAct && game.Player.Value < 20)
                    game.Hit();

                if (game.CanAct)
                    game.Stand();

                game.Deal();
            }

            Assert.True(game.IsBroke, $"still holding {game.Chips} chips after two thousand rounds of bad play");
            Assert.True(game.Chips < BlackjackGame.BetSize);
        }

        [Fact]
        public void ANullRandomSourceIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new BlackjackGame(null));
        }

        /// <summary>A game whose next cards are the ones given, so a test can set up an exact round.</summary>
        private static BlackjackGame Stacked(params Card[] cards)
        {
            var game = new BlackjackGame(new Randomizer(1));
            game.StackDeck(cards);
            game.Deal();
            return game;
        }
    }
}
