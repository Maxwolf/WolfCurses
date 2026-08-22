using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Graphics;
using WolfCurses.Games.Battlezone;
using Xunit;

namespace WolfCurses.Games.Tests.Battlezone
{
    /// <summary>
    ///     The one scene walk both views are drawn from, and the things about it that are decisions rather than
    ///     arithmetic.
    /// </summary>
    public class BattleSceneTests
    {
        private static readonly TimeSpan _frame = TimeSpan.FromMilliseconds(33);

        [Fact]
        public void TheMountainsAreNailedToTheCompassAndNotToThePlayer()
        {
            // Being infinitely far away is the whole of what a horizon is: it swings past as the player turns and
            // does not move an inch as the player drives. Make the ridge depend on position — which is the easy
            // mistake, since everything else in the scene does — and the player can walk to the mountains.
            var field = new BattleField(new Randomizer(21));
            var scene = new BattleScene(new WireCamera(200, 100, 140, 140));

            var before = Collect(scene, field, BattleInkEnum.Horizon);

            for (var i = 0; i < 300; i++)
                field.Advance(_frame, 0, 1);

            Assert.True(field.PlayerZ > 50.0, "the tank did not actually go anywhere");
            Assert.Equal(before, Collect(scene, field, BattleInkEnum.Horizon));
        }

        [Fact]
        public void TurningDoesMoveTheMountains()
        {
            // The other half, or the test above would pass with a ridge that was simply a constant.
            var field = new BattleField(new Randomizer(21));
            var scene = new BattleScene(new WireCamera(200, 100, 140, 140));

            var before = Collect(scene, field, BattleInkEnum.Horizon);

            for (var i = 0; i < 40; i++)
                field.Advance(_frame, 1, 0);

            Assert.NotEqual(before, Collect(scene, field, BattleInkEnum.Horizon));
        }

        [Fact]
        public void TheRidgeIsTheSameWholeWayRoundTheCompass()
        {
            for (var i = 0; i < 40; i++)
            {
                var bearing = i*0.157;
                Assert.Equal(BattleScene.MountainElevation(bearing),
                    BattleScene.MountainElevation(bearing + 2.0*Math.PI), 9);
            }
        }

        [Fact]
        public void TheVolcanoIsTheOnlyThingOnTheHorizonWorthSteeringBy()
        {
            // The ridge repeats and the volcano does not, which is what makes it a landmark rather than scenery.
            var tallest = double.MinValue;
            var whereTallest = 0.0;

            for (var i = 0; i < 2000; i++)
            {
                var bearing = i*2.0*Math.PI/2000.0;
                var elevation = BattleScene.MountainElevation(bearing);

                if (elevation <= tallest)
                    continue;

                tallest = elevation;
                whereTallest = bearing;
            }

            // Well clear of the ordinary ridge, or it would not read as a landmark at a glance.
            var ordinary = BattleScene.MountainElevation(whereTallest + Math.PI);
            Assert.True(tallest > ordinary*2.0, $"the volcano is {tallest:F3} against a ridge of {ordinary:F3}");
        }

        [Fact]
        public void TheRidgeNeverDipsBelowTheHorizon()
        {
            for (var i = 0; i < 2000; i++)
                Assert.True(BattleScene.MountainElevation(i*2.0*Math.PI/2000.0) > 0.0);
        }

        [Fact]
        public void EverythingIsDrawnAndTheGunsightIsAlwaysThere()
        {
            var field = new BattleField(new Randomizer(4));
            var scene = new BattleScene(new WireCamera(200, 100, 140, 140));

            var inks = new HashSet<BattleInkEnum>();
            scene.Draw(field, (_, _2, _3, _4, ink) => inks.Add(ink));

            Assert.Contains(BattleInkEnum.Horizon, inks);
            Assert.Contains(BattleInkEnum.Reticle, inks);
            Assert.Contains(BattleInkEnum.Radar, inks);
            Assert.Contains(BattleInkEnum.Blip, inks);
            Assert.DoesNotContain(BattleInkEnum.Crack, inks);
        }

        [Fact]
        public void TheBrokenScreenIsOnlyDrawnWhenTheScreenIsBroken()
        {
            var field = new BattleField(new Randomizer(2));
            var scene = new BattleScene(new WireCamera(200, 100, 140, 140));

            for (var i = 0; i < 6000 && !field.IsCracked; i++)
                field.Advance(_frame, 0, 0);

            Assert.True(field.IsCracked);

            var lines = 0;
            scene.Draw(field, (_, _2, _3, _4, ink) =>
            {
                if (ink == BattleInkEnum.Crack)
                    lines++;
            });

            Assert.True(lines > 10, $"the break was {lines} lines, which is not broken glass");
        }

        [Fact]
        public void TheBreakStaysNearTheImpactRatherThanCoveringTheWholeScreen()
        {
            // Glass breaks locally. The first version of this radiated spokes long enough to look like damage, which
            // put a line through every corner of the view and hid the game completely behind them.
            var field = new BattleField(new Randomizer(2));
            var camera = new WireCamera(200, 100, 140, 140);
            var scene = new BattleScene(camera);

            for (var i = 0; i < 6000 && !field.IsCracked; i++)
                field.Advance(_frame, 0, 0);

            var touched = new HashSet<int>();
            scene.Draw(field, (x0, _, x1, _2, ink) =>
            {
                if (ink != BattleInkEnum.Crack)
                    return;

                for (var x = Math.Max(0, Math.Min(x0, x1)); x <= Math.Min(camera.Width - 1, Math.Max(x0, x1)); x++)
                    touched.Add(x);
            });

            Assert.True(touched.Count < camera.Width*3/4,
                $"the break spanned {touched.Count} of {camera.Width} columns");
        }

        [Fact]
        public void TheRadarIsACircleInBothViewsRatherThanAnEggInOne()
        {
            // The radar is squashed by the same ratio the projection is, so it comes out round on a pixel buffer and
            // round on a character grid. Measured as the ratio of its drawn width to its drawn height, which should
            // track the focal ratio and not the shape of a cell.
            Assert.InRange(RadarShape(new WireCamera(200, 100, 140, 140)), 0.9, 1.1);
            Assert.InRange(RadarShape(new WireCamera(78, 18, 56, 28)), 0.9, 1.1);
        }

        [Fact]
        public void AShapeOnACharacterGridIsNotSquashedIntoAnEgg()
        {
            // A character cell is about twice as tall as it is wide, so the vertical focal length has to be half the
            // horizontal one. Leave them equal and every position on screen stays exactly right while every SHAPE is
            // wrong - a tank comes out an egg - and the two views disagree about something neither of them reports.
            var text = new BattlezoneText(78, 18);
            var art = new BattlezoneArt(200, 100);

            Assert.Equal(2.0, text.Camera.FocalX/text.Camera.FocalY, 3);
            Assert.Equal(1.0, art.Camera.FocalX/art.Camera.FocalY, 3);
        }

        [Fact]
        public void TheCanvasIsSizedToTheRendererThatWillDrawIt()
        {
            // The strokes are chosen in OUTPUT pixels, so the canvas has to be near the grid the renderer will use
            // or the picture is magnified and every line goes up with it. That is not theoretical: the wireframe
            // shipped fat on a terminal with real pixels and crisp on one without, because a canvas sized for half
            // blocks was being blown up about five times on its way to a sixel terminal.
            var (halfWidth, halfHeight) = BattlezoneArt.SizeFor(198, 44, new HalfBlockImageRenderer());
            var (trueWidth, trueHeight) = BattlezoneArt.SizeFor(198, 44, new SixelImageRenderer());

            Assert.True(trueWidth > halfWidth*1.5, $"real pixels got {trueWidth} against half blocks' {halfWidth}");
            Assert.True(trueHeight > halfHeight*1.5, $"real pixels got {trueHeight} against half blocks' {halfHeight}");

            // And bounded, because every one of those pixels is base64 on its way to the terminal thirty times a
            // second - the cost on the other side of the same knob.
            Assert.InRange(trueWidth*trueHeight, 1, 500_000);
        }

        [Fact]
        public void ACanvasIsSizedFromTheRenderersOwnCellRatherThanFromAGuessAboutIt()
        {
            // The reason the size is asked for rather than written down. A host is free to construct a renderer
            // with the cell size its terminal really has - the library takes it as a constructor argument for
            // exactly that - and the old arithmetic multiplied the columns by a constant chosen against the
            // DEFAULT ten-by-twenty cell. So a smaller cell used to get the same canvas as a larger one, which
            // silently changed the magnification and with it every stroke width on the screen.
            var wide = BattlezoneArt.SizeFor(80, 24, new SixelImageRenderer(10, 20));
            var narrow = BattlezoneArt.SizeFor(80, 24, new SixelImageRenderer(6, 13));

            Assert.True(narrow.Width < wide.Width,
                $"a six-pixel cell asked for {narrow.Width} against a ten-pixel cell's {wide.Width}");
            Assert.True(narrow.Height < wide.Height,
                $"a thirteen-pixel cell asked for {narrow.Height} against a twenty-pixel cell's {wide.Height}");
        }

        [Fact]
        public void TheHalfBlockCanvasIsStillExactlyTwiceWhatWillBeDrawn()
        {
            // The absolute half of the pair above, because "smaller than the other one" is satisfied by any number
            // of wrong answers. Half blocks draw one pixel per column and two per row, and the canvas is a 2x
            // supersample of that - which is the whole reason a two-pixel line survives the average down to a cell.
            // Below the clamp floors, so this is the arithmetic and not the bound.
            // Typed as the interface deliberately: CellPixelWidth and CellPixelHeight are default interface
            // members, so half blocks answer them without declaring them and they are only reachable this way.
            IImageRenderer renderer = new HalfBlockImageRenderer();
            var (width, height) = BattlezoneArt.SizeFor(198, 44, renderer);

            Assert.Equal(198*renderer.CellPixelWidth*2, width);
            Assert.Equal(44*renderer.CellPixelHeight*2, height);
        }

        [Fact]
        public void NothingIsDrawnForATankOverTheHorizon()
        {
            var field = new BattleField(new Randomizer(4));
            var scene = new BattleScene(new WireCamera(200, 100, 140, 140));

            var enemy = field.Enemies[0];
            enemy.X = 0;
            enemy.Z = BattleField.DrawRange + 200.0;

            var lines = 0;
            scene.Draw(field, (_, _2, _3, _4, ink) =>
            {
                if (ink == BattleInkEnum.Enemy)
                    lines++;
            });

            Assert.Equal(0, lines);

            enemy.Z = 120.0;
            scene.Draw(field, (_, _2, _3, _4, ink) =>
            {
                if (ink == BattleInkEnum.Enemy)
                    lines++;
            });

            Assert.True(lines > 0, "a tank a hundred and twenty units away was not drawn either");
        }

        /// <summary>How round the radar comes out: its drawn width over its drawn height, corrected for the cell.</summary>
        /// <param name="camera">The eye to draw through.</param>
        /// <returns>One when it is round.</returns>
        private static double RadarShape(WireCamera camera)
        {
            var scene = new BattleScene(camera);
            var field = new BattleField(new Randomizer(4));

            var left = int.MaxValue;
            var right = int.MinValue;
            var top = int.MaxValue;
            var bottom = int.MinValue;

            scene.Draw(field, (x0, y0, x1, y1, ink) =>
            {
                if (ink != BattleInkEnum.Radar)
                    return;

                left = Math.Min(left, Math.Min(x0, x1));
                right = Math.Max(right, Math.Max(x0, x1));
                top = Math.Min(top, Math.Min(y0, y1));
                bottom = Math.Max(bottom, Math.Max(y0, y1));
            });

            var width = right - left;
            var height = (bottom - top)*camera.FocalX/camera.FocalY;
            return width/height;
        }

        /// <summary>Every segment of one kind that the scene puts out.</summary>
        /// <param name="scene">The scene.</param>
        /// <param name="field">The world.</param>
        /// <param name="ink">Which kind to keep.</param>
        /// <returns>The segments, in the order they were drawn.</returns>
        private static List<(int, int, int, int)> Collect(BattleScene scene, BattleField field, BattleInkEnum ink)
        {
            var kept = new List<(int, int, int, int)>();

            scene.Draw(field, (x0, y0, x1, y1, drawn) =>
            {
                if (drawn == ink)
                    kept.Add((x0, y0, x1, y1));
            });

            return kept;
        }
    }
}
