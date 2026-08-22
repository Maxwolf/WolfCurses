// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using WolfCurses.Graphics;

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     Paints the field into a <see cref="PixelBuffer" /> — the picture this game is actually played on.
    ///     <para>
    ///         <b>Nothing here is decoded; every pixel is drawn.</b> That is the whole difference between this and
    ///         WolfChess 5000, which composites artwork loaded from disk. Missile Command is a sky, some lines and
    ///         some circles, so it needs no assets at all and the project copies no content — the graphics stack is
    ///         exercised just as hard by a picture the program invents as by one it reads.
    ///     </para>
    ///     <para>
    ///         <b>Everything is drawn two pixels thick, and the canvas is sized against the renderer that will
    ///         draw it.</b> Under half blocks the picture is area-average resampled on its way to the screen, so a
    ///         one-pixel trail arrives as roughly a quarter of a cell's worth of colour — a grey ghost of a line
    ///         rather than a line. Drawing at twice the resolution that path will use, with strokes two pixels
    ///         wide, is what makes a trail survive the trip, and it is also what buys sub-cell motion: a warhead
    ///         moving a third of a character cell shows up as shading rather than as a jump.
    ///     </para>
    ///     <para>
    ///         <b>That bargain is a fact about half blocks and used to be applied to every renderer, which was a
    ///         live bug on any terminal with real pixels.</b> A fit defaults to <c>Contain</c> and
    ///         <c>ImageFit</c> <i>enlarges</i>, so a canvas sized for half blocks was blown up on its way to a
    ///         sixel terminal - at eighteen rows by seventy-eight columns, a 160x100 canvas into a 780x360 pixel
    ///         area is a magnification of about three and a half, and the deliberate two-pixel trail arrived
    ///         seven pixels thick. Every stroke on the screen was wrong by the same factor. That is the Battlezone
    ///         play-test finding, unlearned; <see cref="SizeFor" /> now asks the renderer instead of assuming,
    ///         and the enlarging resample disappears with it.
    ///     </para>
    ///     <para>
    ///         The one buffer is reused between frames and cleared with <see cref="PixelBuffer.Fill(Rgba32)" />,
    ///         which is exactly the operation that has to <i>paint</i> rather than composite — compositing a colour
    ///         over last frame's sky would never clear anything, and the field would smear.
    ///     </para>
    /// </summary>
    public sealed class MissileFieldArt
    {
        /// <summary>Night, and the colour everything else is drawn over.</summary>
        private static readonly Rgba32 _sky = new(0x08, 0x0C, 0x20, 0xFF);

        private static readonly Rgba32 _ground = new(0x9A, 0x5B, 0x1E, 0xFF);
        private static readonly Rgba32 _city = new(0x5A, 0xD2, 0xE6, 0xFF);
        private static readonly Rgba32 _cityRuin = new(0x3A, 0x2A, 0x22, 0xFF);
        private static readonly Rgba32 _silo = new(0xE0, 0xE0, 0xC0, 0xFF);
        private static readonly Rgba32 _siloRuin = new(0x4A, 0x3A, 0x30, 0xFF);
        private static readonly Rgba32 _shell = new(0x60, 0xE0, 0x70, 0xFF);
        private static readonly Rgba32 _shellLow = new(0xE0, 0xB0, 0x30, 0xFF);
        private static readonly Rgba32 _crosshair = new(0xFF, 0xFF, 0xFF, 0xFF);

        /// <summary>Enemy trails, by kind. Different colours so a smart bomb can be told from a warhead at a glance.</summary>
        private static readonly Rgba32 _icbmTrail = new(0xFF, 0x50, 0x50, 0xFF);

        private static readonly Rgba32 _mirvTrail = new(0xFF, 0xA0, 0x30, 0xFF);
        private static readonly Rgba32 _smartTrail = new(0xFF, 0x60, 0xE0, 0xFF);
        private static readonly Rgba32 _counterTrail = new(0x70, 0xC0, 0xFF, 0xFF);

        /// <summary>
        ///     A fireball's colour over its life: the white of the flash, then yellow, orange, red, and out. Sampled
        ///     on the blast's own age, so every fireball on screen is a different colour and the eye reads which
        ///     ones are about to go.
        /// </summary>
        private static readonly ColorRamp _fireball = ColorRamp.Smooth(
            new Rgb24(0xFF, 0xFF, 0xE8),
            new Rgb24(0xFF, 0xE0, 0x60),
            new Rgb24(0xFF, 0x90, 0x20),
            new Rgb24(0xE0, 0x30, 0x10),
            new Rgb24(0x50, 0x10, 0x08));

        /// <summary>Each city's skyline, so they are not six identical blocks. Fractions of the city block's height.</summary>
        private static readonly double[] _skyline = {0.75, 1.0, 0.6, 0.9, 0.7, 0.85};

        private readonly PixelBuffer _canvas;

        /// <summary>Initializes a new instance of the <see cref="MissileFieldArt" /> class at a canvas size.</summary>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        public MissileFieldArt(int width, int height)
        {
            _canvas = new PixelBuffer(width, height);
        }

        /// <summary>Canvas width in pixels.</summary>
        public int Width => _canvas.Width;

        /// <summary>Canvas height in pixels.</summary>
        public int Height => _canvas.Height;

        /// <summary>
        ///     Works out a canvas for the character rows the game has been given, out of the renderer's own idea
        ///     of how many pixels a cell holds.
        ///     <para>
        ///         Half blocks answer two pixels a row, so <c>rows * CellPixelHeight * Supersample</c> reproduces
        ///         exactly the <c>rows * 4</c> and the 100..200 clamp this used to be written as. A renderer
        ///         drawing real pixels answers something like twenty, and gets a canvas near its own grid instead
        ///         of a half-block-sized one to magnify - which is the whole point, since the strokes are chosen
        ///         in <i>output</i> pixels and a magnified canvas takes every one of them up with it.
        ///     </para>
        ///     <para>
        ///         The cap is the cost on the other side of the same knob: every pixel is base64 on its way to
        ///         the terminal thirty times a second, and <c>Fill</c> plus every <c>DrawLine</c> is paid per
        ///         pixel per frame.
        ///     </para>
        /// </summary>
        /// <param name="rows">How many character rows the picture may occupy.</param>
        /// <param name="renderer">The renderer about to draw it; null asks the current default.</param>
        /// <returns>The canvas size.</returns>
        public static (int Width, int Height) SizeFor(int rows, IImageRenderer renderer)
        {
            renderer ??= ImageRenderers.Default;

            var cellHeight = Math.Max(1, renderer.CellPixelHeight);
            var height = renderer.DrawsTruePixels
                ? Math.Clamp(rows*cellHeight, 100, 420)
                : Math.Clamp(rows*cellHeight*Supersample, 100, 200);

            return ((int) Math.Round(height*MissileField.Aspect), height);
        }

        /// <summary>
        ///     Draws the whole field, crosshair and all. The same buffer comes back every time, so a caller that
        ///     wants to keep a frame must copy it.
        /// </summary>
        /// <param name="field">What to draw.</param>
        /// <param name="aimX">Where the player is aiming, in world units.</param>
        /// <param name="aimY">Where the player is aiming, in world units.</param>
        /// <returns>The finished picture.</returns>
        public PixelBuffer Paint(MissileField field, double aimX, double aimY)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            _canvas.Fill(_sky);

            DrawGround();
            DrawCities(field);
            DrawSilos(field);
            DrawTrails(field);
            DrawFireballs(field);

            if (!field.IsOver)
                DrawCrosshair(aimX, aimY);

            return _canvas;
        }

        /// <summary>Turns a world x into a canvas column.</summary>
        private int ToX(double worldX) => (int) Math.Round(worldX/MissileField.Aspect*(_canvas.Width - 1));

        /// <summary>Turns a world altitude into a canvas row. Altitude runs up, rows run down; this is the only flip.</summary>
        private int ToY(double worldY) => (int) Math.Round((1.0 - worldY)*(_canvas.Height - 1));

        /// <summary>How thick a stroke is at this canvas size, never less than two.</summary>
        /// <summary>
        ///     How many canvas pixels the half-block path is drawn at per pixel it will really show. Two, which
        ///     is what makes a one-cell-pixel line survive being area-averaged down.
        /// </summary>
        private const int Supersample = 2;

        private int Stroke => Math.Max(2, _canvas.Height/70);

        private void DrawGround()
        {
            var top = ToY(MissileField.GroundY);
            _canvas.Fill(0, top, _canvas.Width, _canvas.Height - top, _ground);
        }

        private void DrawCities(MissileField field)
        {
            var baseline = ToY(MissileField.GroundY);
            var block = Math.Max(4, _canvas.Height/14);
            var half = Math.Max(3, _canvas.Width/60);

            for (var i = 0; i < MissileField.CityPositions.Count; i++)
            {
                var centre = ToX(MissileField.CityPositions[i]);
                var standing = field.CitiesStanding[i];
                var height = standing ? (int) (block*_skyline[i]) : Math.Max(2, block/5);

                _canvas.Fill(centre - half, baseline - height, half*2, height, standing ? _city : _cityRuin);
            }
        }

        private void DrawSilos(MissileField field)
        {
            var baseline = ToY(MissileField.GroundY);
            var block = Math.Max(4, _canvas.Height/16);
            var half = Math.Max(3, _canvas.Width/55);

            for (var silo = 0; silo < MissileField.SiloPositions.Count; silo++)
            {
                var centre = ToX(MissileField.SiloPositions[silo]);
                var standing = field.SilosStanding[silo];
                var color = standing ? _silo : _siloRuin;

                // A stepped triangle, drawn as one row of Fill per step. A shape this small is clearer built out of
                // rectangles than described as a polygon.
                var steps = Math.Max(3, block/2);
                for (var step = 0; step < steps; step++)
                {
                    var width = half*2*(steps - step)/steps;
                    _canvas.Fill(centre - width/2, baseline - block + step*block/steps,
                        Math.Max(1, width), Math.Max(1, block/steps + 1), color);
                }

                if (!standing)
                    continue;

                DrawAmmo(field.SiloAmmo[silo], centre, baseline - block - Stroke*3, half);
            }
        }

        /// <summary>
        ///     A row of pips above a battery, one per shell. Drawn into the picture rather than written beside it,
        ///     because the marker contract means nothing may sit to the left of the payload row — and because the
        ///     arcade put them in the playfield for the same reason it is the right place: the player is looking at
        ///     the sky, not at a status line.
        /// </summary>
        private void DrawAmmo(int shells, int centre, int top, int half)
        {
            var pip = Math.Max(1, half/5);
            var gap = pip + Math.Max(1, pip/2);
            var color = shells <= 3 ? _shellLow : _shell;
            var left = centre - gap*MissileField.AmmoPerSilo/2;

            for (var i = 0; i < shells; i++)
                _canvas.Fill(left + i*gap, top, pip, Math.Max(2, pip*2), color);
        }

        private void DrawTrails(MissileField field)
        {
            foreach (var missile in field.Missiles)
            {
                if (!missile.Alive)
                    continue;

                var color = missile.Kind switch
                {
                    MissileKindEnum.Mirv => _mirvTrail,
                    MissileKindEnum.SmartBomb => _smartTrail,
                    MissileKindEnum.Counter => _counterTrail,
                    _ => _icbmTrail
                };

                _canvas.DrawLine(ToX(missile.OriginX), ToY(missile.OriginY),
                    ToX(missile.X), ToY(missile.Y), color, Stroke);

                // A brighter head, so the eye follows the thing rather than the streak behind it.
                _canvas.DrawDisc(ToX(missile.X), ToY(missile.Y), Stroke, _crosshair);
            }
        }

        private void DrawFireballs(MissileField field)
        {
            foreach (var blast in field.Blasts)
            {
                var radius = (int) Math.Round(blast.Radius/MissileField.Aspect*(_canvas.Width - 1));
                if (radius <= 0)
                    continue;

                // Compositing rather than painting, which is what makes two overlapping fireballs brighter than one
                // - and that brightness is how a chain reaction reads as a chain reaction rather than as a lot of
                // separate circles.
                var alpha = (byte) Math.Clamp(255 - 170*blast.Age01, 40, 255);
                _canvas.DrawDisc(ToX(blast.X), ToY(blast.Y), radius, _fireball.Sample(blast.Age01).WithAlpha(alpha));
            }
        }

        private void DrawCrosshair(double aimX, double aimY)
        {
            var x = ToX(aimX);
            var y = ToY(aimY);
            var arm = Math.Max(4, _canvas.Width/40);

            _canvas.DrawLine(x - arm, y, x - arm/3, y, _crosshair, Stroke);
            _canvas.DrawLine(x + arm/3, y, x + arm, y, _crosshair, Stroke);
            _canvas.DrawLine(x, y - arm, x, y - arm/3, _crosshair, Stroke);
            _canvas.DrawLine(x, y + arm/3, x, y + arm, _crosshair, Stroke);
        }
    }
}
