using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     That <see cref="Box" /> and <see cref="TextColumns" /> measure with the <b>whole</b> escape grammar and
    ///     not just with SGR.
    ///     <para>
    ///         Every escape in every other test of those two classes is <c>ESC[…m</c>, which is exactly the subset a
    ///         regular expression gets right — so both could be reverted to the regex <see cref="Box" /> used to
    ///         carry and the entire suite would still pass. <see cref="Graphics.AnsiTextTests" /> pins the grammar
    ///         itself and pins the presenter forwarding to it, and says nothing about whether these two callers use
    ///         it. These are the shapes that separate the two: an OSC 8 hyperlink, whose URL is not in an SGR
    ///         sequence at all, and a CSI whose final byte is not a letter.
    ///     </para>
    /// </summary>
    public class VisibleWidthGrammarTests
    {
        private const char Esc = '';

        /// <summary>An OSC 8 hyperlink: four visible columns wrapped around a URL that must count as nothing.</summary>
        private static readonly string _hyperlink =
            $"{Esc}]8;;https://example.com{Esc}\\link{Esc}]8;;{Esc}\\";

        /// <summary>Bracketed paste: a CSI whose final byte is '~', which an SGR-only parser leaves on the row.</summary>
        private static readonly string _bracketed = $"{Esc}[200~abcd";

        [Fact]
        public void BoxMeasuresAHyperlinkRowByItsVisibleWidthNotItsUrl()
        {
            var box = new Box {Title = "T"};

            var exotic = box.Render($"{_hyperlink}\n{_bracketed}\nabcd");
            var plain = box.Render("abcd\nabcd\nabcd");

            // The same SIZE, not the same text - the rows say "link" and "abcd", which is the content and not the
            // point. All three rows are four columns wide however many bytes they take to say so, and measured
            // with an SGR-only regex the first two are thirty-odd columns and the box comes out enormous.
            Assert.Equal(WidthOf(plain), WidthOf(exotic));
            Assert.Equal(LinesOf(plain), LinesOf(exotic));
        }

        /// <summary>The visible width of the widest row of a rendered block.</summary>
        private static int WidthOf(string block)
        {
            var width = 0;
            foreach (var row in block.Replace("\r\n", "\n").Split('\n'))
                width = Math.Max(width, AnsiText.VisibleLength(row));

            return width;
        }

        /// <summary>How many rows a rendered block has.</summary>
        private static int LinesOf(string block) => block.Replace("\r\n", "\n").Split('\n').Length;

        [Fact]
        public void EveryRowOfABoxComesOutTheSameWidth()
        {
            var box = new Box();

            var rendered = box.Render($"{_hyperlink}\n{_bracketed}\nabcd");

            var width = -1;
            foreach (var row in rendered.Split('\n'))
            {
                var visible = AnsiText.VisibleLength(row.TrimEnd('\r'));
                if (width < 0)
                    width = visible;

                Assert.Equal(width, visible);
            }
        }

        [Fact]
        public void AHyperlinkColumnDoesNotPushItsNeighbourOutOfTrue()
        {
            var joined = TextColumns.Join(1, $"{_hyperlink}\n{_bracketed}\nabcd", "L\nM\nR");
            var rows = AnsiText.StripEscapes(joined).Replace("\r\n", "\n").Split('\n');

            // Absolute, not merely consistent: the left column is four columns wide and the gap is one, so the
            // right column starts at five and nowhere else. An SGR-only measure puts it at thirty-something on the
            // first two rows and at five on the third.
            Assert.Equal(5, rows[0].IndexOf('L'));
            Assert.Equal(5, rows[1].IndexOf('M'));
            Assert.Equal(5, rows[2].IndexOf('R'));
        }
    }
}
