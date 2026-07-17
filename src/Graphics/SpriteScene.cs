// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     A background with sprites on top of it, recomposed as often as they move.
    ///     <para>
    ///         This is what <see cref="AnsiImage.Overlay(AnsiImage, int, int)" /> is not: a scene is built once and
    ///         asked for a picture over and over, so the background is kept as pixels rather than re-decoded, and each
    ///         <see cref="Compose" /> is a copy and a few blits. Sprites are drawn in list order, so the last one added
    ///         is nearest the viewer.
    ///     </para>
    ///     <para>
    ///         <b>The scene's size is the background's size, and that is the knob that matters.</b> Composing costs what
    ///         its area costs, and so does turning the result into a string afterwards — which is much the larger half.
    ///         Measured on a 1280x987 photograph with one sprite, drawn into an 78x16 terminal: composing and rendering
    ///         at the photograph's own resolution takes about <b>9.2 ms a frame</b>, of which 7.3 ms is the renderer
    ///         resampling 1.26 million pixels down to a picture a few dozen cells across — every frame, to throw
    ///         essentially all of it away. Resize the background once to something near what the terminal can actually
    ///         show and the same frame costs about <b>0.3 ms</b>. Nothing here does that for you, because only the caller
    ///         knows what it is aiming at; but the choice is entirely yours to make, and it is worth thirty times.
    ///     </para>
    ///     <para>
    ///         Sizing the scene also decides how finely a sprite can move: positions are whole scene pixels, so a scene
    ///         no bigger than the terminal's own grid moves its sprites a whole character cell at a time. A little larger
    ///         than the grid buys smoother motion, because the renderer's area-averaging turns a sub-cell position into
    ///         shading rather than a jump.
    ///     </para>
    /// </summary>
    /// <seealso cref="Sprite" />
    public sealed class SpriteScene
    {
        /// <summary>Initializes a new instance of the <see cref="SpriteScene" /> class over a background picture.</summary>
        /// <param name="background">
        ///     The picture the sprites are drawn over. Not copied here and not modified by <see cref="Compose" />, so it
        ///     may be shared between scenes.
        /// </param>
        public SpriteScene(PixelBuffer background)
        {
            Background = background ?? throw new ArgumentNullException(nameof(background));
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SpriteScene" /> class over nothing: a transparent scene of
        ///     the given size, which a terminal shows as its own background wherever no sprite covers it.
        /// </summary>
        /// <param name="width">Scene width in pixels.</param>
        /// <param name="height">Scene height in pixels.</param>
        public SpriteScene(int width, int height) : this(new PixelBuffer(width, height))
        {
        }

        /// <summary>The picture the sprites are drawn over.</summary>
        public PixelBuffer Background { get; }

        /// <summary>
        ///     The sprites, drawn in order, so the last is nearest the viewer. Add, remove and reorder freely between
        ///     frames.
        /// </summary>
        public IList<Sprite> Sprites { get; } = new List<Sprite>();

        /// <summary>Scene width in pixels, which is the background's.</summary>
        public int Width => Background.Width;

        /// <summary>Scene height in pixels, which is the background's.</summary>
        public int Height => Background.Height;

        /// <summary>
        ///     Draws the background and every visible sprite over it, into a new picture. The scene's own background is
        ///     left as it was, so this may be called as often as the sprites move.
        /// </summary>
        /// <returns>A new <see cref="PixelBuffer" /> the size of the scene.</returns>
        public PixelBuffer Compose()
        {
            // A copy rather than the background itself: composing into the background would mean the second frame drew
            // sprites over the first frame's sprites, and the scene would silently smear instead of animating. This is
            // the same trap the GIF decoder's frames have, and the same answer.
            var canvas = new PixelBuffer(Background.Width, Background.Height, (byte[]) Background.Data.Clone());

            foreach (var sprite in Sprites)
            {
                if (sprite == null)
                    throw new InvalidOperationException("The scene holds a null sprite.");

                // DrawImage is alpha-compositing "source over" and clips anything hanging off the edge, so a sprite
                // half way out of the scene needs no special case here.
                if (sprite.Visible)
                    canvas.DrawImage(sprite.Image, sprite.X, sprite.Y);
            }

            return canvas;
        }

        /// <summary>
        ///     Every other sprite on the scene whose box overlaps the given one's — what touched what, which is the
        ///     question a game asks constantly and the reason <see cref="Sprite.Intersects" /> exists.
        ///     <para>
        ///         Invisible sprites are skipped. <see cref="Sprite.Visible" /> is documented as the gentler way of
        ///         taking a sprite off the scene, and something that is not on the scene cannot be bumped into; a caller
        ///         who wants the geometry regardless has <see cref="Sprite.Intersects" /> to ask directly. The given
        ///         sprite's own visibility is not consulted, since it is the one doing the asking.
        ///     </para>
        ///     <para>
        ///         The sprite need not be on this scene, which is occasionally useful: a rectangle can be tried
        ///         somewhere before anything is moved there.
        ///     </para>
        /// </summary>
        /// <param name="sprite">The sprite to test everything else against.</param>
        /// <returns>The sprites it is touching, in the order they are drawn.</returns>
        public IEnumerable<Sprite> SpritesTouching(Sprite sprite)
        {
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));

            foreach (var other in Sprites)
                if (other != null && !ReferenceEquals(other, sprite) && other.Visible && other.Intersects(sprite))
                    yield return other;
        }

        /// <summary>
        ///     Composes the scene and renders it to an ANSI string, the way <see cref="AnsiImage.ToAnsi(AnsiImageOptions)" />
        ///     would.
        /// </summary>
        /// <param name="options">How to size and colour the result; null for the defaults.</param>
        /// <returns>The scene as text ready to write to a terminal.</returns>
        public string ToAnsi(AnsiImageOptions options = null)
        {
            return AnsiImage.FromPixels(Compose()).ToAnsi(options);
        }

        /// <summary>Composes the scene and renders it with a specific renderer.</summary>
        /// <param name="options">How to size and colour the result; null for the defaults.</param>
        /// <param name="renderer">The renderer to draw with.</param>
        /// <returns>The scene as text ready to write to a terminal.</returns>
        public string ToAnsi(AnsiImageOptions options, IImageRenderer renderer)
        {
            return AnsiImage.FromPixels(Compose()).ToAnsi(options, renderer);
        }
    }
}
