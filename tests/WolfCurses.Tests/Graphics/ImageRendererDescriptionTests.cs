using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     A renderer describes itself, so an application deciding something about layout does not have to type-test
    ///     the built-in classes — which is what both example apps were doing, and which gets the wrong answer for
    ///     exactly the third-party renderers the <see cref="IImageRenderer" /> seam exists to allow.
    /// </summary>
    public class ImageRendererDescriptionTests
    {
        /// <summary>A renderer from outside the library, taking the interface's defaults for both members.</summary>
        private sealed class BareRenderer : IImageRenderer
        {
            public string Render(PixelBuffer image, AnsiImageOptions options = null) => string.Empty;
        }

        /// <summary>One that draws real pixels and says so, which is the case type-testing could never get right.</summary>
        private sealed class ThirdPartyPixelRenderer : IImageRenderer
        {
            public string Name => "acme pixels";

            public bool DrawsTruePixels => true;

            public string Render(PixelBuffer image, AnsiImageOptions options = null) => string.Empty;
        }

        [Fact]
        public void TheBuiltInRenderersSayWhatTheyAre()
        {
            Assert.Equal("half blocks", new HalfBlockImageRenderer().Name);
            Assert.Equal("sixel", new SixelImageRenderer().Name);
            Assert.Equal("kitty", new KittyImageRenderer().Name);
        }

        [Fact]
        public void OnlyTheTruePixelRenderersClaimRealPixels()
        {
            // The distinction an application uses to decide whether a picture is worth showing at all: half blocks
            // get two pixels per row, so a small board is a smudge where the same rows of sixel are a picture.
            Assert.False(new HalfBlockImageRenderer().DrawsTruePixels);
            Assert.True(new SixelImageRenderer().DrawsTruePixels);
            Assert.True(new KittyImageRenderer().DrawsTruePixels);
        }

        [Fact]
        public void AnImplementationThatSaysNothingGetsSafeDefaults()
        {
            // Both are default interface members, so every renderer written before they existed still compiles and
            // still answers. False is the safe half: guessing "no real pixels" costs a plainer picture, guessing
            // the other way costs a screen of escape garbage.
            IImageRenderer bare = new BareRenderer();

            Assert.False(bare.DrawsTruePixels);
            Assert.Equal(nameof(BareRenderer), bare.Name);
        }

        [Fact]
        public void AThirdPartyRendererCanClaimRealPixels_WhichTypeTestingCouldNot()
        {
            // The reason these members exist. `renderer is SixelImageRenderer or KittyImageRenderer` is false here,
            // and it is the wrong answer.
            IImageRenderer acme = new ThirdPartyPixelRenderer();

            Assert.True(acme.DrawsTruePixels);
            Assert.Equal("acme pixels", acme.Name);
            Assert.False(acme is SixelImageRenderer or KittyImageRenderer);
        }
    }
}
