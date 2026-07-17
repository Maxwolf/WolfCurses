// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;
using System.Linq;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins what an image that cannot be loaded turns into: the magenta-and-black checkerboard every developer
    ///     already knows from a game engine with a missing asset, rather than an exception thrown out of a field
    ///     initializer at start-up.
    ///     <para>
    ///         The trade being pinned here is that <see cref="AnsiImage" /> stops throwing while
    ///         <see cref="IImageDecoder" /> keeps throwing — the convenience layer is forgiving, the seam is strict,
    ///         and the reason is preserved either way. Tests on both halves of that live here.
    ///     </para>
    /// </summary>
    public class AnsiImageErrorTextureTests
    {
        [Fact]
        public void FromStream_UndecodableData_IsACheckerboardRatherThanAnException()
        {
            var image = AnsiImage.FromStream(new MemoryStream([1, 2, 3, 4]));

            Assert.True(image.IsError);
            Assert.IsType<InvalidDataException>(image.Error);
        }

        [Fact]
        public void FromStream_UndecodableData_KeepsTheReasonTheDecoderGave()
        {
            // The checkerboard says something is wrong; this is what says what. Losing the decoder's message would
            // trade a good error for a mystery, which is not the trade being made.
            var image = AnsiImage.FromStream(new MemoryStream([1, 2, 3, 4]));

            Assert.Contains("PNG, JPEG or GIF", image.Error.Message, StringComparison.Ordinal);
            Assert.Contains("ImageDecoders.Default", image.Error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void FromFile_MissingFile_IsACheckerboardToo()
        {
            // The most common way anyone gets here is a mistyped asset path, which is exactly the case the convention
            // exists for — and the one where an exception is least useful, since it fires during construction.
            var image = AnsiImage.FromFile(Path.Combine(Path.GetTempPath(), "wolfcurses-no-such-image.png"));

            Assert.True(image.IsError);
            Assert.IsAssignableFrom<IOException>(image.Error);
            Assert.True(image.Width > 0 && image.Height > 0);
        }

        [Fact]
        public void RenderFile_MissingFile_StillReturnsDrawableText()
        {
            // The whole point: the documented usage is a field initializer, so this call failing would take out the
            // type's constructor and surface as a TypeInitializationException naming something unrelated. It has to
            // return something drawable no matter what.
            var ansi = AnsiImage.RenderFile(Path.Combine(Path.GetTempPath(), "wolfcurses-no-such-image.png"),
                new AnsiImageOptions {MaxColumns = 8, MaxRows = 4});

            Assert.False(string.IsNullOrEmpty(ansi));
        }

        [Fact]
        public void FromPixels_IsNeverAnError()
        {
            var image = AnsiImage.FromPixels(new PixelBuffer(2, 2));

            Assert.False(image.IsError);
            Assert.Null(image.Error);
        }

        [Fact]
        public void FromStream_NullStream_StillThrows()
        {
            // A null argument is the caller being wrong, not a picture being wrong. Handing back a checkerboard would
            // hide a bug that has nothing to do with images.
            Assert.Throws<ArgumentNullException>(() => AnsiImage.FromStream(null));
        }

        [Fact]
        public void Decoder_IsUnaffected_AndStillThrows()
        {
            // The seam's contract did not change: anything that wants the exception asks the decoder directly. This
            // is the escape hatch that makes the forgiving default acceptable.
            Assert.Throws<InvalidDataException>(() => ImageDecoders.Default.Decode(new MemoryStream([1, 2, 3, 4])));
        }

        [Fact]
        public void Create_IsMagentaAndBlackAndNothingElse()
        {
            var texture = ImageErrorTexture.Create();

            var colors = new bool[2];
            for (var y = 0; y < texture.Height; y++)
            for (var x = 0; x < texture.Width; x++)
            {
                var pixel = texture.GetPixel(x, y);
                Assert.Equal(255, pixel.A);

                if (pixel is {R: 255, G: 0, B: 255}) colors[0] = true;
                else if (pixel is {R: 0, G: 0, B: 0}) colors[1] = true;
                else Assert.Fail($"Unexpected colour at ({x},{y}): {pixel.R},{pixel.G},{pixel.B}");
            }

            Assert.True(colors[0], "The texture must contain magenta.");
            Assert.True(colors[1], "The texture must contain black.");
        }

        [Fact]
        public void Create_AlternatesInBothDirections()
        {
            var texture = ImageErrorTexture.Create(64, 64, 8);

            // Diagonal neighbours share a colour, orthogonal ones do not. That is what makes it a checkerboard rather
            // than stripes, which is the thing that reads as "error" at a glance.
            Assert.Equal(texture.GetPixel(0, 0).R, texture.GetPixel(8, 8).R);
            Assert.NotEqual(texture.GetPixel(0, 0).R, texture.GetPixel(8, 0).R);
            Assert.NotEqual(texture.GetPixel(0, 0).R, texture.GetPixel(0, 8).R);
        }

        [Fact]
        public void Create_SurvivesBeingScaledDownToTerminalSize()
        {
            // The texture is area-average resampled on its way to the screen, which is exactly how a fine pattern
            // becomes a flat smear. Scaled to a plausible on-screen size it must still be visibly patterned rather
            // than one averaged mauve colour — otherwise the whole convention stops working where it is needed.
            var scaled = ImageErrorTexture.Create().Resize(40, 40);

            var reds = new int[40 * 40];
            for (var y = 0; y < 40; y++)
            for (var x = 0; x < 40; x++)
                reds[y * 40 + x] = scaled.GetPixel(x, y).R;

            Assert.True(reds.Max() > 200, $"Magenta should survive scaling; brightest red was {reds.Max()}.");
            Assert.True(reds.Min() < 55, $"Black should survive scaling; darkest red was {reds.Min()}.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_RejectsAnImpossibleCheckCount(int checks)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ImageErrorTexture.Create(64, 64, checks));
        }
    }
}
