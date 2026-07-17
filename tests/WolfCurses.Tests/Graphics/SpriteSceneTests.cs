// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers <see cref="SpriteScene" /> and <see cref="Sprite" />: a background, things drawn over it, and the
    ///     rule that composing never disturbs either so it can be done again next frame.
    /// </summary>
    public class SpriteSceneTests
    {
        private static readonly Rgba32 _red = new(255, 0, 0, 255);
        private static readonly Rgba32 _blue = new(0, 0, 255, 255);
        private static readonly Rgba32 _green = new(0, 255, 0, 255);
        private static readonly Rgba32 _clear = new(0, 0, 0, 0);

        [Fact]
        public void SceneWithNoSprites_ComposesToItsBackground()
        {
            var scene = new SpriteScene(Solid(4, 4, _red));

            var composed = scene.Compose();

            Assert.Equal(4, composed.Width);
            Assert.Equal(4, composed.Height);
            AssertPixel(composed, 0, 0, _red);
            AssertPixel(composed, 3, 3, _red);
        }

        [Fact]
        public void ASpriteIsDrawnAtItsPosition()
        {
            var scene = new SpriteScene(Solid(8, 8, _red));
            scene.Sprites.Add(new Sprite(Solid(2, 2, _blue), 3, 4));

            var composed = scene.Compose();

            AssertPixel(composed, 3, 4, _blue);
            AssertPixel(composed, 4, 5, _blue);
            AssertPixel(composed, 2, 4, _red); // just outside
            AssertPixel(composed, 5, 4, _red);
        }

        [Fact]
        public void MovingASpriteBetweenFramesLeavesNoTrail()
        {
            // The one that matters, and the reason Compose copies rather than drawing into the background it was given.
            // Compose into the background and the second frame draws the sprite over a background that already has the
            // first frame's sprite in it: nothing looks wrong for one frame, and then the sprite smears across the
            // scene forever. Exactly the trap the GIF decoder's frames have.
            var scene = new SpriteScene(Solid(8, 8, _red));
            var sprite = new Sprite(Solid(2, 2, _blue), 0, 0);
            scene.Sprites.Add(sprite);

            var first = scene.Compose();
            AssertPixel(first, 0, 0, _blue);

            sprite.X = 5;
            var second = scene.Compose();

            AssertPixel(second, 5, 0, _blue); // where it is now
            AssertPixel(second, 0, 0, _red); // where it was: the background, not a ghost
            AssertPixel(first, 0, 0, _blue); // and the frame already handed out is untouched
        }

        [Fact]
        public void ComposingDoesNotDisturbTheBackgroundItWasGiven()
        {
            // The background may be shared between scenes, and is documented as not copied on the way in — which is only
            // safe as long as nothing writes to it.
            var background = Solid(4, 4, _red);
            var scene = new SpriteScene(background);
            scene.Sprites.Add(new Sprite(Solid(4, 4, _blue)));

            scene.Compose();

            AssertPixel(background, 0, 0, _red);
            AssertPixel(background, 3, 3, _red);
        }

        [Fact]
        public void TransparentPartsOfASpriteLetTheBackgroundThrough()
        {
            // What makes a sprite a sprite rather than a rectangle.
            var scene = new SpriteScene(Solid(4, 4, _red));
            var image = Solid(2, 2, _blue);
            image.SetPixel(1, 1, _clear);
            scene.Sprites.Add(new Sprite(image));

            var composed = scene.Compose();

            AssertPixel(composed, 0, 0, _blue);
            AssertPixel(composed, 1, 1, _red);
        }

        [Fact]
        public void SpritesAreDrawnInOrderSoTheLastIsNearest()
        {
            var scene = new SpriteScene(Solid(4, 4, _red));
            scene.Sprites.Add(new Sprite(Solid(2, 2, _blue)));
            scene.Sprites.Add(new Sprite(Solid(2, 2, _green)));

            AssertPixel(scene.Compose(), 0, 0, _green);
        }

        [Fact]
        public void AnInvisibleSpriteIsNotDrawn()
        {
            var scene = new SpriteScene(Solid(4, 4, _red));
            scene.Sprites.Add(new Sprite(Solid(2, 2, _blue)) {Visible = false});

            AssertPixel(scene.Compose(), 0, 0, _red);
        }

        [Fact]
        public void ASpriteHangingOffTheEdgeIsClippedRatherThanRefused()
        {
            // Documented behaviour: a sprite can be walked in from off-screen without the caller doing arithmetic to
            // hide the fact, so every edge and both signs have to survive being composed.
            var scene = new SpriteScene(Solid(4, 4, _red));
            var sprite = new Sprite(Solid(2, 2, _blue), -1, -1);
            scene.Sprites.Add(sprite);

            AssertPixel(scene.Compose(), 0, 0, _blue); // the one corner still inside

            sprite.X = 3;
            sprite.Y = 3;
            AssertPixel(scene.Compose(), 3, 3, _blue);

            // Entirely outside, in each direction: nothing drawn, nothing thrown.
            foreach (var (x, y) in new[] {(-5, 0), (0, -5), (9, 0), (0, 9)})
            {
                sprite.X = x;
                sprite.Y = y;
                var composed = scene.Compose();
                AssertPixel(composed, 0, 0, _red);
                AssertPixel(composed, 3, 3, _red);
            }
        }

        [Fact]
        public void ASceneBuiltFromSizeAloneIsTransparent()
        {
            // For sprites over nothing, where a terminal shows its own background through the gaps.
            var scene = new SpriteScene(6, 3);

            Assert.Equal(6, scene.Width);
            Assert.Equal(3, scene.Height);
            Assert.Equal(0, scene.Compose().GetPixel(0, 0).A);
        }

        [Fact]
        public void SpriteReportsTheSizeOfItsCurrentPicture()
        {
            // Settable, because an animated sprite is one whose picture is swapped as time passes — and its size can
            // change with it, which is what anything bouncing it off a wall has to ask.
            var sprite = new Sprite(Solid(2, 5, _blue));
            Assert.Equal(2, sprite.Width);
            Assert.Equal(5, sprite.Height);

            sprite.Image = Solid(7, 3, _blue);
            Assert.Equal(7, sprite.Width);
            Assert.Equal(3, sprite.Height);
        }

        [Fact]
        public void NothingAcceptsANullPicture()
        {
            Assert.Throws<ArgumentNullException>(() => new SpriteScene(null));
            Assert.Throws<ArgumentNullException>(() => new Sprite(null));
            Assert.Throws<ArgumentNullException>(() => new Sprite(Solid(1, 1, _red)).Image = null);
        }

        [Fact]
        public void ToAnsi_RendersTheComposedSceneRatherThanTheBareBackground()
        {
            // The convenience path is the one callers will actually use, so it is worth proving it composes at all
            // rather than quietly rendering the background it was constructed with.
            var scene = new SpriteScene(Solid(8, 8, _red));
            var bare = scene.ToAnsi(new AnsiImageOptions {MaxColumns = 8, MaxRows = 4});

            scene.Sprites.Add(new Sprite(Solid(8, 8, _blue)));
            var covered = scene.ToAnsi(new AnsiImageOptions {MaxColumns = 8, MaxRows = 4});

            Assert.NotEqual(bare, covered);
        }

        private static PixelBuffer Solid(int width, int height, Rgba32 color)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, color);

            return buffer;
        }

        private static void AssertPixel(PixelBuffer image, int x, int y, Rgba32 expected)
        {
            var actual = image.GetPixel(x, y);
            Assert.True(
                expected.R == actual.R && expected.G == actual.G && expected.B == actual.B && expected.A == actual.A,
                $"({x},{y}): expected {expected.R},{expected.G},{expected.B},{expected.A} " +
                $"got {actual.R},{actual.G},{actual.B},{actual.A}");
        }
    }
}
