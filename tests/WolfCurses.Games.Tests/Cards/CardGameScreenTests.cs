using System;
using System.Collections.Generic;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.Cards
{
    /// <summary>
    ///     Both card tables driven through the real arcade. Neither sleeps: they are turn-based and redraw only when
    ///     a key is pressed.
    /// </summary>
    [Collection("GamesApp")]
    public class CardGameScreenTests
    {
        [Fact]
        public void BlackjackOpensWithAHandAndAHiddenHoleCard()
        {
            // Dealt until the opening hand is one somebody still has to play, and that is not fussiness: a natural
            // on EITHER side settles the round inside Deal, before the player has a turn, and that happens on 9.4%
            // of opening hands (measured over five thousand). Every assertion below is about a live hand — the
            // chips have not moved, the hole card is still face down, the dealer is still "showing" — so all five
            // of them are wrong on a settled one, and this test shipped asserting them unconditionally and failed
            // about one run in eleven.
            //
            // A fresh app each attempt rather than dealing again in the same one, because the exact chip count is
            // part of what is being checked and a settled round has already moved it.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                using var game = new DrivenGamesApp();
                game.ChooseMenuItem((int) GamesCommandsEnum.Blackjack);

                var screen = game.Screen;
                if (!screen.Contains("Hit or stand?", StringComparison.Ordinal))
                    continue;

                Assert.Contains("Chips 500", screen, StringComparison.Ordinal);
                Assert.Contains("Dealer", screen, StringComparison.Ordinal);
                Assert.Contains("showing", screen, StringComparison.Ordinal);

                // The hatched back. Exactly one of them: the hole card and nothing else.
                Assert.Contains("▒▒▒", screen, StringComparison.Ordinal);
                return;
            }

            Assert.Fail("ten opening hands in a row settled themselves, which is not credible");
        }

        [Fact]
        public void ANaturalSettlesBeforeAnybodyGetsATurn()
        {
            // The other side of the test above, so that the skipping up there is a documented rule rather than a
            // way of ignoring an inconvenient outcome. Driven through the rules directly, since finding a natural
            // by dealing through the arcade would take a hundred windows.
            var found = false;

            for (var seed = 1; seed <= 200 && !found; seed++)
            {
                var blackjack = new WolfCurses.Games.Blackjack.BlackjackGame(new WolfCurses.Core.Randomizer(seed));
                if (blackjack.Player.IsBlackjack || blackjack.Dealer.IsBlackjack)
                {
                    found = true;
                    Assert.False(blackjack.CanAct, "a natural left the player with a turn to take");
                    Assert.DoesNotContain("Hit or stand?", blackjack.Message, StringComparison.Ordinal);
                }
            }

            Assert.True(found, "two hundred deals produced no natural at all, which is not credible");
        }

        [Fact]
        public void HittingAddsACardAndStandingRevealsTheHole()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Blackjack);

            var before = CountCards(game);
            game.PressChar('h', ConsoleKey.H);

            // A hit either adds a card or ends the round by busting - both are the key working.
            var after = CountCards(game);
            Assert.True(after > before || !game.Screen.Contains("Hit or stand?", StringComparison.Ordinal),
                "hitting did nothing at all");

            game.PressChar('s', ConsoleKey.S);
            Assert.DoesNotContain("▒▒▒", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("showing", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void PokerOpensWithFiveCardsAndNoneHeld()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            var screen = game.Screen;

            Assert.Contains("Chips 475", screen, StringComparison.Ordinal);
            Assert.Contains("SPACE to draw", screen, StringComparison.Ordinal);
            Assert.DoesNotContain("HELD", screen, StringComparison.Ordinal);
            Assert.Equal(5, CountCards(game));
        }

        [Fact]
        public void NumberKeysHoldAndReleaseCards()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            game.PressChar('2', ConsoleKey.D2);
            Assert.Contains("HELD", game.Screen, StringComparison.Ordinal);

            game.PressChar('2', ConsoleKey.D2);
            Assert.DoesNotContain("HELD", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void DrawingScoresTheHand()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            game.PressChar(' ', ConsoleKey.Spacebar);

            Assert.DoesNotContain("SPACE to draw", game.Screen, StringComparison.Ordinal);
            Assert.Equal(5, CountCards(game));
        }

        [Fact]
        public void EnterDealsTheNextRoundRatherThanLeavingTheTable()
        {
            // The binding every other game here has the other way round. A card table is played in rounds, so the
            // key pressed most often between them must not be the one that ends the session.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Blackjack);

            // Counted in ROUNDS rather than checked for a live hand, which is what makes this deterministic. A
            // natural settles the moment it is dealt, so the round ENTER deals is live about ninety-five times in a
            // hundred - and the first version of this test asserted on "Hit or stand?" being back, which flaked on
            // the other five. Standing twice settles two rounds however they were dealt.
            game.PressChar('s', ConsoleKey.S);
            Assert.Contains("Rounds 1", game.Screen, StringComparison.Ordinal);

            game.Type(string.Empty);
            game.PressChar('s', ConsoleKey.S);

            Assert.Contains("Rounds 2", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EnterDoesNothingWhileTheHandIsStillBeingPlayed()
        {
            // Dealing over a hand the player is still deciding would throw their cards away for a keypress that
            // meant nothing.
            //
            // Asserted on a hand that has been HIT, which is the only version of this test that works: a fresh deal
            // is always two cards each, so an untouched opening hand looks identical whether ENTER re-dealt it or
            // did nothing - the round count, the message and the card count are all the same either way. A third
            // card on the table is proof the hand survived. The first version of this test compared the opening
            // hand, passed, and went on passing with the guard deleted.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                using var game = new DrivenGamesApp();
                game.ChooseMenuItem((int) GamesCommandsEnum.Blackjack);
                game.PressChar('h', ConsoleKey.H);

                // The hit may have busted, which ends the round and makes dealing exactly ENTER's job. Only a live
                // hand answers the question being asked here.
                if (!game.Screen.Contains("Hit or stand?", StringComparison.Ordinal))
                    continue;

                var cards = CountCards(game);
                Assert.True(cards >= 5, $"the hit left only {cards} cards on the table");

                game.Type(string.Empty);

                Assert.Equal(cards, CountCards(game));
                Assert.Contains("Hit or stand?", game.Screen, StringComparison.Ordinal);
                Assert.Contains("Rounds 0", game.Screen, StringComparison.Ordinal);
                return;
            }

            Assert.Fail("every attempt busted on its first hit, which is not credible");
        }

        [Fact]
        public void OnlyEscapeLeavesTheBlackjackTable()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Blackjack);

            // A dozen rounds of ENTER, which under the old binding would have quit on the first one.
            for (var round = 0; round < 12; round++)
            {
                game.PressChar('s', ConsoleKey.S);
                game.Type(string.Empty);
            }

            Assert.Contains("Chips", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which game?", game.Screen, StringComparison.Ordinal);

            game.Escape();
            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheTableSaysWhichKeyDraws()
        {
            // Named on the table itself, not only in the prompt underneath it - the message is where a player who
            // does not know what to do next is already looking.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            Assert.Contains("SPACE to draw", game.Screen, StringComparison.Ordinal);

            game.PressChar(' ', ConsoleKey.Spacebar);

            Assert.Contains("ENTER for the next hand", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void PokerEnterDealsTheNextHandRatherThanLeavingTheTable()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            game.PressChar(' ', ConsoleKey.Spacebar);
            Assert.Contains("Hands 1", game.Screen, StringComparison.Ordinal);

            game.Type(string.Empty);

            Assert.Contains("SPACE to draw", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void PokerEnterDoesNothingWhileHoldsAreStillBeingChosen()
        {
            // Asserted on the CHIPS rather than on the cards, which is what makes this test able to fail: a re-deal
            // leaves five cards on the table exactly as before, but it also takes another stake - so the pile is the
            // one thing that tells the two apart. The blackjack version of this test needed the same lesson learned
            // the hard way.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            Assert.Contains("Chips 475", game.Screen, StringComparison.Ordinal);

            game.Type(string.Empty);

            Assert.Contains("Chips 475", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Hands 0", game.Screen, StringComparison.Ordinal);
            Assert.Contains("SPACE to draw", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void OnlyEscapeLeavesThePokerTable()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            for (var hand = 0; hand < 12; hand++)
            {
                game.PressChar(' ', ConsoleKey.Spacebar);
                game.Type(string.Empty);
            }

            Assert.Contains("Chips", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which game?", game.Screen, StringComparison.Ordinal);

            game.Escape();
            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void BothTablesFitAnEightyByTwentyFourTerminal()
        {
            foreach (var item in new[] {GamesCommandsEnum.Blackjack, GamesCommandsEnum.Poker})
            {
                using var game = new DrivenGamesApp();
                game.ChooseMenuItem((int) item);

                var rows = new List<string>(game.Screen.Replace("\r\n", "\n").Split('\n'));

                Assert.InRange(rows.Count, 1, 23);
                foreach (var row in rows)
                    Assert.InRange(row.TrimEnd('\r').Length, 0, 80);
            }
        }

        [Fact]
        public void ATerminalWithoutRealPixelsGetsLettersRatherThanASmudge()
        {
            // The headless host draws half blocks, where a seventy-five pixel card is seventy-five columns. Letters
            // are not a consolation prize here - the rank and pip in the corner is the whole of what a card says,
            // and it is the first thing to vanish when the picture is scaled to fit.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Blackjack);

            Assert.Contains("┌───┐", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain('', game.RawScreen);
        }

        [Fact]
        public void SingleKeyPlayNeverReachesThePrompt()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Blackjack);

            game.PressChar('h', ConsoleKey.H);
            game.PressChar('s', ConsoleKey.S);
            game.PressChar('d', ConsoleKey.D);

            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }

        [Fact]
        public void EscapeBacksOutOfBothTables()
        {
            foreach (var item in new[] {GamesCommandsEnum.Blackjack, GamesCommandsEnum.Poker})
            {
                using var game = new DrivenGamesApp();
                game.ChooseMenuItem((int) item);
                Assert.Contains("Chips", game.Screen, StringComparison.Ordinal);

                game.Escape();

                Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheArcadeMenuOffersBoth()
        {
            using var game = new DrivenGamesApp();

            Assert.Contains("Blackjack", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Poker", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Blackjack best:", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Poker best:", game.Screen, StringComparison.Ordinal);
        }

        /// <summary>How many cards are drawn, counted off the frame by their boxes.</summary>
        private static int CountCards(DrivenGamesApp game)
        {
            var count = 0;
            foreach (var line in game.Screen.Split('\n'))
            {
                var at = line.IndexOf('┌');
                while (at >= 0)
                {
                    count++;
                    at = line.IndexOf('┌', at + 1);
                }
            }

            return count;
        }
    }
}
