using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     The scroll window over a document. Small enough to be all arithmetic, which is exactly why it is worth
    ///     pinning: every one of these is a line somebody would otherwise write inline in a render method and get
    ///     right on the first three cases.
    /// </summary>
    public class TextViewportTests
    {
        [Fact]
        public void AViewportStartsAtTheTopLeft()
        {
            var viewport = new TextViewport(80, 24);

            Assert.Equal(0, viewport.FirstLine);
            Assert.Equal(0, viewport.FirstColumn);
            Assert.Equal(80, viewport.Width);
            Assert.Equal(24, viewport.Height);
        }

        [Fact]
        public void ASizeOfZeroBecomesOneRatherThanBreakingTheArithmetic()
        {
            // A headless host really does report zero, and a viewport with no rows makes every calculation here
            // meaningless rather than merely small.
            var viewport = new TextViewport(0, 0);

            Assert.Equal(1, viewport.Width);
            Assert.Equal(1, viewport.Height);

            viewport.Resize(-10, -10);
            Assert.Equal(1, viewport.Width);
            Assert.Equal(1, viewport.Height);
        }

        [Fact]
        public void ScrollingNeverGoesAboveOrLeftOfTheDocumentStart()
        {
            var viewport = new TextViewport(20, 10);

            viewport.ScrollTo(-5, -5);
            Assert.Equal(0, viewport.FirstLine);
            Assert.Equal(0, viewport.FirstColumn);

            viewport.ScrollBy(-99, -99);
            Assert.Equal(0, viewport.FirstLine);
            Assert.Equal(0, viewport.FirstColumn);
        }

        [Fact]
        public void ClampingStopsAtTheLastScreenfulRatherThanTheLastLine()
        {
            // The classic version of this bug is PageDown at the end of a long document walking the origin off into
            // empty space, so the text scrolls away entirely. The furthest you may go is the last full screen.
            var viewport = new TextViewport(20, 10);
            viewport.ScrollTo(500, 0);

            viewport.ClampToDocument(100);

            Assert.Equal(90, viewport.FirstLine);
        }

        [Fact]
        public void ADocumentShorterThanTheWindowCannotScrollAtAll()
        {
            var viewport = new TextViewport(20, 10);
            viewport.ScrollTo(5, 0);

            viewport.ClampToDocument(4);

            Assert.Equal(0, viewport.FirstLine);
        }

        [Fact]
        public void RevealingSomethingBelowScrollsTheLeastAmountThatWorks()
        {
            // Minimal movement, not centring: a caret that steps off the bottom should bring one new row into view,
            // not jump the document half a screen and lose the reader's place.
            var viewport = new TextViewport(20, 10);

            Assert.True(viewport.EnsureVisible(new TextPosition(10, 0)));

            Assert.Equal(1, viewport.FirstLine);
        }

        [Fact]
        public void RevealingSomethingAboveScrollsExactlyToIt()
        {
            var viewport = new TextViewport(20, 10);
            viewport.ScrollTo(50, 0);

            viewport.EnsureVisible(new TextPosition(30, 0));

            Assert.Equal(30, viewport.FirstLine);
        }

        [Fact]
        public void RevealingSomethingAlreadyOnScreenMovesNothingAndSaysSo()
        {
            // The false return is the useful half: a caller can skip a redraw when the origin did not change.
            var viewport = new TextViewport(20, 10);
            viewport.ScrollTo(40, 0);

            Assert.False(viewport.EnsureVisible(new TextPosition(45, 10)));

            Assert.Equal(40, viewport.FirstLine);
            Assert.Equal(0, viewport.FirstColumn);
        }

        [Fact]
        public void RevealingSomethingOffToTheRightScrollsSideways()
        {
            var viewport = new TextViewport(20, 10);

            viewport.EnsureVisible(new TextPosition(0, 25));

            Assert.Equal(6, viewport.FirstColumn);
            Assert.Equal(0, viewport.FirstLine);
        }

        [Fact]
        public void AClickMapsBackToTheDocumentPositionUnderIt()
        {
            var viewport = new TextViewport(20, 10);
            viewport.ScrollTo(100, 7);

            Assert.Equal(new TextPosition(103, 12), viewport.ToDocument(3, 5));
        }

        [Fact]
        public void ScreenAndDocumentCoordinatesAreTheInverseOfEachOther()
        {
            var viewport = new TextViewport(20, 10);
            viewport.ScrollTo(100, 7);

            var document = viewport.ToDocument(4, 6);
            Assert.True(viewport.TryToScreen(document, out var row, out var column));
            Assert.Equal(4, row);
            Assert.Equal(6, column);
        }

        [Fact]
        public void APositionOffScreenHasNoCellAndSaysSoRatherThanGuessing()
        {
            // A renderer that assumes a caret is visible paints the cursor onto whatever row happened to be there.
            var viewport = new TextViewport(20, 10);
            viewport.ScrollTo(100, 0);

            Assert.False(viewport.TryToScreen(new TextPosition(50, 0), out _, out _));
            Assert.False(viewport.TryToScreen(new TextPosition(200, 0), out _, out _));
            Assert.False(viewport.TryToScreen(new TextPosition(105, 40), out _, out _));
        }

        [Fact]
        public void TheCaretStaysVisibleWhileWalkingRightThroughALongLine()
        {
            // The property that matters, asserted over a walk rather than at one position: whatever the caret does,
            // EnsureVisible leaves it somewhere the renderer can draw it.
            var viewport = new TextViewport(20, 10);

            for (var column = 0; column < 200; column++)
            {
                var caret = new TextPosition(0, column);
                viewport.EnsureVisible(caret);

                Assert.True(viewport.TryToScreen(caret, out _, out _),
                    $"the caret at column {column} was left off screen");
            }
        }
    }
}
