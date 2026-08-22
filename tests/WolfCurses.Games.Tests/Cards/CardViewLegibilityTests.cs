using System;
using WolfCurses.Games.Cards;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Games.Tests.Cards
{
    /// <summary>
    ///     Whether a picture of a card table is worth drawing, which the two card games ask before they draw one.
    ///     <para>
    ///         Asked of the arithmetic rather than of <c>CardView.PictureIsLegible</c>, and that is not a
    ///         convenience. The public entry point starts by asking whether the console can show a picture at all,
    ///         and a test host has no console to enable virtual terminal processing on, so it answers false whatever
    ///         the rows and whatever the renderer. A test written against it would pass with the whole pixel
    ///         calculation deleted. The seam exists for exactly the reason the library's own internal seams do.
    ///     </para>
    /// </summary>
    public class CardViewLegibilityTests
    {
        [Fact]
        public void TheDefaultRenderersAnswerWhatTheRowThresholdsUsedTo()
        {
            // The compatibility half, and the reason this could ship without looking at a terminal: 44 rows of half
            // blocks and 10 of a default sixel cell are what this test used to be written as directly, so every
            // renderer anybody is actually running answers exactly what it answered before.
            Assert.False(CardView.HasPixelsForACornerIndex(43, new HalfBlockImageRenderer()));
            Assert.True(CardView.HasPixelsForACornerIndex(44, new HalfBlockImageRenderer()));

            Assert.False(CardView.HasPixelsForACornerIndex(9, new SixelImageRenderer()));
            Assert.True(CardView.HasPixelsForACornerIndex(10, new SixelImageRenderer()));

            Assert.False(CardView.HasPixelsForACornerIndex(9, new KittyImageRenderer()));
            Assert.True(CardView.HasPixelsForACornerIndex(10, new KittyImageRenderer()));
        }

        [Fact]
        public void ARendererWithSmallerCellsNeedsMoreRowsForTheSamePicture()
        {
            // The whole reason for asking instead of assuming. A sixel cell size is a constructor argument, so ten
            // rows is only ever enough at the ten by twenty default: at six by thirteen the same ten rows are 130
            // pixels rather than 200, and the corner index of a card is a stain. A threshold written as a row count
            // cannot tell those two renderers apart, and answers "legible" for both.
            var small = new SixelImageRenderer(6, 13);

            Assert.False(CardView.HasPixelsForACornerIndex(10, small));

            // Sixteen rows is 208 pixels, which is the first row count that buys back what the smaller cell lost.
            Assert.True(CardView.HasPixelsForACornerIndex(16, small));
        }

        [Fact]
        public void ARendererWithLargerCellsEarnsThePictureSooner()
        {
            // The same finding from the other side, so the test cannot pass by the answer merely getting stricter.
            var large = new SixelImageRenderer(20, 40);

            Assert.True(CardView.HasPixelsForACornerIndex(5, large));
            Assert.False(CardView.HasPixelsForACornerIndex(4, large));
        }

        [Fact]
        public void ARendererClaimingNoPixelsAtAllIsNotBelieved()
        {
            // The built-in true-pixel renderers refuse a cell smaller than one pixel in their own constructors, so
            // this can only arrive from a third party - which is precisely the case the seam exists to allow. Left
            // untreated the multiplication collapses to zero, every terminal is refused, and a renderer that draws
            // perfectly good pictures never gets to draw one.
            Assert.True(CardView.HasPixelsForACornerIndex(300, new SilentRenderer()));
        }

        [Fact]
        public void NoRowsIsNeverLegible()
        {
            Assert.False(CardView.HasPixelsForACornerIndex(0, new SixelImageRenderer()));
            Assert.False(CardView.HasPixelsForACornerIndex(-5, new SixelImageRenderer()));
        }

        [Fact]
        public void ARendererIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => CardView.HasPixelsForACornerIndex(40, null));
        }

        /// <summary>
        ///     A third-party renderer that answers nothing it does not have to. Every member of
        ///     <see cref="IImageRenderer" /> except <c>Render</c> is a default interface member, so this is what the
        ///     seam actually permits: a renderer whose cell size is whatever the interface says it is.
        /// </summary>
        private sealed class SilentRenderer : IImageRenderer
        {
            /// <inheritdoc />
            public int CellPixelHeight => 0;

            /// <inheritdoc />
            public string Render(PixelBuffer image, AnsiImageOptions options = null)
            {
                return string.Empty;
            }
        }
    }
}
