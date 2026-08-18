// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     Draws the scene as real pixels: black, and green lines on it.
    ///     <para>
    ///         This is the half of the graphics stack that generates rather than decodes, the same as Missile
    ///         Command — no artwork, no content beside the executable, and the whole picture assembled from
    ///         <see cref="PixelBuffer.DrawLine(int,int,int,int,Rgba32,int)" /> calls. What it adds over that game is
    ///         that the lines are not chosen; they are <i>worked out</i>, from a world that has no pictures in it at
    ///         all.
    ///     </para>
    ///     <para>
    ///         <b>Everything is drawn two pixels thick and the canvas is twice the resolution the terminal will
    ///         use.</b> A one-pixel line area-averaged down to a character cell arrives as a grey smudge at a
    ///         quarter strength, which for a picture made of nothing but lines means a picture made of nothing. It
    ///         is also what buys sub-cell motion: a tank sliding a third of a cell shows as shading rather than as a
    ///         jump.
    ///     </para>
    /// </summary>
    public sealed class BattlezoneArt
    {
        /// <summary>Night on the plain.</summary>
        private static readonly Rgba32 _night = new(0x04, 0x06, 0x0A, 0xFF);

        private static readonly Rgba32 _horizon = new(0x17, 0x8C, 0x38, 0xFF);
        private static readonly Rgba32 _scenery = new(0x27, 0xCC, 0x55, 0xFF);
        private static readonly Rgba32 _enemy = new(0x86, 0xFF, 0x94, 0xFF);
        private static readonly Rgba32 _saucer = new(0xA8, 0xFF, 0xE4, 0xFF);
        private static readonly Rgba32 _shell = new(0xFF, 0xF2, 0xA0, 0xFF);
        private static readonly Rgba32 _explosion = new(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Rgba32 _reticle = new(0x33, 0xFF, 0x66, 0xFF);
        private static readonly Rgba32 _radar = new(0x1B, 0x82, 0x3C, 0xFF);
        private static readonly Rgba32 _blip = new(0xFF, 0x5C, 0x48, 0xFF);
        private static readonly Rgba32 _crack = new(0xEA, 0xFF, 0xF2, 0xFF);

        private readonly PixelBuffer _canvas;
        private readonly BattleScene _scene;
        private readonly Action<int, int, int, int, BattleInkEnum> _draw;

        /// <summary>Initializes a new instance of the <see cref="BattlezoneArt" /> class at a canvas size.</summary>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        public BattlezoneArt(int width, int height)
        {
            _canvas = new PixelBuffer(width, height);

            // Square pixels, so one focal length does for both axes - see WireCamera for why that is worth saying
            // out loud. Seventy degrees across, which is close to what the cabinet showed.
            var focal = width*0.714;
            _scene = new BattleScene(new WireCamera(width, height, focal, focal));
            _draw = Draw;
        }

        /// <summary>Canvas width in pixels.</summary>
        public int Width => _canvas.Width;

        /// <summary>Canvas height in pixels.</summary>
        public int Height => _canvas.Height;

        /// <summary>The eye this picture is drawn through, whose two focal lengths are equal. See the constructor.</summary>
        public WireCamera Camera => _scene.Camera;

        /// <summary>
        ///     Works out a canvas for the character cells the game has been given: two pixels per column and four
        ///     per row, which is twice what half blocks will use in each direction and so leaves the resampling
        ///     something to average.
        /// </summary>
        /// <param name="columns">How many character columns the picture may occupy.</param>
        /// <param name="rows">How many character rows the picture may occupy.</param>
        /// <returns>The canvas size.</returns>
        public static (int Width, int Height) SizeFor(int columns, int rows)
        {
            return (Math.Clamp(columns*2, 80, 440), Math.Clamp(rows*4, 48, 240));
        }

        /// <summary>Draws the whole scene. The same buffer comes back every time, so a caller keeping a frame copies it.</summary>
        /// <param name="field">What to draw.</param>
        /// <returns>The finished picture.</returns>
        public PixelBuffer Paint(BattleField field)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            // Fill, which paints rather than composites - compositing over last frame's sky would never clear
            // anything and the plain would smear into a solid green fog within a second.
            _canvas.Fill(_night);
            _scene.Draw(field, _draw);
            return _canvas;
        }

        /// <summary>Puts one line down in whatever colour its meaning calls for.</summary>
        /// <param name="x0">One end, across.</param>
        /// <param name="y0">One end, down.</param>
        /// <param name="x1">The other end, across.</param>
        /// <param name="y1">The other end, down.</param>
        /// <param name="ink">What the line is.</param>
        private void Draw(int x0, int y0, int x1, int y1, BattleInkEnum ink)
        {
            var color = ink switch
            {
                BattleInkEnum.Horizon => _horizon,
                BattleInkEnum.Scenery => _scenery,
                BattleInkEnum.Enemy => _enemy,
                BattleInkEnum.Saucer => _saucer,
                BattleInkEnum.Shell => _shell,
                BattleInkEnum.Explosion => _explosion,
                BattleInkEnum.Reticle => _reticle,
                BattleInkEnum.Radar => _radar,
                BattleInkEnum.Blip => _blip,
                _ => _crack
            };

            var thickness = ink switch
            {
                BattleInkEnum.Blip => 5,
                BattleInkEnum.Shell => 3,
                BattleInkEnum.Radar => 1,
                _ => 2
            };

            _canvas.DrawLine(x0, y0, x1, y1, color, thickness);
        }
    }
}
