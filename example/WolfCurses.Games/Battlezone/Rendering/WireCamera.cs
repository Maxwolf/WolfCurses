// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     Turns the world into lines on a screen: the whole of the three-dimensional part of this game, in one
    ///     class that knows nothing about tanks.
    ///     <para>
    ///         <b>There is no matrix here and there does not need to be one.</b> A Battlezone camera sits at a fixed
    ///         height on a flat plain and never pitches, rolls or leans, so the view transform is a two-dimensional
    ///         rotation of the ground plane and a subtraction of the eye height — after which a perspective divide
    ///         does the rest. A four-by-four pipeline would spend most of its multiplies on zeroes and ones to
    ///         arrive at the same six lines. Write the general thing when there is a general problem.
    ///     </para>
    ///     <para>
    ///         <b>The near plane is the one clip that cannot be skipped, and skipping it does not look like a
    ///         missing feature.</b> A vertex behind the eye has a negative depth, so dividing by it flips the sign
    ///         of both screen coordinates and puts the point on the <i>opposite</i> side of the screen from where it
    ///         is. An edge with one end behind you therefore draws as a line slashing right across the view, which
    ///         reads as the renderer having a fit rather than as a missing clip — and it only happens when
    ///         something gets close, which is to say only in the moments that matter. So every edge is cut at
    ///         <see cref="NearPlane" /> first: both ends behind means nothing is drawn, one end behind means the end
    ///         is moved forward along the edge to where it crosses.
    ///     </para>
    ///     <para>
    ///         <b><see cref="FocalX" /> and <see cref="FocalY" /> are separate, and that is not a detail.</b> On a
    ///         pixel buffer they are equal, because the buffer's pixels are square once half blocks have paired
    ///         them up. On a character grid a cell is about twice as tall as it is wide, so the vertical focal
    ///         length has to be half the horizontal one or every tank comes out an egg — and the same scene drawn
    ///         both ways would disagree about a shape while agreeing about every position, which is a maddening
    ///         thing to debug from a screenshot.
    ///     </para>
    /// </summary>
    public sealed class WireCamera
    {
        /// <summary>How close something may get before it is cut away, in world units.</summary>
        public const double NearPlane = 1.2;

        /// <summary>
        ///     How far off the screen a projected point is allowed to land before it is pinned.
        ///     <para>
        ///         A vertex a hair in front of the near plane projects enormously far out, and a <c>double</c> that
        ///         big cast to <c>int</c> does not wrap — it becomes <see cref="int.MinValue" />, which turns a line
        ///         heading off the right of the screen into one heading off the left. The line drawers clip their
        ///         own loop ranges, so a coordinate a million out costs nothing to hand them.
        ///     </para>
        /// </summary>
        private const double CoordinateLimit = 1_000_000.0;

        private double[] _viewX = new double[32];
        private double[] _viewY = new double[32];
        private double[] _viewZ = new double[32];

        /// <summary>Initializes a new instance of the <see cref="WireCamera" /> class.</summary>
        /// <param name="width">How many columns of the target the view covers.</param>
        /// <param name="height">How many rows of the target the view covers.</param>
        /// <param name="focalX">Horizontal focal length, in the target's own units.</param>
        /// <param name="focalY">Vertical focal length, in the target's own units.</param>
        public WireCamera(int width, int height, double focalX, double focalY)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            FocalX = focalX;
            FocalY = focalY;
        }

        /// <summary>How many columns of the target the view covers.</summary>
        public int Width { get; }

        /// <summary>How many rows of the target the view covers.</summary>
        public int Height { get; }

        /// <summary>Horizontal focal length, in the target's own units.</summary>
        public double FocalX { get; }

        /// <summary>Vertical focal length, in the target's own units.</summary>
        public double FocalY { get; }

        /// <summary>Where the eye is, east.</summary>
        public double X { get; private set; }

        /// <summary>Where the eye is, north.</summary>
        public double Z { get; private set; }

        /// <summary>Which way it looks, in radians clockwise from north.</summary>
        public double Heading { get; private set; }

        /// <summary>How far off the ground it sits.</summary>
        public double EyeHeight { get; private set; } = 6.0;

        /// <summary>The middle column.</summary>
        public double CenterX => (Width - 1)/2.0;

        /// <summary>
        ///     The middle row, which is also where the horizon is.
        ///     <para>
        ///         Ground level infinitely far away projects to exactly here whatever the eye height, since the
        ///         height above the eye is a constant and the depth is not — so the horizon needs no special case
        ///         and cannot drift out of step with the scene.
        ///     </para>
        /// </summary>
        public double HorizonRow => (Height - 1)/2.0;

        /// <summary>Points the camera.</summary>
        /// <param name="x">Where the eye is, east.</param>
        /// <param name="z">Where the eye is, north.</param>
        /// <param name="heading">Which way it looks, in radians clockwise from north.</param>
        /// <param name="eyeHeight">How far off the ground it sits.</param>
        public void SetView(double x, double z, double heading, double eyeHeight)
        {
            X = x;
            Z = z;
            Heading = heading;
            EyeHeight = eyeHeight;
        }

        /// <summary>
        ///     Which way a column of the screen is looking, relative to straight ahead. The inverse of the
        ///     projection, used by anything drawn from the horizon outward rather than from the world inward.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <returns>The bearing in radians, negative to the left.</returns>
        public double BearingAtColumn(double column)
        {
            return Math.Atan((column - CenterX)/FocalX);
        }

        /// <summary>Projects one world point, if it is in front of the eye.</summary>
        /// <param name="worldX">Where it is, east.</param>
        /// <param name="worldY">How far off the ground.</param>
        /// <param name="worldZ">Where it is, north.</param>
        /// <param name="column">Where it lands, across.</param>
        /// <param name="row">Where it lands, down.</param>
        /// <returns>True when it is in front of the near plane.</returns>
        public bool TryProject(double worldX, double worldY, double worldZ, out int column, out int row)
        {
            ToView(worldX, worldY, worldZ, out var vx, out var vy, out var vz);

            if (vz < NearPlane)
            {
                column = 0;
                row = 0;
                return false;
            }

            Project(vx, vy, vz, out column, out row);
            return true;
        }

        /// <summary>
        ///     Draws a model standing somewhere on the plain, facing somewhere, at some size.
        /// </summary>
        /// <param name="model">The shape.</param>
        /// <param name="worldX">Where it stands, east.</param>
        /// <param name="worldZ">Where it stands, north.</param>
        /// <param name="lift">How far off the ground it floats.</param>
        /// <param name="heading">Which way it faces, in radians clockwise from north.</param>
        /// <param name="scale">How big it is, in world units per model unit.</param>
        /// <param name="sink">Where the finished screen-space segments go.</param>
        public void DrawModel(WireModel model, double worldX, double worldZ, double lift, double heading,
            double scale, Action<int, int, int, int> sink)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            var vertices = model.Vertices;
            if (_viewX.Length < vertices.Length)
            {
                _viewX = new double[vertices.Length];
                _viewY = new double[vertices.Length];
                _viewZ = new double[vertices.Length];
            }

            // Transformed once per corner rather than once per edge. A cube has twelve edges over eight corners, so
            // the loop below would otherwise do the work three times for every one of them.
            var modelCos = Math.Cos(heading);
            var modelSin = Math.Sin(heading);

            for (var i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                var wx = worldX + (v.X*modelCos + v.Z*modelSin)*scale;
                var wz = worldZ + (v.Z*modelCos - v.X*modelSin)*scale;
                ToView(wx, v.Y*scale + lift, wz, out _viewX[i], out _viewY[i], out _viewZ[i]);
            }

            var edges = model.Edges;
            for (var e = 0; e + 1 < edges.Length; e += 2)
            {
                var a = edges[e];
                var b = edges[e + 1];

                if (TryClipToNear(_viewX[a], _viewY[a], _viewZ[a], _viewX[b], _viewY[b], _viewZ[b],
                        out var ax, out var ay, out var az, out var bx, out var by, out var bz))
                {
                    Project(ax, ay, az, out var x0, out var y0);
                    Project(bx, by, bz, out var x1, out var y1);
                    sink(x0, y0, x1, y1);
                }
            }
        }

        /// <summary>
        ///     Cuts an edge at the near plane, so nothing behind the eye is ever divided by.
        /// </summary>
        /// <param name="ax">One end, sideways in view space.</param>
        /// <param name="ay">One end, up in view space.</param>
        /// <param name="az">One end, forward in view space.</param>
        /// <param name="bx">The other end, sideways.</param>
        /// <param name="by">The other end, up.</param>
        /// <param name="bz">The other end, forward.</param>
        /// <param name="outAx">The kept end, sideways.</param>
        /// <param name="outAy">The kept end, up.</param>
        /// <param name="outAz">The kept end, forward.</param>
        /// <param name="outBx">The other kept end, sideways.</param>
        /// <param name="outBy">The other kept end, up.</param>
        /// <param name="outBz">The other kept end, forward.</param>
        /// <returns>False when the whole edge is behind the near plane and nothing should be drawn.</returns>
        public static bool TryClipToNear(double ax, double ay, double az, double bx, double by, double bz,
            out double outAx, out double outAy, out double outAz,
            out double outBx, out double outBy, out double outBz)
        {
            outAx = ax;
            outAy = ay;
            outAz = az;
            outBx = bx;
            outBy = by;
            outBz = bz;

            var aBehind = az < NearPlane;
            var bBehind = bz < NearPlane;

            if (aBehind && bBehind)
                return false;

            if (!aBehind && !bBehind)
                return true;

            // Exactly one end is behind, so the edge crosses the plane exactly once and the crossing can be found
            // by interpolating. The divisor cannot be zero: one depth is below the plane and the other is not.
            if (aBehind)
            {
                var t = (NearPlane - az)/(bz - az);
                outAx = ax + (bx - ax)*t;
                outAy = ay + (by - ay)*t;
                outAz = NearPlane;
            }
            else
            {
                var t = (NearPlane - bz)/(az - bz);
                outBx = bx + (ax - bx)*t;
                outBy = by + (ay - by)*t;
                outBz = NearPlane;
            }

            return true;
        }

        /// <summary>
        ///     Rotates a point on the ground into the eye's own frame: how far to the right of it, and how far in
        ///     front of it.
        ///     <para>
        ///         Public because the radar needs exactly this and nothing else — a radar is the same rotation
        ///         without the perspective divide. Sharing it is what stops the map and the view ever disagreeing
        ///         about which way is ahead, which is a bug that would show as the blips being subtly wrong in a way
        ///         nobody could point at.
        ///     </para>
        /// </summary>
        /// <param name="worldX">Where it is, east.</param>
        /// <param name="worldZ">Where it is, north.</param>
        /// <param name="right">How far to the right of the eye.</param>
        /// <param name="forward">How far in front of the eye.</param>
        public void ToGround(double worldX, double worldZ, out double right, out double forward)
        {
            var dx = worldX - X;
            var dz = worldZ - Z;
            var sin = Math.Sin(Heading);
            var cos = Math.Cos(Heading);

            right = dx*cos - dz*sin;
            forward = dx*sin + dz*cos;
        }

        /// <summary>Moves a world point into view space: sideways, up, and forward from the eye.</summary>
        /// <param name="worldX">Where it is, east.</param>
        /// <param name="worldY">How far off the ground.</param>
        /// <param name="worldZ">Where it is, north.</param>
        /// <param name="viewX">How far to the right of the eye.</param>
        /// <param name="viewY">How far above the eye.</param>
        /// <param name="viewZ">How far in front of the eye.</param>
        private void ToView(double worldX, double worldY, double worldZ, out double viewX, out double viewY,
            out double viewZ)
        {
            ToGround(worldX, worldZ, out viewX, out viewZ);
            viewY = worldY - EyeHeight;
        }

        /// <summary>Divides a view-space point through by its depth.</summary>
        /// <param name="viewX">How far to the right of the eye.</param>
        /// <param name="viewY">How far above the eye.</param>
        /// <param name="viewZ">How far in front of the eye; never below the near plane.</param>
        /// <param name="column">Where it lands, across.</param>
        /// <param name="row">Where it lands, down.</param>
        private void Project(double viewX, double viewY, double viewZ, out int column, out int row)
        {
            var depth = Math.Max(viewZ, NearPlane);

            // Screen rows count downward and the world counts upward, which is the whole reason for the minus.
            column = Pin(CenterX + FocalX*viewX/depth);
            row = Pin(HorizonRow - FocalY*viewY/depth);
        }

        /// <summary>Keeps a projected coordinate inside the range an <c>int</c> can hold honestly.</summary>
        /// <param name="value">The projected coordinate.</param>
        /// <returns>The same value, pinned.</returns>
        private static int Pin(double value)
        {
            if (double.IsNaN(value))
                return 0;

            // Rounded, not truncated. Truncation is asymmetric about zero, so the same shape drawn on the left of
            // the screen and on the right would round in opposite directions and come out a cell wider on one side.
            return (int) Math.Round(Math.Clamp(value, -CoordinateLimit, CoordinateLimit),
                MidpointRounding.AwayFromZero);
        }
    }
}
