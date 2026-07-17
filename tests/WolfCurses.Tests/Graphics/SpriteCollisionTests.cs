// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Linq;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers <see cref="Sprite.Intersects" /> and <see cref="SpriteScene.SpritesTouching" />: whether two
    ///     rectangles overlap, and which sprites on a scene are overlapping a given one.
    /// </summary>
    public class SpriteCollisionTests
    {
        private static readonly Rgba32 _blue = new(0, 0, 255, 255);

        [Theory]
        // Overlapping by a single pixel on each axis: the smallest true there is.
        [InlineData(0, 0, 3, 3, 2, 2, 3, 3, true)]
        // Squarely on top of one another.
        [InlineData(0, 0, 4, 4, 1, 1, 2, 2, true)]
        // Side by side, one ending exactly where the next begins. Adjacent is not overlapping.
        [InlineData(0, 0, 2, 2, 2, 0, 2, 2, false)]
        // The same, stacked.
        [InlineData(0, 0, 2, 2, 0, 2, 2, 2, false)]
        // A pixel of clear air between them.
        [InlineData(0, 0, 2, 2, 3, 0, 2, 2, false)]
        // Overlapping horizontally but nowhere near vertically: both axes have to agree.
        [InlineData(0, 0, 4, 2, 1, 9, 4, 2, false)]
        // And the other way round.
        [InlineData(0, 0, 2, 4, 9, 1, 2, 4, false)]
        // Negative coordinates work like any other, since a sprite may sit off the edge of its scene.
        [InlineData(-3, -3, 4, 4, 0, 0, 2, 2, true)]
        [InlineData(-5, 0, 4, 4, 0, 0, 2, 2, false)]
        public void Intersects_IsTrueOnlyWhenTheBoxesShareAPixel(int ax, int ay, int aw, int ah, int bx, int by,
            int bw, int bh, bool expected)
        {
            var a = new Sprite(Solid(aw, ah), ax, ay);
            var b = new Sprite(Solid(bw, bh), bx, by);

            Assert.Equal(expected, a.Intersects(b));

            // Touching is mutual; a test that only worked one way round would be worse than none.
            Assert.Equal(expected, b.Intersects(a));
        }

        [Fact]
        public void Intersects_IsGeometryAndIgnoresVisibility()
        {
            // Documented split: whether a thing is drawn and whether it is there are different questions, and only the
            // caller knows which it is asking. SpritesTouching is the one that takes a view.
            var a = new Sprite(Solid(4, 4)) {Visible = false};
            var b = new Sprite(Solid(4, 4)) {Visible = false};

            Assert.True(a.Intersects(b));
        }

        [Fact]
        public void ASpriteOverlapsItself()
        {
            // Not useful, but it falls out of the definition, and a caller comparing everything against everything will
            // reach it. Better asserted than discovered.
            var sprite = new Sprite(Solid(2, 2));

            Assert.True(sprite.Intersects(sprite));
        }

        [Fact]
        public void SpritesTouching_NamesWhatWasHitRatherThanMerelyThatSomethingWas()
        {
            // The whole point of the scene-level query: with several sprites about, the answer a game needs is which
            // one, not whether.
            var scene = new SpriteScene(16, 16);
            var player = new Sprite(Solid(4, 4), 0, 0);
            var near = new Sprite(Solid(4, 4), 2, 2);
            var far = new Sprite(Solid(4, 4), 11, 11);

            scene.Sprites.Add(near);
            scene.Sprites.Add(far);
            scene.Sprites.Add(player);

            var hit = scene.SpritesTouching(player).ToList();

            Assert.Single(hit);
            Assert.Same(near, hit[0]);
        }

        [Fact]
        public void SpritesTouching_DoesNotReportTheSpriteAskingTheQuestion()
        {
            // It overlaps itself perfectly, so without this every sprite on every scene is always touching something.
            var scene = new SpriteScene(8, 8);
            var player = new Sprite(Solid(4, 4));
            scene.Sprites.Add(player);

            Assert.Empty(scene.SpritesTouching(player));
        }

        [Fact]
        public void SpritesTouching_SkipsInvisibleSprites()
        {
            // Visible is documented as the gentler way of taking a sprite off the scene, and something that is not on
            // the scene cannot be bumped into.
            var scene = new SpriteScene(8, 8);
            var ghost = new Sprite(Solid(4, 4)) {Visible = false};
            var player = new Sprite(Solid(4, 4));

            scene.Sprites.Add(ghost);
            scene.Sprites.Add(player);

            Assert.Empty(scene.SpritesTouching(player));

            ghost.Visible = true;
            Assert.Same(ghost, scene.SpritesTouching(player).Single());
        }

        [Fact]
        public void SpritesTouching_AnswersForASpriteThatIsNotOnTheScene()
        {
            // Documented as useful, and it is: it lets a rectangle be tried somewhere before anything is moved there.
            var scene = new SpriteScene(8, 8);
            var wall = new Sprite(Solid(4, 4), 4, 0);
            scene.Sprites.Add(wall);

            var proposed = new Sprite(Solid(4, 4), 3, 0);

            Assert.Same(wall, scene.SpritesTouching(proposed).Single());
        }

        [Fact]
        public void SpritesTouching_ReportsEveryoneInDrawingOrder()
        {
            var scene = new SpriteScene(8, 8);
            var first = new Sprite(Solid(8, 8));
            var second = new Sprite(Solid(8, 8));
            var player = new Sprite(Solid(2, 2), 3, 3);

            scene.Sprites.Add(first);
            scene.Sprites.Add(second);
            scene.Sprites.Add(player);

            Assert.Equal(new[] {first, second}, scene.SpritesTouching(player).ToArray());
        }

        [Fact]
        public void TheEdgeTriggerACallerHasToBuildIsAnHonestOne()
        {
            // Not a library feature — the demo has to do this itself — but it is the shape every caller needs and the
            // place collision code goes wrong first, so it is worth one test that the primitive supports it. Touching is
            // true for as long as the boxes overlap, so a caller that acts on the level rather than the change acts
            // hundreds of times.
            var scene = new SpriteScene(32, 8);
            var wall = new Sprite(Solid(4, 4), 12, 0);
            var player = new Sprite(Solid(4, 4), 0, 0);
            scene.Sprites.Add(wall);
            scene.Sprites.Add(player);

            var fired = 0;
            var wasTouching = false;

            void Step(int dx)
            {
                player.X += dx;
                var touching = scene.SpritesTouching(player).Any();
                if (touching && !wasTouching)
                    fired++;

                wasTouching = touching;
            }

            // Walk in and stay a while: one trigger, however long it lingers.
            for (var i = 0; i < 6; i++) Step(2);
            Assert.True(wasTouching);
            Assert.Equal(1, fired);

            for (var i = 0; i < 3; i++) Step(0);
            Assert.Equal(1, fired);

            // Back off and come again: a second.
            for (var i = 0; i < 6; i++) Step(-2);
            Assert.False(wasTouching);
            Assert.Equal(1, fired);

            for (var i = 0; i < 6; i++) Step(2);
            Assert.Equal(2, fired);
        }

        [Fact]
        public void NothingAcceptsANullSprite()
        {
            var scene = new SpriteScene(4, 4);

            Assert.Throws<ArgumentNullException>(() => new Sprite(Solid(2, 2)).Intersects(null));
            Assert.Throws<ArgumentNullException>(() => scene.SpritesTouching(null).ToList());
        }

        private static PixelBuffer Solid(int width, int height)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, _blue);

            return buffer;
        }
    }
}
