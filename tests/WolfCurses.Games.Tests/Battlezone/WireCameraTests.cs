using System;
using System.Collections.Generic;
using WolfCurses.Games.Battlezone;
using Xunit;

namespace WolfCurses.Games.Tests.Battlezone
{
    /// <summary>
    ///     The projection: the only three-dimensional thing in the game, and the only place a first-person renderer
    ///     can go spectacularly wrong.
    /// </summary>
    public class WireCameraTests
    {
        [Fact]
        public void SomethingStraightAheadIsInTheMiddleOfTheScreen()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            Assert.True(camera.TryProject(0, 6, 100, out var column, out _));
            Assert.Equal((int) Math.Round(camera.CenterX, MidpointRounding.AwayFromZero), column);
        }

        [Fact]
        public void SomethingToTheRightIsDrawnToTheRight()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            Assert.True(camera.TryProject(40, 6, 100, out var right, out _));
            Assert.True(camera.TryProject(-40, 6, 100, out var left, out _));

            Assert.True(right > camera.CenterX, "east of the eye did not draw to the right");
            Assert.True(left < camera.CenterX, "west of the eye did not draw to the left");
        }

        [Fact]
        public void TurningRightSwingsTheWorldLeft()
        {
            var camera = Build();

            camera.SetView(0, 0, 0, 6);
            camera.TryProject(0, 6, 100, out var ahead, out _);

            camera.SetView(0, 0, 0.3, 6);
            camera.TryProject(0, 6, 100, out var afterTurning, out _);

            Assert.True(afterTurning < ahead, "turning right did not move the scene left");
        }

        [Fact]
        public void ThingsAboveTheEyeAreDrawnAboveTheHorizon()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            Assert.True(camera.TryProject(0, 30, 100, out _, out var high));
            Assert.True(camera.TryProject(0, 0, 100, out _, out var low));

            Assert.True(high < camera.HorizonRow, "a point above the eye was not drawn above the horizon");
            Assert.True(low > camera.HorizonRow, "the ground was not drawn below the horizon");
        }

        [Fact]
        public void TheFurtherAwayAThingIsTheNearerTheHorizonItSits()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            var previous = double.MaxValue;
            foreach (var range in new[] {20, 50, 120, 300, 900})
            {
                Assert.True(camera.TryProject(0, 0, range, out _, out var row));
                var below = row - camera.HorizonRow;

                Assert.True(below < previous, $"the ground at {range} was not nearer the horizon than the step before");
                Assert.True(below > 0, "the ground rose above the horizon");
                previous = below;
            }
        }

        [Fact]
        public void TheHorizonSitsInTheMiddleWhateverHeightTheEyeIsAt()
        {
            // Ground level infinitely far away projects to the same row whatever the eye height, since the height
            // above the eye is a constant and the depth is not. That is why the horizon needs no special case, and
            // it is worth pinning because a "fix" that offsets it by eye height looks perfectly reasonable.
            var camera = Build();

            camera.SetView(0, 0, 0, 6);
            var low = camera.HorizonRow;

            camera.SetView(0, 0, 0, 200);
            Assert.Equal(low, camera.HorizonRow);

            camera.TryProject(0, 200, 1_000_000, out _, out var faraway);
            Assert.InRange(faraway, (int) low - 1, (int) low + 1);
        }

        [Fact]
        public void NothingBehindTheEyeIsProjectedAtAll()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            Assert.False(camera.TryProject(0, 6, -100, out _, out _));
            Assert.False(camera.TryProject(0, 6, 0, out _, out _));
        }

        [Fact]
        public void AnEdgeWithOneEndBehindTheEyeStaysOnItsOwnSideOfTheScreen()
        {
            // THE bug this clip exists to prevent, and the reason it cannot be skipped: a vertex behind the eye has a
            // negative depth, so dividing by it flips the sign of both screen coordinates and lands the point on the
            // OPPOSITE side of the screen. The edge then draws as a line slashing right across the view - which
            // reads as the renderer having a fit rather than as a missing clip, and only happens when something gets
            // close, which is to say only in the moments that matter.
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            // A post standing well to the left, running from behind the eye to well in front of it.
            var post = new WireModel(
                new[] {new System.Numerics.Vector3(0f, 0f, -1f), new System.Numerics.Vector3(0f, 0f, 1f)},
                new[] {0, 1});

            var drawn = new List<int>();
            camera.DrawModel(post, -20, 0, 6, 0, 10, (x0, _, x1, _2) =>
            {
                drawn.Add(x0);
                drawn.Add(x1);
            });

            Assert.NotEmpty(drawn);
            foreach (var column in drawn)
                Assert.True(column < camera.CenterX, $"a point to the left of the eye was drawn at column {column}");
        }

        [Fact]
        public void AnEdgeEntirelyBehindTheEyeIsNotDrawn()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            var post = new WireModel(
                new[] {new System.Numerics.Vector3(0f, 0f, -1f), new System.Numerics.Vector3(0f, 0f, 1f)},
                new[] {0, 1});

            var lines = 0;
            camera.DrawModel(post, 0, -100, 6, 0, 10, (_, _2, _3, _4) => lines++);

            Assert.Equal(0, lines);
        }

        [Fact]
        public void ClippingMovesTheEndToTheNearPlaneAndLeavesTheRestAlone()
        {
            var kept = WireCamera.TryClipToNear(0, 0, -10, 0, 20, 10,
                out var ax, out var ay, out var az, out var bx, out var by, out var bz);

            Assert.True(kept);
            Assert.Equal(WireCamera.NearPlane, az, 6);
            Assert.Equal(10.0, bz, 6);
            Assert.Equal(20.0, by, 6);
            Assert.Equal(0.0, bx, 6);

            // Halfway along in depth means halfway along in everything else, which is what makes the cut invisible.
            var t = (WireCamera.NearPlane + 10.0)/20.0;
            Assert.Equal(20.0*t, ay, 6);
            Assert.Equal(0.0, ax, 6);
        }

        [Fact]
        public void ClippingIsTheSameWhicheverEndIsGivenFirst()
        {
            WireCamera.TryClipToNear(3, 4, -8, 9, 12, 30, out var ax, out var ay, out var az, out var bx,
                out var by, out var bz);
            WireCamera.TryClipToNear(9, 12, 30, 3, 4, -8, out var rx, out var ry, out var rz, out var sx,
                out var sy, out var sz);

            Assert.Equal(ax, sx, 6);
            Assert.Equal(ay, sy, 6);
            Assert.Equal(az, sz, 6);
            Assert.Equal(bx, rx, 6);
            Assert.Equal(by, ry, 6);
            Assert.Equal(bz, rz, 6);
        }

        [Fact]
        public void AnEdgeWithBothEndsInFrontIsPassedThroughUntouched()
        {
            var kept = WireCamera.TryClipToNear(1, 2, 3, 4, 5, 6, out var ax, out var ay, out var az,
                out var bx, out var by, out var bz);

            Assert.True(kept);
            Assert.Equal((1.0, 2.0, 3.0), (ax, ay, az));
            Assert.Equal((4.0, 5.0, 6.0), (bx, by, bz));
        }

        [Fact]
        public void TheRadarAndTheViewAgreeAboutWhichWayIsAhead()
        {
            // The radar is the same rotation without the perspective divide, so it shares one. If they ever came
            // apart the blips would be subtly wrong in a way nobody could point at.
            var camera = Build();
            camera.SetView(30, -12, 0.8, 6);

            foreach (var (x, z) in new[] {(90.0, 40.0), (-50.0, 200.0), (10.0, -60.0), (0.0, 0.0)})
            {
                camera.ToGround(x, z, out var right, out var forward);

                if (forward < WireCamera.NearPlane)
                {
                    Assert.False(camera.TryProject(x, 6, z, out _, out _));
                    continue;
                }

                Assert.True(camera.TryProject(x, 6, z, out var column, out _));
                Assert.Equal(Math.Sign(right), Math.Sign(Math.Round(column - camera.CenterX, 6)));
            }
        }

        [Fact]
        public void AColumnKnowsWhichWayItIsLooking()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            Assert.Equal(0.0, camera.BearingAtColumn(camera.CenterX), 6);
            Assert.True(camera.BearingAtColumn(camera.Width - 1) > 0);
            Assert.True(camera.BearingAtColumn(0) < 0);
        }

        [Fact]
        public void ABiggerModelDrawsWider()
        {
            var camera = Build();
            camera.SetView(0, 0, 0, 6);

            Assert.True(Spread(camera, 20) > Spread(camera, 5), "scale made no difference to the drawn width");
        }

        private static int Spread(WireCamera camera, double scale)
        {
            var low = int.MaxValue;
            var high = int.MinValue;

            camera.DrawModel(WireModel.Cube, 0, 120, 0, 0, scale, (x0, _, x1, _2) =>
            {
                low = Math.Min(low, Math.Min(x0, x1));
                high = Math.Max(high, Math.Max(x0, x1));
            });

            return high - low;
        }

        private static WireCamera Build()
        {
            return new WireCamera(200, 100, 140, 140);
        }
    }
}
