using System;
using System.IO;
using WolfCurses.Example.Demos;
using Xunit;

namespace WolfCurses.Example.Tests
{
    /// <summary>
    ///     <c>DemoImages</c> decides which of the shared media files each demo sees, and the rule is not obvious:
    ///     <c>media/</c> is a fixture drawer rather than a gallery, so the slideshows take only the numbered
    ///     photographs and every other demo names the one file it wants.
    /// </summary>
    public class DemoImagesTests
    {
        [Fact]
        public void TheSlideshowTakesOnlyTheNumberedPhotographs()
        {
            // The filter exists so the GIFs do not appear as motionless first frames and the logo does not appear
            // at all. Adding a file to media/ deliberately does NOT put it in the slideshow.
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            var slides = DemoImages.SlideshowImages();
            Assert.NotEmpty(slides);

            foreach (var slide in slides)
            {
                var name = Path.GetFileName(slide);
                Assert.StartsWith("image_", name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void TheSlideshowIsOrderedByName()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            var slides = DemoImages.SlideshowImages();
            for (var i = 1; i < slides.Length; i++)
            {
                Assert.True(
                    string.CompareOrdinal(Path.GetFileName(slides[i - 1]).ToUpperInvariant(),
                        Path.GetFileName(slides[i]).ToUpperInvariant()) <= 0,
                    "the slideshow is not in name order, so it would jump about between runs");
            }
        }

        [Fact]
        public void TheNamedFixturesAreWhereTheDemosLookForThem()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            Assert.True(File.Exists(DemoImages.PenguinPath), $"{DemoImages.PenguinPath} is missing");
            Assert.True(File.Exists(DemoImages.AnimatedGifPath), $"{DemoImages.AnimatedGifPath} is missing");
            Assert.True(File.Exists(DemoImages.TruncatablePhotoPath), $"{DemoImages.TruncatablePhotoPath} is missing");
        }

        [Fact]
        public void ThePenguinIsNotInTheSlideshow()
        {
            // It is fetched by name rather than filtered out of the list; narrowing the list instead would silently
            // empty the compositing demo.
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            Assert.DoesNotContain(DemoImages.PenguinPath, DemoImages.SlideshowImages());
        }

        [Fact]
        public void FitOptionsLeaveColourAtAutoSoTheGlobalOverrideReachesThem()
        {
            // Deliberately byte-unchanged at Auto: forcing a colour mode is a library-wide setting, and an options
            // object that stamped its own would make the "Force render type" menu silently not reach the images.
            var options = DemoImages.FitOptions();

            Assert.Equal(WolfCurses.Graphics.AnsiColorModeEnum.Auto, options.ColorMode);
            Assert.True(options.MaxColumns > 0);
            Assert.True(options.MaxRows > 0);
        }
    }
}
