// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Games.Cards;

namespace WolfCurses.Games.Blackjack
{
    /// <summary>
    ///     A round of blackjack against a dealer who has no choices to make.
    ///     <para>
    ///         <b>The dealer is the interesting part, precisely because it does not think.</b> It draws to sixteen
    ///         and stands on all seventeens, and that is the whole of it — no reading the player, no adapting, no
    ///         search. Put beside <see cref="PacMan.Ghost" /> (four one-line rules) and
    ///         <see cref="Chess.Bot.WolfChessBot" /> (a real sliced search), this is the third point on that line:
    ///         an opponent with a published, fixed strategy that the player is expected to know. The house edge is a
    ///         property of the rules, not of the dealer being clever.
    ///     </para>
    ///     <para>
    ///         Everything here is turn-based and has no clock at all — the state machine moves only when the player
    ///         presses something, which makes the whole game driveable a hundred rounds deep in a test with nothing
    ///         sleeping.
    ///     </para>
    /// </summary>
    public sealed class BlackjackGame
    {
        /// <summary>What the player starts with, and what they are trying not to lose.</summary>
        public const int StartingChips = 500;

        /// <summary>The bet, fixed so the game is about the cards rather than about bet sizing.</summary>
        public const int BetSize = 25;

        /// <summary>The dealer draws until reaching this, and stands on it. The published rule of the house.</summary>
        public const int DealerStandsOn = 17;

        private readonly Deck _deck;

        /// <summary>Initializes a new instance of the <see cref="BlackjackGame" /> class and deals the first round.</summary>
        /// <param name="random">The simulation's shared random source.</param>
        public BlackjackGame(Randomizer random)
        {
            _deck = new Deck(random ?? throw new ArgumentNullException(nameof(random)));
            Chips = StartingChips;
            Deal();
        }

        /// <summary>The player's hand.</summary>
        public BlackjackHand Player { get; } = new();

        /// <summary>The dealer's hand.</summary>
        public BlackjackHand Dealer { get; } = new();

        /// <summary>What the player has left.</summary>
        public int Chips { get; private set; }

        /// <summary>The most the player has had at once, which is the score worth keeping.</summary>
        public int BestChips { get; private set; } = StartingChips;

        /// <summary>How many rounds have been played out.</summary>
        public int RoundsPlayed { get; private set; }

        /// <summary>Where the round is up to.</summary>
        public BlackjackStateEnum State { get; private set; }

        /// <summary>What just happened, in words, for the screen to show.</summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>What the last finished round paid, negative when it cost.</summary>
        public int LastPayout { get; private set; }

        /// <summary>True once the player has no chips left and no round in progress.</summary>
        public bool IsBroke => Chips < BetSize && State == BlackjackStateEnum.RoundOver;

        /// <summary>Whether the player may still act on this hand.</summary>
        public bool CanAct => State == BlackjackStateEnum.PlayerTurn;

        /// <summary>
        ///     The dealer's cards as they should be shown: the hole card stays face down until the dealer plays.
        /// </summary>
        /// <returns>What a renderer needs.</returns>
        public IReadOnlyList<TableCard> DealerTable()
        {
            var table = new List<TableCard>(Dealer.Count);

            for (var i = 0; i < Dealer.Count; i++)
            {
                // The second card, and only while the player is still deciding. Turning it up any earlier gives away
                // the one piece of information the whole game is played against.
                var hidden = i == 1 && State == BlackjackStateEnum.PlayerTurn;
                table.Add(new TableCard(Dealer.Cards[i], !hidden));
            }

            return table;
        }

        /// <summary>What the dealer's hand is worth as far as the player can see it.</summary>
        public int DealerShowing
        {
            get
            {
                if (State != BlackjackStateEnum.PlayerTurn || Dealer.Count == 0)
                    return Dealer.Value;

                var card = Dealer.Cards[0];
                return card.Rank == CardRankEnum.Ace ? 11 : card.IsFace ? 10 : (int) card.Rank;
            }
        }

        /// <summary>
        ///     Deals a fresh round: two cards each, and settles immediately if either side has a natural.
        /// </summary>
        public void Deal()
        {
            if (Chips < BetSize)
            {
                State = BlackjackStateEnum.RoundOver;
                Message = "Out of chips. ENTER for a fresh stake, ESC to leave the table.";
                return;
            }

            Player.Clear();
            Dealer.Clear();

            Player.Add(_deck.Deal());
            Dealer.Add(_deck.Deal());
            Player.Add(_deck.Deal());
            Dealer.Add(_deck.Deal());

            State = BlackjackStateEnum.PlayerTurn;
            LastPayout = 0;
            Message = "Hit or stand?";

            // A natural on either side ends the round before anybody acts, which is why this is here and not in
            // Stand: the player never gets a turn on a hand that is already decided.
            if (Player.IsBlackjack || Dealer.IsBlackjack)
                Settle();
        }

        /// <summary>Takes another card, and busts out if it goes over.</summary>
        public void Hit()
        {
            if (!CanAct)
                return;

            Player.Add(_deck.Deal());

            if (Player.IsBust)
                Settle();
            else if (Player.Value == 21)
                Stand();
            else
                Message = "Hit or stand?";
        }

        /// <summary>Stops drawing and lets the dealer play out its fixed strategy.</summary>
        public void Stand()
        {
            if (!CanAct)
                return;

            State = BlackjackStateEnum.DealerTurn;

            // Draws to sixteen, stands on all seventeens - soft ones included. Which way a house plays soft
            // seventeen is the one variation worth knowing about, and standing is the player-friendlier of the two.
            while (Dealer.Value < DealerStandsOn)
                Dealer.Add(_deck.Deal());

            Settle();
        }

        /// <summary>Works out who won, pays it, and closes the round.</summary>
        private void Settle()
        {
            State = BlackjackStateEnum.RoundOver;
            RoundsPlayed++;

            var payout = Outcome();
            LastPayout = payout;
            Chips += payout;

            if (Chips > BestChips)
                BestChips = Chips;

            if (Chips < BetSize)
                Message += "  Out of chips - ENTER for a fresh stake, ESC to leave.";
        }

        /// <summary>Who won, and what that is worth. Sets <see cref="Message" /> on the way past.</summary>
        private int Outcome()
        {
            if (Player.IsBust)
            {
                Message = $"Bust with {Player.Value}. Lost {BetSize}.";
                return -BetSize;
            }

            if (Player.IsBlackjack && Dealer.IsBlackjack)
            {
                Message = "Both blackjack - push.";
                return 0;
            }

            if (Player.IsBlackjack)
            {
                // A natural pays three to two, which is the single best thing that can happen to a player and the
                // reason the hole card matters so much.
                var bonus = BetSize*3/2;
                Message = $"Blackjack! Won {bonus}.";
                return bonus;
            }

            if (Dealer.IsBlackjack)
            {
                Message = $"Dealer blackjack. Lost {BetSize}.";
                return -BetSize;
            }

            if (Dealer.IsBust)
            {
                Message = $"Dealer bust with {Dealer.Value}. Won {BetSize}.";
                return BetSize;
            }

            if (Player.Value > Dealer.Value)
            {
                Message = $"{Player.Value} beats {Dealer.Value}. Won {BetSize}.";
                return BetSize;
            }

            if (Player.Value < Dealer.Value)
            {
                Message = $"{Dealer.Value} beats {Player.Value}. Lost {BetSize}.";
                return -BetSize;
            }

            Message = $"Both {Player.Value} - push.";
            return 0;
        }

        /// <summary>Stacks the shoe so a test can deal an exact round rather than one it had to shuffle into.</summary>
        /// <param name="cards">The cards to deal next, in order.</param>
        internal void StackDeck(params Card[] cards)
        {
            _deck.StackDeck(cards);
        }
    }
}
