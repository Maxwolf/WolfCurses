using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Core;
using WolfCurses.Games.Cards;
using WolfCurses.Games.Poker;
using WolfCurses.Games.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Games.Tests.Cards
{
    /// <summary>
    ///     The jacks-or-better paytable the poker screen draws through the library's <c>FieldList</c>.
    /// </summary>
    [Collection("GamesApp")]
    public class PokerPaytableTests
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
        public void EveryRowPromisesWhatTheGameActuallyPays(PokerCategoryEnum category, params string[] cards)
        {
            // The screen's multiples are a second copy of the ones in PokerGame.Payout, which is private, so the two
            // can drift and nothing stops compiling when they do: the table simply promises chips it does not pay,
            // which is the one lie a paytable must not tell. This walks the whole loop rather than half of it - the
            // hand is played through the real game, the row the screen would point at is chosen by the real
            // selection rule, and the number printed on that row has to be the number the game handed over.
            var game = Score(cards);
            Assert.Equal(category, game.Category);
            Assert.True(game.LastPayout > 0, $"{category} paid nothing, so this proves nothing about its row");

            var row = PokerDialog.PaytableRowFor(game.Category, game.LastPayout);
            Assert.InRange(row, 0, PokerDialog.BuildPaytable().Entries.Count - 1);

            var entry = PokerDialog.BuildPaytable().Entries[row];
            Assert.Equal(game.LastPayout.ToString(CultureInfo.InvariantCulture), entry.Value.Trim());
        }

        [Fact]
        public void APairThatPaidNothingPointsAtNoRowAtAll()
        {
            // The trap the row would otherwise walk straight into. A pair of fives is a Pair and pays nothing, so
            // keying the highlight off the category would light up "Jacks or better" for a hand that got nothing -
            // the screen claiming chips the player can see they were not given.
            var low = Score("7s", "7h", "2d", "5c", "9s");

            Assert.Equal(PokerCategoryEnum.Pair, low.Category);
            Assert.Equal(0, low.LastPayout);
            Assert.Equal(-1, PokerDialog.PaytableRowFor(low.Category, low.LastPayout));

            // And the pair that does pay lands on a real row, or the test above would pass with the whole rule
            // replaced by "never point at anything".
            var jacks = Score("Js", "Jh", "3d", "6c", "9s");
            Assert.True(PokerDialog.PaytableRowFor(jacks.Category, jacks.LastPayout) >= 0);
        }

        [Fact]
        public void AHandStillBeingChosenPointsAtNothing()
        {
            // A list only being read has nothing to point at, which is why FieldList.Selected starts at -1 - and a
            // hand whose holds are still being picked has paid nothing, so it lands on the same answer for free.
            var game = new PokerGame(new Randomizer(1));

            Assert.True(game.IsChoosing);
            Assert.Equal(-1, PokerDialog.PaytableRowFor(game.Category, game.LastPayout));
            Assert.Equal(-1, PokerDialog.BuildPaytable().Selected);
        }

        [Fact]
        public void ThePairRowIsTheOnlyOneNotNamedAfterItsCategory()
        {
            // The rule the game is named after, said where a player is reading rather than only in the code that
            // enforces it. Every other row can take the evaluator's own name for the hand.
            var entries = PokerDialog.BuildPaytable().Entries;

            Assert.Contains(entries, entry => string.Equals(entry.Label, "Jacks or better", StringComparison.Ordinal));
            Assert.DoesNotContain(entries, entry => string.Equals(entry.Label, "Pair", StringComparison.Ordinal));

            foreach (var category in new[]
                     {
                         PokerCategoryEnum.RoyalFlush, PokerCategoryEnum.StraightFlush,
                         PokerCategoryEnum.FourOfAKind, PokerCategoryEnum.FullHouse, PokerCategoryEnum.Flush,
                         PokerCategoryEnum.Straight, PokerCategoryEnum.ThreeOfAKind, PokerCategoryEnum.TwoPair
                     })
            {
                var name = PokerHand.Describe(category);
                Assert.Contains(entries, entry => string.Equals(entry.Label, name, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void PickingARowOutNeverChangesHowTallTheTableIs()
        {
            // FieldList reserves its rows rather than capping them, and the poker screen's whole row budget is
            // built on that: the paytable is drawn under a hand whose height is already decided, so a block that
            // grew by a row when something was picked out would push the message off the bottom of the terminal
            // on the hands that pay.
            var table = PokerDialog.BuildPaytable();
            var quiet = table.Render().Count;

            for (var row = 0; row < table.Entries.Count; row++)
            {
                table.Selected = row;
                Assert.Equal(quiet, table.Render().Count);
            }
        }

        [Fact]
        public void EveryRowIsTheSameWidthAndNarrowEnoughToSitBesideNothing()
        {
            // FieldList draws every row to exactly its Width in visible columns, blanks included, which is what
            // lets the screen add the block by plain concatenation without measuring anything.
            var table = PokerDialog.BuildPaytable();

            foreach (var row in table.Render())
                Assert.Equal(table.Width, AnsiText.VisibleLength(row));
        }

        [Fact]
        public void ThePaytableIsOnScreenAndTheScreenStillFitsAnEightyByTwentyFourTerminal()
        {
            // The row budget is the risk the paytable introduced, so it is asserted here as well as next door: the
            // hand and its chrome are ten rows and the paytable and its caption another ten, which leaves three
            // under the twenty-three a twenty-four row terminal has once the presenter has clipped one away.
            Assert.SkipUnless(AnsiConsole.SafeWindowHeight() >= 21,
                "a terminal this short drops the paytable on purpose");

            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            var screen = game.Screen;

            Assert.Contains("Royal flush", screen, StringComparison.Ordinal);
            Assert.Contains("Jacks or better", screen, StringComparison.Ordinal);

            // The best hand in the game at a 25 stake. An absolute rather than a comparison between rows, since
            // every row moves together if the stake or the multiples change.
            Assert.Contains("6250", screen, StringComparison.Ordinal);

            var rows = screen.Replace("\r\n", "\n").Split('\n');
            Assert.InRange(rows.Length, 1, 23);

            foreach (var row in rows)
                Assert.InRange(row.TrimEnd('\r').Length, 0, 80);
        }

        [Fact]
        public void TheRowThatIsLitIsTheHandTheMessageSaysWasMade()
        {
            // Read off the RAW frame, because the callout is a colour and nothing else. DrivenGamesApp.Screen strips
            // the escapes, so a test written against the visible text cannot see a highlight at all - the same
            // reason the minesweeper hover is pinned against its panel rather than against a screen. Here the frame
            // is the only place the two halves meet, so it is worth the parsing.
            Assert.SkipUnless(AnsiConsole.SafeWindowHeight() >= 21,
                "a terminal this short drops the paytable on purpose");
            Assert.SkipWhen(AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None,
                "a terminal asked for no escapes at all has no callout to find");

            using var app = new DrivenGamesApp();
            app.ChooseMenuItem((int) GamesCommandsEnum.Poker);

            // Drawing five fresh cards pays about one hand in five, so sixty of them not paying once is somewhere
            // past a million to one. Going broke on the way is handled by the screen itself: ENTER on a spent
            // stake starts a new one.
            for (var hand = 0; hand < 60; hand++)
            {
                app.PressChar(' ', ConsoleKey.Spacebar);

                if (app.Screen.Contains(" - paid ", StringComparison.Ordinal))
                {
                    AssertTheLitRowMatchesTheMessage(app);
                    return;
                }

                app.Type(string.Empty);
            }

            Assert.Fail("sixty hands drawn blind and not one of them paid, which is not credible");
        }

        /// <summary>
        ///     Finds the one paytable row wearing the callout style and checks it against the hand the message
        ///     names. Asserted as "exactly one", not "this one is lit", or a version that lit every row would pass.
        /// </summary>
        /// <param name="app">The arcade, showing a hand that has just paid.</param>
        private static void AssertTheLitRowMatchesTheMessage(DrivenGamesApp app)
        {
            var table = PokerDialog.BuildPaytable();
            var callout = new TextStyle(ConsoleColor.Yellow, bold: true).OpenSequence();
            var lit = new List<string>();

            foreach (var line in app.RawScreen.Replace("\r\n", "\n").Split('\n'))
            {
                // A paytable row and nothing else: they are the only lines on this screen exactly as wide as the
                // list, which is a property FieldList guarantees of every row it draws. The hold markers wear the
                // same yellow and are twenty-nine columns, so this is what keeps them out.
                if (AnsiText.VisibleLength(line) != table.Width)
                    continue;

                var label = AnsiText.StripEscapes(line).Substring(0, table.LabelWidth).TrimEnd();

                if (line.Contains(callout, StringComparison.Ordinal))
                    lit.Add(label);
            }

            var single = Assert.Single(lit);

            // The message names the hand the evaluator made, which for a paying pair is the one row whose label is
            // not the category's own name.
            var message = app.Screen;
            var at = message.IndexOf(" - paid ", StringComparison.Ordinal);
            var start = message.LastIndexOf('\n', at) + 1;
            var made = message.Substring(start, at - start).Trim();

            Assert.Equal(string.Equals(made, "Pair", StringComparison.Ordinal) ? "Jacks or better" : made, single);
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        ///     Deals an exact hand, keeps every card, and draws, so the hand that gets scored is the hand that was
        ///     set up. Holding is not optional and forgetting it fails silently: a draw with nothing held replaces
        ///     all five cards and scores whatever the shuffle produced next.
        /// </summary>
        private static PokerGame Score(params string[] cards)
        {
            var game = new PokerGame(new Randomizer(1));
            game.StackDeck(Hand(cards).ToArray());
            game.Deal();

            for (var i = 0; i < PokerHand.HandSize; i++)
                game.ToggleHold(i);

            game.Draw();
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
                    _ => (CardRankEnum) int.Parse(rankText, CultureInfo.InvariantCulture)
                };

                hand.Add(new Card(rank, suit));
            }

            return hand;
        }
    }
}
