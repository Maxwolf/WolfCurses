using System;
using System.Linq;
using WolfCurses.Apps.WordProcessor;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The spelling rules, on their own. Every test here builds its own dictionary rather than reading the
    ///     shipped one, because the shipped one has 370,105 entries and plenty of them look like typos: asserting
    ///     that some misspelling is caught against the real list would be asserting a fact about a data file.
    /// </summary>
    public class SpellCheckerTests
    {
        [Fact]
        public void AWordIsCheckedByThePartInFrontOfItsApostrophe()
        {
            // A plain word list has no contractions in it, so "don't" whole is never found. The stem is, and it is
            // the difference between a clean pass over prose and half of Hamlet underlined.
            var dictionary = SpellDictionary.ForTesting("don", "hamlet");

            Assert.Equal("don", SpellChecker.Stem("don't"), StringComparer.Ordinal);
            Assert.Equal("Hamlet", SpellChecker.Stem("Hamlet's"), StringComparer.Ordinal);
            Assert.Equal("plain", SpellChecker.Stem("plain"), StringComparer.Ordinal);

            Assert.True(SpellChecker.IsCorrect("don't", dictionary));
            Assert.True(SpellChecker.IsCorrect("Hamlet's", dictionary));
        }

        [Fact]
        public void CapitalizationDoesNotMakeAWordWrong()
        {
            var dictionary = SpellDictionary.ForTesting("wolf");

            Assert.True(SpellChecker.IsCorrect("wolf", dictionary));
            Assert.True(SpellChecker.IsCorrect("Wolf", dictionary));
        }

        [Fact]
        public void ThingsThatAreNotProseAreLeftAlone()
        {
            // Each of these is a false positive somebody would have to dismiss on every document: the sample RFCs
            // are full of all three.
            var dictionary = SpellDictionary.ForTesting("word");

            Assert.False(SpellChecker.ShouldCheck("rfc1149"));
            Assert.False(SpellChecker.ShouldCheck("IP"));
            Assert.False(SpellChecker.ShouldCheck("BBN"));
            Assert.False(SpellChecker.ShouldCheck("a"));

            Assert.True(SpellChecker.IsCorrect("rfc1149", dictionary));
            Assert.True(SpellChecker.IsCorrect("BBN", dictionary));
        }

        [Fact]
        public void AnOrdinaryWordIsStillChecked()
        {
            // The other half of the previous test, and the one that would fail silently: rules that skip too much
            // give a spell checker that never reports anything and looks like it is working.
            var dictionary = SpellDictionary.ForTesting("carrier");

            Assert.True(SpellChecker.ShouldCheck("carrier"));
            Assert.True(SpellChecker.ShouldCheck("Carrier"));

            Assert.False(SpellChecker.IsCorrect("carrer", dictionary));
        }

        [Fact]
        public void ItSuggestsTheWordAnEditAway()
        {
            var dictionary = SpellDictionary.ForTesting("carrier", "pigeon", "avian");

            // A transposition, a dropped letter, an extra letter and a wrong letter: the four ways a word is
            // mistyped, and leaving transposition out loses the most common one of them.
            Assert.Contains("carrier", SpellChecker.Suggest("carrier".Insert(3, "x"), dictionary));
            Assert.Contains("pigeon", SpellChecker.Suggest("pigen", dictionary));
            Assert.Contains("avian", SpellChecker.Suggest("avain", dictionary));
            Assert.Contains("avian", SpellChecker.Suggest("avien", dictionary));
        }

        [Fact]
        public void ASuggestionArrivesDressedLikeTheWordItReplaces()
        {
            // Correcting the first word of a sentence must not quietly lower-case it.
            var dictionary = SpellDictionary.ForTesting("pigeon");

            Assert.Contains("Pigeon", SpellChecker.Suggest("Pigen", dictionary));
            Assert.Contains("pigeon", SpellChecker.Suggest("pigen", dictionary));
        }

        [Fact]
        public void ItReachesTwoEditsAwayOnlyWhenNothingCloserExists()
        {
            var dictionary = SpellDictionary.ForTesting("carrier");

            // Two letters wrong, so nothing is one edit away and the second tier has to be built.
            Assert.Contains("carrier", SpellChecker.Suggest("carrer", dictionary));
        }

        [Fact]
        public void SomethingNothingIsCloseToOffersNothingRatherThanRubbish()
        {
            var dictionary = SpellDictionary.ForTesting("pigeon");

            Assert.Empty(SpellChecker.Suggest("qzwxjkv", dictionary));
        }

        [Fact]
        public void SuggestionsAreCappedAndNeverRepeatThemselves()
        {
            var dictionary = SpellDictionary.ForTesting("bat", "cat", "hat", "mat", "oat", "rat", "sat", "vat");

            var suggestions = SpellChecker.Suggest("at", dictionary, 3);

            Assert.Equal(3, suggestions.Count);
            Assert.Equal(suggestions.Count, suggestions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void ADictionaryThatCouldNotBeLoadedCallsEverythingCorrect()
        {
            // Rather than reporting every word in the document as wrong, which is what a checker with an empty word
            // list would otherwise do the moment the data file went missing.
            var empty = SpellDictionary.ForTesting();

            Assert.False(empty.IsUsable);
            Assert.True(SpellChecker.IsCorrect("qzwxjkv", empty));
            Assert.Empty(SpellChecker.Suggest("qzwxjkv", empty));
        }

        [Fact]
        public void TheShippedWordListIsReallyThereAndReallyLoads()
        {
            // The one test that does read the real file, because the thing worth checking is that the build copies
            // it at all: everything above would pass just as happily with no data file in the repository.
            var dictionary = SpellDictionary.Shared();

            Assert.True(dictionary.IsUsable, "the shipped word list did not load: " + dictionary.Error);
            Assert.True(dictionary.Count > 100000,
                "the shipped word list has only " + dictionary.Count + " words in it");

            Assert.True(SpellChecker.IsCorrect("carrier", dictionary));
            Assert.True(SpellChecker.IsCorrect("pigeon", dictionary));
        }
    }
}
