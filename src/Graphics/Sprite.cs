// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     A picture with a position: something drawn <i>over</i> a scene rather than being the scene.
    ///     <para>
    ///         What separates this from handing the same picture to <see cref="AnsiImage.Overlay(AnsiImage, int, int)" />
    ///         is only that a sprite expects to move. An overlay is composited once and the answer kept; a sprite's
    ///         position is meant to be changed between one frame and the next, which is why <see cref="X" /> and
    ///         <see cref="Y" /> are settable and why <see cref="SpriteScene" /> exists to recompose them cheaply.
    ///     </para>
    ///     <para>
    ///         <see cref="Image" /> is settable for the same reason: an animated sprite is one whose picture is swapped
    ///         for the next frame as time passes, so nothing further is needed here to hold one.
    ///     </para>
    /// </summary>
    /// <seealso cref="SpriteScene" />
    public sealed class Sprite
    {
        private PixelBuffer _image;

        /// <summary>Initializes a new instance of the <see cref="Sprite" /> class at the scene's origin.</summary>
        /// <param name="image">The picture to draw.</param>
        public Sprite(PixelBuffer image)
        {
            Image = image;
        }

        /// <summary>Initializes a new instance of the <see cref="Sprite" /> class at a position.</summary>
        /// <param name="image">The picture to draw.</param>
        /// <param name="x">Distance of the sprite's left edge from the scene's, in scene pixels.</param>
        /// <param name="y">Distance of the sprite's top edge from the scene's, in scene pixels.</param>
        public Sprite(PixelBuffer image, int x, int y) : this(image)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        ///     The picture drawn for this sprite. Transparent pixels let whatever is underneath show through, which is
        ///     what makes a sprite a sprite rather than a rectangle.
        /// </summary>
        public PixelBuffer Image
        {
            get => _image;
            set => _image = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        ///     Distance of the sprite's left edge from the scene's, in scene pixels. May be negative, and may put the
        ///     sprite past the far edge: a sprite is clipped to the scene rather than refused by it, so one can be walked
        ///     in from off-screen without arithmetic to hide the fact.
        /// </summary>
        public int X { get; set; }

        /// <summary>Distance of the sprite's top edge from the scene's, in scene pixels. May be negative.</summary>
        public int Y { get; set; }

        /// <summary>Whether the sprite is drawn. Cheaper and less destructive than removing it from the scene.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Width of the sprite's current picture, in scene pixels.</summary>
        public int Width => Image.Width;

        /// <summary>Height of the sprite's current picture, in scene pixels.</summary>
        public int Height => Image.Height;
    }
}
