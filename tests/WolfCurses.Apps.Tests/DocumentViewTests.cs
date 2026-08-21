using System.IO;
using WolfCurses.Apps.WordProcessor;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The editor's drawing, on its own. Pure input to pure output with no application around it, so these run
    ///     in parallel rather than joining the driven-app collection.
    ///     <para>
    ///         Everything here is asserted with <see cref="TextStyle.None" />, which is what makes the assertions
    ///         mean anything: with a style the rows legitimately carry escape sequences, and "no control character
    ///         survived" could not be said about them at all.
    ///     </para>
    /// </summary>
    public class DocumentViewTests
    {
        private static TextViewport ViewportOnto(int line, int width = 40, int height = 3)
        {
            var viewport = new TextViewport();
            viewport.Resize(width, height);
            viewport.ScrollTo(line, 0);

            return viewport;
        }

        [Fact]
        public void AControlCharacterInTheDocumentIsDrawnRatherThanObeyed()
        {
            // A form feed written straight to a console is acted on: it moves the cursor down a row part way
            // through writing a row, so the rest of that row lands on the line below, over whatever was there. On
            // screen it looks like the document bleeding through an open menu.
            var buffer = TextBuffer.FromText("before\n\fafter");

            var rows = DocumentView.Render(buffer, ViewportOnto(0), TextStyle.None, TextStyle.None);

            Assert.All(rows, row => Assert.DoesNotContain(row, character => char.IsControl(character)));
        }

        [Fact]
        public void ReplacingAControlCharacterDoesNotMoveAnythingAfterItOnTheLine()
        {
            // The reason it is a substitution rather than a removal. Every row is the viewport's exact width, and a
            // replacement of any other width would shift the caret, the selection and every mouse hit test by
            // however many control characters happened to be earlier in the line.
            var plain = TextBuffer.FromText("ab cd");
            var withControl = TextBuffer.FromText("ab\fcd");

            var plainRows = DocumentView.Render(plain, ViewportOnto(0), TextStyle.None, TextStyle.None, false);
            var controlRows = DocumentView.Render(withControl, ViewportOnto(0), TextStyle.None, TextStyle.None, false);

            Assert.Equal(plainRows[0].Length, controlRows[0].Length);
            Assert.Equal(plainRows[0].Substring(3), controlRows[0].Substring(3), System.StringComparer.Ordinal);

            // Length alone would pass with the control character left in place, since it is one character wide in
            // the string too; it is only the terminal that treats it as no width and a movement. The cell has to
            // hold something drawable, which is the half a width check cannot see.
            Assert.False(char.IsControl(controlRows[0][2]));
        }

        [Fact]
        public void TheShippedSamplesPageBreakIsDrawnRatherThanObeyed()
        {
            // The file this was actually found in. Text files have carried a form feed as a page break for fifty
            // years, so this is not an exotic document somebody constructed: it is what the sample happens to be.
            var buffer = TextBuffer.FromText(File.ReadAllText(DocumentLibrary.DefaultDocumentPath));

            // Asked of the document rather than named, since which line it lands on is not this test's business.
            var pageBreak = -1;
            for (var line = 0; line < buffer.LineCount && pageBreak < 0; line++)
            {
                if (buffer.GetLine(line).IndexOf('\f') >= 0)
                    pageBreak = line;
            }

            Assert.True(pageBreak >= 0,
                "the sample document no longer contains a page break, so this test is guarding nothing");

            var rows = DocumentView.Render(buffer, ViewportOnto(pageBreak), TextStyle.None, TextStyle.None);

            Assert.All(rows, row => Assert.DoesNotContain(row, character => char.IsControl(character)));
        }

        [Fact]
        public void AControlCharacterIsNotScrubbedOutOfTheDocumentItself()
        {
            // Drawing is drawing. A page break has to survive being opened and saved again, which is the same
            // stance TextBuffer takes on remembering a file's line ending rather than normalizing it; an editor
            // that cleaned control characters out on load would be a reformatter.
            var buffer = TextBuffer.FromText("before\n\fafter");

            DocumentView.Render(buffer, ViewportOnto(0), TextStyle.None, TextStyle.None);

            Assert.Contains('\f', buffer.GetText());
        }
    }
}
