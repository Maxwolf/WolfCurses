using System;
using System.Globalization;
using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     The other half of turning a stored line into a drawable one. A terminal does not draw a control
    ///     character, it obeys it, and the first one anybody meets is not an exotic byte somebody typed: it is the
    ///     form feed a text file has used as a page break for fifty years.
    ///     <para>
    ///         Every control character in here is written as an escape rather than as itself. That is not fussiness:
    ///         a literal U+0085 in a source file is a line break to the compiler, so a test written the obvious way
    ///         does not compile, and the ones that do are invisible to anybody reading the file.
    ///     </para>
    /// </summary>
    public class ControlPicturesTests
    {
        /// <summary>The picture for a form feed, which is what a page break has to turn into.</summary>
        private const char FormFeedPicture = '␌';

        /// <summary>The picture for an escape.</summary>
        private const char EscapePicture = '␛';

        /// <summary>The picture for a tab, which is what one becomes if it reaches here unexpanded.</summary>
        private const char TabPicture = '␉';

        [Fact]
        public void AFormFeedBecomesSomethingThatCanBeDrawn()
        {
            // THE trap, and the bug this was written for. Written raw, a form feed moves the cursor down a row part
            // way through writing a row, so everything after it on that row lands on the line below, over whatever
            // was already there. It looks like the document bleeding through a menu.
            var drawn = ControlPictures.Replace("page\fbreak");

            Assert.DoesNotContain('\f', drawn);
            Assert.Equal("page" + FormFeedPicture + "break", drawn, StringComparer.Ordinal);
        }

        [Fact]
        public void EveryReplacementIsExactlyOneCharacterWide()
        {
            // The whole contract. A caller has already worked out its columns with TabStops, so a substitution of
            // any other width moves the caret, the selection and every mouse hit test by however many control
            // characters happened to be earlier in the line.
            const string stored = "\0ab\fcde";

            var drawn = ControlPictures.Replace(stored);

            Assert.Equal(stored.Length, drawn.Length);
            for (var i = 0; i < stored.Length; i++)
                Assert.Equal(ControlPictures.For(stored[i]), drawn[i]);
        }

        [Fact]
        public void AnEscapeIsReplacedBecauseItIsTheOneWithTeeth()
        {
            // A document holding escape sequences is not exotic: a captured terminal session or a coloured log is
            // exactly that. Passed through, it repaints the interface it is being read in.
            var drawn = ControlPictures.Replace("\u001B[2Jgone");

            Assert.DoesNotContain('\u001B', drawn);
            Assert.Equal(EscapePicture + "[2Jgone", drawn, StringComparer.Ordinal);
        }

        [Fact]
        public void TextWithNothingToReplaceComesBackAsTheSameReference()
        {
            // The ordinary line is every line, and this runs per row per frame, so it costs a scan and no
            // allocation. Same stance TabStops.Expand takes.
            const string plain = "an ordinary line of prose";

            Assert.Same(plain, ControlPictures.Replace(plain));
            Assert.Same(string.Empty, ControlPictures.Replace(string.Empty));
            Assert.Null(ControlPictures.Replace(null));
        }

        [Fact]
        public void ASpaceIsLeftAloneEvenThoughItHasAPictureOfItsOwn()
        {
            // Unicode gives a space a picture, which is a trap rather than an invitation: it is not a control
            // character, and a document full of visible space markers is not what anybody asked for.
            Assert.Equal(' ', ControlPictures.For(' '));
            Assert.Same("a b", ControlPictures.Replace("a b"));
        }

        [Fact]
        public void ATabReachingHereBecomesOnePictureRatherThanARunOfSpaces()
        {
            // Which is why the order is expand first, replace second. This answer is right for a caller that does
            // not lay out tab stops and wrong for one that does, and the two are not interchangeable.
            Assert.Equal("a" + TabPicture + "b", ControlPictures.Replace("a\tb"), StringComparer.Ordinal);
            Assert.Equal("a       b", ControlPictures.Replace(TabStops.Expand("a\tb", 8)), StringComparer.Ordinal);
        }

        [Fact]
        public void AControlWithNoPictureOfItsOwnStillGetsReplaced()
        {
            // The C1 range, which Unicode has no pictures for. Worth replacing anyway: one of them is an eight-bit
            // control sequence introducer, so a terminal in the right mode acts on it exactly as it would an escape.
            Assert.Equal(ControlPictures.Unknown, ControlPictures.For('\u009B'));
            Assert.Equal(ControlPictures.Unknown, ControlPictures.For('\u0085'));

            Assert.False(char.IsControl(ControlPictures.Unknown),
                "the stand-in for a control character must not itself be one");
        }

        [Fact]
        public void NoControlCharacterAnywhereSurvivesBeingReplaced()
        {
            // Asserted over the whole range rather than over the handful anybody thinks of, since the point is that
            // nothing reaches the terminal, not that the listed ones do not.
            for (var code = 0; code <= 0x9F; code++)
            {
                var character = (char) code;
                if (!char.IsControl(character))
                    continue;

                Assert.False(char.IsControl(ControlPictures.For(character)),
                    "U+" + code.ToString("X4", CultureInfo.InvariantCulture) + " came back as a control character");
            }
        }
    }
}
