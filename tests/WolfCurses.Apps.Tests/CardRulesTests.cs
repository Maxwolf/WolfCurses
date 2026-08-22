using System;
using System.IO;
using WolfCurses.Apps.CardFile;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The card file's rules, with no console anywhere near them.
    ///     <para>
    ///         Most of these are about one thing: this is the screen that reads back a file it wrote itself, and
    ///         the file can have been edited by hand in between. So the tests that matter are the mangled ones -
    ///         columns moved, a column deleted, a row a field short, no header at all - and the round trip that
    ///         says nothing was lost in the meantime.
    ///     </para>
    /// </summary>
    public class CardRulesTests
    {
        /// <summary>A deck with the three values a naive CSV writer loses.</summary>
        private static CardDeck Awkward()
        {
            var deck = new CardDeck {NewLine = "\n"};

            deck.Add(new Card("Vance, Aurelia", "Human", "555-0142", "a@example.gov", "City Hall",
                "Says \"retroactively permitted\".\nOn two lines.\nOn three, in fact."));

            deck.Add(new Card("Quill", "Otter", "555-0188", string.Empty, "The Long Weir", "Tailor."));

            return deck;
        }

        [Fact]
        public void WritingADeckAndReadingItBackGivesTheSameCards()
        {
            var written = Awkward();
            var read = CardFileLibrary.Parse(CardFileLibrary.Format(written));

            Assert.Equal(written.Count, read.Count);

            for (var i = 0; i < written.Count; i++)
            {
                for (var field = 0; field < written.Cards[i].Fields; field++)
                    Assert.Equal(written.Cards[i][field], read.Cards[i][field]);
            }
        }

        [Fact]
        public void ANoteWithLineBreaksInItSurvivesTheRoundTrip()
        {
            // The case that settles the whole design of the reader: a record and a line are different things.
            var read = CardFileLibrary.Parse(CardFileLibrary.Format(Awkward()));
            var note = read.Cards[1][Card.NotesField];

            Assert.Equal(3, note.Split('\n').Length);
            Assert.Contains("On three, in fact.", note, StringComparison.Ordinal);
        }

        [Fact]
        public void ANoteWithADoubledQuoteAndADelimiterSurvivesToo()
        {
            var read = CardFileLibrary.Parse(CardFileLibrary.Format(Awkward()));

            Assert.Contains("\"retroactively permitted\"", read.Cards[1][Card.NotesField], StringComparison.Ordinal);
            Assert.Equal("Vance, Aurelia", read.Cards[1].Name);
        }

        [Fact]
        public void MovingTheColumnsAboutChangesNothing()
        {
            // The whole reason the reader looks columns up by name. Somebody has opened the file in the word
            // processor and rearranged it, which is three menu items away from here.
            var moved = CardFileLibrary.Parse(
                "Notes,Phone,Name,Address,Email,Kind\n" +
                "Tailor.,555-0188,Quill,The Long Weir,q@example.net,Otter\n");

            Assert.Equal(1, moved.Count);
            Assert.Equal("Quill", moved.Cards[0].Name);
            Assert.Equal("Otter", moved.Cards[0][1]);
            Assert.Equal("555-0188", moved.Cards[0][2]);
            Assert.Equal("Tailor.", moved.Cards[0][Card.NotesField]);
        }

        [Fact]
        public void AColumnTheFileNoLongerHasReadsAsEmptyAndTheRestStillArrive()
        {
            var read = CardFileLibrary.Parse("Name,Phone\nQuill,555-0188\n");

            Assert.Equal("Quill", read.Cards[0].Name);
            Assert.Equal("555-0188", read.Cards[0][2]);
            Assert.Equal(string.Empty, read.Cards[0][Card.NotesField]);
        }

        [Fact]
        public void ARowThatIsAFieldShortIsReadRatherThanRefused()
        {
            var read = CardFileLibrary.Parse(
                "Name,Kind,Phone,Email,Address,Notes\n" +
                "Quill,Otter,555-0188\n");

            Assert.Equal(1, read.Count);
            Assert.Equal("555-0188", read.Cards[0][2]);
            Assert.Equal(string.Empty, read.Cards[0][4]);
        }

        [Fact]
        public void AColumnNobodyDeclaredIsIgnoredRatherThanShiftingEverything()
        {
            var read = CardFileLibrary.Parse(
                "Name,Kind,Phone,Email,Address,Notes,Favourite Colour\n" +
                "Quill,Otter,555-0188,q@example.net,The Long Weir,Tailor.,brown\n");

            Assert.Equal("Tailor.", read.Cards[0][Card.NotesField]);
        }

        [Fact]
        public void AFileWithNoHeaderIsReadByPositionAndKeepsItsFirstRow()
        {
            // The first row is a card, not a header, and losing it would be the classic way to lose one silently.
            var read = CardFileLibrary.Parse("Quill,Otter,555-0188\nVance,Human,555-0142\n");

            Assert.Equal(2, read.Count);
            Assert.Equal("Quill", read.Cards[0].Name);
            Assert.Equal("Vance", read.Cards[1].Name);
        }

        [Fact]
        public void ARowWithNoNameIsSkippedRatherThanFiledUnderNothing()
        {
            var read = CardFileLibrary.Parse(
                "Name,Kind,Phone\n" +
                ",Otter,555-0188\n" +
                "Quill,Otter,555-0199\n");

            Assert.Equal(1, read.Count);
            Assert.Equal("Quill", read.Cards[0].Name);
        }

        [Fact]
        public void AnEmptyFileIsAnEmptyDeckRatherThanAThrow()
        {
            Assert.Equal(0, CardFileLibrary.Parse(string.Empty).Count);
            Assert.Equal(0, CardFileLibrary.Parse(null).Count);
            Assert.Equal(0, CardFileLibrary.Parse("Name,Kind,Phone\n").Count);
        }

        [Fact]
        public void AFileSavedUntouchedKeepsItsLineEndings()
        {
            var crlf = CardFileLibrary.Parse("Name,Kind\r\nQuill,Otter\r\n");
            var lf = CardFileLibrary.Parse("Name,Kind\nQuill,Otter\n");

            Assert.Contains("\r\n", CardFileLibrary.Format(crlf), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", CardFileLibrary.Format(lf), StringComparison.Ordinal);
        }

        [Fact]
        public void TheDeckIsAnIndexRatherThanAList()
        {
            var deck = new CardDeck();

            deck.Add(new Card("Quill"));
            deck.Add(new Card("aurelia"));
            deck.Add(new Card("Meech"));

            // Sorted, and without case mattering, or half the alphabet would file after the whole of the other.
            Assert.Equal("aurelia", deck.Cards[0].Name);
            Assert.Equal("Meech", deck.Cards[1].Name);
            Assert.Equal("Quill", deck.Cards[2].Name);
        }

        [Fact]
        public void RenamingACardMovesItAndSaysWhereItWent()
        {
            var deck = new CardDeck();

            deck.Add(new Card("Aurelia"));
            deck.Add(new Card("Meech"));
            deck.Add(new Card("Quill"));

            var card = deck.Cards[0];
            card[Card.NameField] = "Zephyr";

            var moved = deck.Resort(0);

            // The cursor has to follow the card rather than the position, or renaming leaves it on somebody else.
            Assert.Equal(2, moved);
            Assert.Same(card, deck.Cards[moved]);
        }

        [Fact]
        public void ACardWithNoNameIsRefused()
        {
            var deck = new CardDeck();

            Assert.Equal(-1, deck.Add(new Card("   ")));
            Assert.Equal(-1, deck.Add(null));
            Assert.Equal(0, deck.Count);
        }

        [Fact]
        public void FindStartsAfterTheCardItWasGivenAndWrapsRoundToIt()
        {
            var deck = new CardDeck();

            deck.Add(new Card("Aurelia", "Human"));
            deck.Add(new Card("Meech", "Human"));
            deck.Add(new Card("Quill", "Otter"));

            // Starting where you are would find where you are, forever, and Find Next would never move.
            Assert.Equal(1, deck.Find("Human", 0));
            Assert.Equal(0, deck.Find("Human", 1));

            // And a deck with one match still finds it rather than reporting nothing.
            Assert.Equal(2, deck.Find("Otter", 2));
        }

        [Fact]
        public void FindLooksInEveryFieldAndIgnoresCase()
        {
            var deck = new CardDeck();
            deck.Add(new Card("Quill", "Otter", "555-0188", string.Empty, string.Empty, "Tailor."));

            Assert.Equal(0, deck.Find("TAILOR", -1));
            Assert.Equal(0, deck.Find("0188", -1));
            Assert.Equal(-1, deck.Find("locksmith", -1));
            Assert.Equal(-1, deck.Find(string.Empty, -1));
        }

        [Fact]
        public void ATabWithNothingBehindItSaysSo()
        {
            var deck = new CardDeck();

            deck.Add(new Card("Quill"));
            deck.Add(new Card("11 Bell Court"));

            Assert.True(deck.HasBehind('Q'));
            Assert.True(deck.HasBehind('q'));
            Assert.False(deck.HasBehind('T'));
            Assert.Equal(-1, deck.FirstBehind('T'));

            // A name that does not start with a letter is filed behind the last tab rather than nowhere.
            Assert.True(deck.HasBehind(Card.OtherLetter));
            Assert.Equal(Card.OtherLetter, deck.Cards[deck.FirstBehind(Card.OtherLetter)].IndexLetter);
        }

        [Fact]
        public void ADeckKnowsWhetherItMatchesWhatIsOnDisk()
        {
            var deck = CardFileLibrary.Parse("Name,Kind\nQuill,Otter\n");

            Assert.False(deck.IsModified);

            deck.Cards[0][1] = "Sea otter";
            deck.Touch();
            Assert.True(deck.IsModified);

            deck.MarkSaved();
            Assert.False(deck.IsModified);
        }

        [Fact]
        public void TheShippedSampleOpensAndIsWhatItSaysItIs()
        {
            // Without this the copy step could break and every test above would go on passing against its own
            // hand-written strings, which is the shape of dead test the arcade's artwork copy exists to prevent.
            Assert.True(File.Exists(CardFileLibrary.DefaultCardsPath),
                "the sample was not copied to " + CardFileLibrary.DefaultCardsPath);

            var deck = CardFileLibrary.TryLoad(CardFileLibrary.DefaultCardsPath, out var error);

            Assert.Null(error);
            Assert.NotNull(deck);
            Assert.True(deck.Count >= 20, "the sample should have a card for most of the alphabet");

            // Two letters are left empty on purpose so the greyed tabs are visible.
            Assert.False(deck.HasBehind('T'));
            Assert.False(deck.HasBehind('X'));
            Assert.True(deck.HasBehind(Card.OtherLetter));

            // And the awkward rows are really in there rather than having been tidied away.
            Assert.True(deck.Find("\"good boy\"", -1) >= 0, "the doubled-quote row is missing");
            Assert.True(deck.Find("Petty Officer Hale", -1) >= 0, "the multi-line note is missing");
        }

        [Fact]
        public void TheShippedSampleSurvivesBeingWrittenBackOut()
        {
            var deck = CardFileLibrary.TryLoad(CardFileLibrary.DefaultCardsPath, out _);
            var again = CardFileLibrary.Parse(CardFileLibrary.Format(deck));

            Assert.Equal(deck.Count, again.Count);

            for (var i = 0; i < deck.Count; i++)
            {
                for (var field = 0; field < deck.Cards[i].Fields; field++)
                    Assert.Equal(deck.Cards[i][field], again.Cards[i][field]);
            }
        }

        [Fact]
        public void AValueIsFlattenedForTheTableAndNotForTheCard()
        {
            // Opposite treatments of the same field, on purpose: a row of a table is a row.
            Assert.Equal("one two three", CardListView.Flatten("one\r\ntwo\nthree"));
            Assert.Equal(string.Empty, CardListView.Flatten(null));
        }

        [Fact]
        public void AColumnIsAsWideAsTheWidestThingInItUpToACap()
        {
            var deck = new CardDeck();

            deck.Add(new Card("Quill", "Otter"));
            deck.Add(new Card("A name that is very considerably longer than the cap allows", "Human"));

            var widths = CardListView.ColumnWidths(deck, new[] {0, 1});

            Assert.True(widths[0] < 30, "one long name must not push everything else off the screen");

            // "Kind" is four letters and the longest value in it is five, plus the gutter either side.
            Assert.Equal(7, widths[1]);
        }
    }
}
