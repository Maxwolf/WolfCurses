using System;
using System.Text.RegularExpressions;
using WolfCurses.Apps.CardFile;
using WolfCurses.Apps.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The card file as a person meets it: keys in, frames out.
    ///     <para>
    ///         Which card the cursor is on comes off the counter notched into the box's top edge, which states it
    ///         outright, rather than from counting rows. The one test that finds a row on screen finds it by
    ///         looking for the name it wants and then checks the counter agrees, so a hit test that had drifted
    ///         cannot make the search look right.
    ///     </para>
    /// </summary>
    [Collection("Suite")]
    public class CardFileTests
    {
        private static DrivenSuite OpenCardFile()
        {
            var suite = new DrivenSuite();
            suite.ChooseMenuItem((int) OfficeCommandsEnum.CardFile);

            return suite;
        }

        /// <summary>Which card of how many, read off the box's own top edge.</summary>
        private static (int At, int Of) Counter(DrivenSuite suite)
        {
            var rows = suite.Screen.Split('\n');
            var match = Regex.Match(rows[CardChrome.BodyRow], @"(\d+) of (\d+)");

            Assert.True(match.Success, "the box's top edge did not say which card is showing:\n" + suite.Describe());

            return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        }

        /// <summary>What the card on show is called, read off the Name field.</summary>
        private static string NameShown(DrivenSuite suite)
        {
            var row = suite.Screen.Split('\n')[CardChrome.FieldRow].TrimEnd('\r');
            var at = row.IndexOf("Name", StringComparison.Ordinal);

            Assert.True(at >= 0, "no name field on the card:\n" + suite.Describe());

            return row.Substring(at + "Name".Length).Trim('\u2502', ' ');
        }

        /// <summary>Opens the View menu's Columns dialog and ticks every field, so the table has to scroll.</summary>
        private static void ShowEveryColumn(DrivenSuite suite)
        {
            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.RightArrow);
            suite.Press(ConsoleKey.RightArrow);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("Which fields does the list show", suite.Screen, StringComparison.Ordinal);

            suite.Type("A");
            suite.Type("S");
        }

        [Fact]
        public void ItOpensOnTheSampleWithTheFirstCardShowing()
        {
            using var suite = OpenCardFile();

            var counter = Counter(suite);

            Assert.Equal(1, counter.At);
            Assert.True(counter.Of >= 20, "the sample should have most of an alphabet in it");
            Assert.Contains("contacts.csv", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryLetterOfTheAlphabetHasATab()
        {
            using var suite = OpenCardFile();

            var row = suite.Screen.Split('\n')[CardChrome.TabRow + 1];

            for (var letter = 'A'; letter <= 'Z'; letter++)
                Assert.Contains(letter, row);

            Assert.Contains(Card.OtherLetter, row);
        }

        [Fact]
        public void ATabWithNothingBehindItIsDrawnGreyedRatherThanLeftOut()
        {
            using var suite = OpenCardFile();

            var row = suite.RawScreen.Split('\n')[CardChrome.TabRow + 1];

            // The greying is what says why the tab refuses the pointer, which is the bug the word processor's
            // Edit menu had: switched off in behaviour and identical in pixels.
            var live = DosTheme.Header.Apply("A");
            var dead = DosTheme.MenuDisabled.Apply("T");

            Assert.Contains(live, row, StringComparison.Ordinal);
            Assert.Contains(dead, row, StringComparison.Ordinal);
        }

        [Fact]
        public void TypingALetterFlipsToThatTab()
        {
            using var suite = OpenCardFile();

            suite.PressChar('q', ConsoleKey.Q);
            Assert.StartsWith("Q", NameShown(suite), StringComparison.Ordinal);

            suite.PressChar('c', ConsoleKey.C);
            Assert.StartsWith("C", NameShown(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void TypingALetterWithNothingBehindItSaysSoAndMovesNothing()
        {
            using var suite = OpenCardFile();

            suite.PressChar('q', ConsoleKey.Q);
            var before = NameShown(suite);

            suite.PressChar('x', ConsoleKey.X);

            Assert.Equal(before, NameShown(suite));
            Assert.Contains("Nothing is filed under X", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingATabFlipsToItToo()
        {
            using var suite = OpenCardFile();

            // The tabs are a keypad, which remembers where it drew every key, so this is the same layout the
            // drawing used rather than a second guess at it.
            var row = suite.Screen.Split('\n')[CardChrome.TabRow + 1];
            var column = row.IndexOf('M', StringComparison.Ordinal);

            Assert.True(column > 0, "no M tab was drawn:\n" + suite.Describe());

            suite.Click(CardChrome.TabRow + 1, column);

            Assert.StartsWith("M", NameShown(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void ArrowsFlipCardsAndTheCounterFollows()
        {
            using var suite = OpenCardFile();

            suite.Press(ConsoleKey.RightArrow);
            Assert.Equal(2, Counter(suite).At);

            suite.Press(ConsoleKey.RightArrow);
            Assert.Equal(3, Counter(suite).At);

            suite.Press(ConsoleKey.LeftArrow);
            Assert.Equal(2, Counter(suite).At);
        }

        [Fact]
        public void TheEndsOfTheDeckAreWallsRatherThanWrapping()
        {
            using var suite = OpenCardFile();

            suite.Press(ConsoleKey.LeftArrow);
            Assert.Equal(1, Counter(suite).At);

            suite.Press(ConsoleKey.End);
            var last = Counter(suite);
            Assert.Equal(last.Of, last.At);

            suite.Press(ConsoleKey.RightArrow);
            Assert.Equal(last.Of, Counter(suite).At);
        }

        [Fact]
        public void UpAndDownWalkTheFieldsOnTheCardAndTheCardsInTheList()
        {
            using var suite = OpenCardFile();

            // On the card they move the highlight and leave the card alone.
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.DownArrow);
            Assert.Equal(1, Counter(suite).At);

            suite.Press(ConsoleKey.F6);
            suite.Press(ConsoleKey.DownArrow);
            Assert.Equal(2, Counter(suite).At);
        }

        [Fact]
        public void TheChosenCardIsTheOneThingBothViewsAgreeAbout()
        {
            using var suite = OpenCardFile();

            suite.PressChar('r', ConsoleKey.R);
            var name = NameShown(suite);
            var at = Counter(suite).At;

            suite.Press(ConsoleKey.F6);
            Assert.Equal(at, Counter(suite).At);

            suite.Press(ConsoleKey.F5);
            Assert.Equal(name, NameShown(suite));
        }

        [Fact]
        public void BothViewsAreExactlyTheSameHeight()
        {
            using var suite = OpenCardFile();

            var card = suite.Screen.Split('\n').Length;

            suite.Press(ConsoleKey.F6);
            Assert.Equal(card, suite.Screen.Split('\n').Length);

            // Switching how you look at something must not move everything under it.
            suite.Press(ConsoleKey.Tab);
            Assert.Equal(card, suite.Screen.Split('\n').Length);
        }

        [Fact]
        public void TheListShowsTheChosenColumnsWithTheirHeadings()
        {
            using var suite = OpenCardFile();
            suite.Press(ConsoleKey.F6);

            var headings = suite.Screen.Split('\n')[CardListView.HeaderRow];

            Assert.Contains("Name", headings, StringComparison.Ordinal);
            Assert.Contains("Kind", headings, StringComparison.Ordinal);
            Assert.Contains("Phone", headings, StringComparison.Ordinal);
            Assert.DoesNotContain("Notes", headings, StringComparison.Ordinal);
        }

        [Fact]
        public void WhereACardIsDrawnInTheListIsWhereAClickOnItLands()
        {
            using var suite = OpenCardFile();
            suite.Press(ConsoleKey.F6);

            var rows = suite.Screen.Split('\n');

            // Found on screen rather than worked out, then checked against the counter, which states the answer
            // outright: a hit test that had drifted cannot make both halves agree.
            for (var row = CardListView.FirstRow; row < CardListView.FirstRow + 5; row++)
            {
                var name = rows[row].Substring(CardListView.TableColumn).TrimStart();

                if (name.Length == 0)
                    continue;

                suite.Click(row, CardListView.TableColumn + 2);

                Assert.Equal(row - CardListView.FirstRow + 1, Counter(suite).At);
            }
        }

        [Fact]
        public void ClickingTheBorderOrTheHeadingsMovesNothing()
        {
            using var suite = OpenCardFile();
            suite.Press(ConsoleKey.F6);
            suite.Press(ConsoleKey.DownArrow);

            var at = Counter(suite).At;

            suite.Click(CardListView.HeaderRow, CardListView.TableColumn + 2);
            Assert.Equal(at, Counter(suite).At);

            suite.Click(CardChrome.BodyRow, CardListView.TableColumn + 2);
            Assert.Equal(at, Counter(suite).At);
        }

        [Fact]
        public void ScrollingTheListSidewaysBringsTheLaterColumnsIntoView()
        {
            using var suite = OpenCardFile();
            suite.Press(ConsoleKey.F6);

            // Three columns fit, so there is nowhere to scroll to and the key correctly does nothing.
            var fitting = suite.Screen.Split('\n')[CardListView.HeaderRow];
            suite.Press(ConsoleKey.RightArrow);
            Assert.Equal(fitting, suite.Screen.Split('\n')[CardListView.HeaderRow]);

            ShowEveryColumn(suite);

            var before = suite.Screen.Split('\n')[CardListView.HeaderRow];
            suite.Press(ConsoleKey.RightArrow);
            var after = suite.Screen.Split('\n')[CardListView.HeaderRow];

            Assert.NotEqual(before, after);
            Assert.DoesNotContain("Name", after, StringComparison.Ordinal);
        }

        [Fact]
        public void ANoteIsWrappedOnTheCardAndFlattenedInTheList()
        {
            using var suite = OpenCardFile();

            // The Coast Guard's note is the one with real line breaks in it.
            suite.PressChar('c', ConsoleKey.C);

            var rows = suite.Screen.Split('\n');
            var first = Array.FindIndex(rows, row => row.Contains("Written apology", StringComparison.Ordinal));

            Assert.True(first > 0, "the note was not drawn:\n" + suite.Describe());

            // Its second line is on the next row, which is what "wrapped" means and what a table cannot do.
            Assert.Contains("They have a folder", rows[first + 1], StringComparison.Ordinal);

            suite.Press(ConsoleKey.F6);
            Assert.DoesNotContain("Written apology", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingAFieldPicksThatFieldWhicheverOfItsRowsIsClicked()
        {
            using var suite = OpenCardFile();
            suite.PressChar('c', ConsoleKey.C);

            var rows = suite.Screen.Split('\n');
            var note = Array.FindIndex(rows, row => row.Contains("Written apology", StringComparison.Ordinal));

            // The third line of the note, which the naive hit test would call a different field entirely.
            suite.Click(note + 2, CardChrome.FieldColumn + 3);
            suite.Press(ConsoleKey.F2);

            Assert.Contains("Notes for", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EditingAFieldChangesItAndMarksTheFileUnsaved()
        {
            using var suite = OpenCardFile();
            suite.PressChar('j', ConsoleKey.J);

            // Onto Kind, which is the second field, and then change it.
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.F2);

            Assert.Contains("Kind for", suite.Screen, StringComparison.Ordinal);

            Retype(suite, "Herring gull");

            Assert.Contains("Herring gull", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("contacts.csv *", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void RenamingACardRefilesItAndTheCursorFollows()
        {
            using var suite = OpenCardFile();
            suite.PressChar('j', ConsoleKey.J);

            var was = Counter(suite).At;

            suite.Press(ConsoleKey.F2);
            Retype(suite, "Aardvark Pike");

            // The deck is an index, so the card has moved; the cursor has to have gone with it.
            Assert.Equal("Aardvark Pike", NameShown(suite));

            var now = Counter(suite).At;
            Assert.NotEqual(was, now);

            // Really refiled rather than merely renamed. Asked of the deck rather than by naming the position it
            // ought to be at, since where an A sorts depends on what else is in the file.
            if (now > 1)
            {
                suite.Press(ConsoleKey.LeftArrow);

                Assert.True(
                    string.Compare(NameShown(suite), "Aardvark Pike", StringComparison.OrdinalIgnoreCase) < 0,
                    "the deck is not in filing order after the rename:\n" + suite.Describe());
            }
        }

        [Fact]
        public void ANewCardIsFiledInOrderAndBecomesTheOneShowing()
        {
            using var suite = OpenCardFile();

            var before = Counter(suite).Of;

            suite.Press(ConsoleKey.F7);
            Assert.Contains("new card's name", suite.Screen, StringComparison.Ordinal);

            suite.Type("Marrow, Ferris");

            Assert.Equal(before + 1, Counter(suite).Of);
            Assert.Equal("Marrow, Ferris", NameShown(suite));
        }

        [Fact]
        public void ThrowingACardAwayAsksFirst()
        {
            using var suite = OpenCardFile();
            suite.PressChar('j', ConsoleKey.J);

            var name = NameShown(suite);
            var before = Counter(suite).Of;

            suite.Press(ConsoleKey.Delete);
            Assert.Contains("Throw away the card", suite.Screen, StringComparison.Ordinal);

            // Said no, so nothing happens, which is the half that gets left untested.
            suite.Type("N");
            Assert.Equal(before, Counter(suite).Of);
            Assert.Equal(name, NameShown(suite));

            suite.Press(ConsoleKey.Delete);
            suite.Type("Y");

            Assert.Equal(before - 1, Counter(suite).Of);
            Assert.NotEqual(name, NameShown(suite));
        }

        [Fact]
        public void ClearingAFieldEmptiesIt()
        {
            using var suite = OpenCardFile();
            suite.PressChar('q', ConsoleKey.Q);

            Assert.Contains("555-0173", suite.Screen, StringComparison.Ordinal);

            // Onto Phone, then Edit, then Clear Field. It has no shortcut on purpose, which is the entry that
            // would be unreachable from the keyboard if ENTER never became a key press.
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.DownArrow);

            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.RightArrow);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.Enter);

            Assert.DoesNotContain("555-0173", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Phone cleared", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClearFieldIsGreyedOnTheNameSinceTheIndexIsByName()
        {
            using var suite = OpenCardFile();

            // The card opens with the highlight on Name, which is the one field that cannot be emptied: the deck
            // refuses a nameless card, so the entry says so by being greyed rather than by failing when pressed.
            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.RightArrow);

            Assert.True(IsGreyed(RawRowWith(suite, "Clear Field")),
                "Clear Field should be greyed with the name highlighted:\n" + suite.Describe());
        }

        [Fact]
        public void FindLooksInEveryFieldAndStartsAfterWhereYouAre()
        {
            using var suite = OpenCardFile();

            suite.Press(ConsoleKey.F9);
            Assert.Contains("Find which text", suite.Screen, StringComparison.Ordinal);

            suite.Type("Institution");
            var first = Counter(suite).At;

            // Pressing it again walks on rather than finding the card it is already sitting on. Typed over,
            // because the prompt comes back holding the last search.
            suite.Press(ConsoleKey.F9);
            Retype(suite, "Institution");

            Assert.NotEqual(first, Counter(suite).At);
        }

        [Fact]
        public void FindOffersTheLastSearchBackSoFindNextIsTwoKeys()
        {
            using var suite = OpenCardFile();

            suite.Press(ConsoleKey.F9);
            suite.Type("Human");
            var first = Counter(suite).At;

            // F9 then a bare ENTER takes the offered value, which is what makes this Find Next.
            suite.Press(ConsoleKey.F9);
            suite.Type(string.Empty);

            Assert.NotEqual(first, Counter(suite).At);
        }

        [Fact]
        public void FindSaysWhenNothingHoldsIt()
        {
            using var suite = OpenCardFile();

            suite.Press(ConsoleKey.F9);
            suite.Type("aardvark");

            Assert.Contains("Nothing holds", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ChoosingWhichColumnsTheListShowsChangesTheHeadings()
        {
            using var suite = OpenCardFile();
            suite.Press(ConsoleKey.F6);

            Assert.DoesNotContain("Email", suite.Screen.Split('\n')[CardListView.HeaderRow], StringComparison.Ordinal);

            // View, then Columns, through the menu bar rather than a shortcut, since it has none. Two presses and
            // not three, because the arrows walk past the separator on their own.
            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.RightArrow);
            suite.Press(ConsoleKey.RightArrow);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("Which fields does the list show", suite.Screen, StringComparison.Ordinal);

            // The current three arrive already ticked, so this adds Email to them rather than replacing them.
            suite.Type("4");
            suite.Type("S");

            var headings = suite.Screen.Split('\n')[CardListView.HeaderRow];

            Assert.Contains("Email", headings, StringComparison.Ordinal);
            Assert.Contains("Name", headings, StringComparison.Ordinal);
        }

        [Fact]
        public void SaveIsGreyedUntilThereIsSomethingToSave()
        {
            using var suite = OpenCardFile();

            suite.Press(ConsoleKey.F10);
            Assert.True(IsGreyed(RawRowWith(suite, "Save ")), "Save should be greyed with nothing to save");

            suite.Press(ConsoleKey.Escape);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.F2);
            Retype(suite, "Something else");

            suite.Press(ConsoleKey.F10);
            Assert.False(IsGreyed(RawRowWith(suite, "Save ")), "Save should be live once there is something to save");
        }

        [Fact]
        public void EscapeShutsAnOpenMenuBeforeItLeavesTheApplication()
        {
            using var suite = OpenCardFile();

            suite.Press(ConsoleKey.F10);
            Assert.Contains("Open...", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.Escape);
            Assert.DoesNotContain("Open...", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("contacts.csv", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.Escape);
            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWholeScreenFitsAnEightyColumnTerminal()
        {
            using var suite = OpenCardFile();

            foreach (var row in suite.Screen.Split('\n'))
                Assert.True(row.TrimEnd('\r').Length <= 80, "a row was " + row.Length + " columns wide");

            suite.Press(ConsoleKey.F6);

            foreach (var row in suite.Screen.Split('\n'))
                Assert.True(row.TrimEnd('\r').Length <= 80, "a row was " + row.Length + " columns wide");
        }

        /// <summary>The raw row holding some text, escapes and all.</summary>
        /// <param name="suite">The running suite.</param>
        /// <param name="text">What the row must contain once its escapes are taken out.</param>
        /// <returns>The row.</returns>
        private static string RawRowWith(DrivenSuite suite, string text)
        {
            foreach (var row in suite.RawScreen.Split('\n'))
            {
                if (AnsiText.StripEscapes(row).Contains(text, StringComparison.Ordinal))
                    return row;
            }

            Assert.Fail("no row held that text:\n" + suite.Describe());
            return string.Empty;
        }

        /// <summary>
        ///     Whether a menu row is painted in the greyed style. Asked of the whole row rather than of the word,
        ///     because a panel entry is styled as one run: label, padding and shortcut together.
        /// </summary>
        /// <param name="row">The raw row.</param>
        /// <returns>TRUE when it is greyed.</returns>
        private static bool IsGreyed(string row)
        {
            return row.Contains(DosTheme.MenuDisabled.OpenSequence(), StringComparison.Ordinal);
        }

        /// <summary>
        ///     Types over a prompt that arrives with a value already in it. Backspacing first is the whole of it:
        ///     typing straight into a pre-filled prompt appends, which is how one of these tests first claimed a
        ///     card was called "Jory PikeAardvark".
        /// </summary>
        /// <param name="suite">The running suite.</param>
        /// <param name="value">What to leave in the prompt.</param>
        private static void Retype(DrivenSuite suite, string value)
        {
            for (var i = 0; i < 120; i++)
                suite.Press(ConsoleKey.Backspace);

            suite.Type(value);
        }
    }
}
