// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Turns a decoded <see cref="PixelBuffer" /> into the text that draws it in a terminal. This is the render half
    ///     of the graphics feature's two seams: <see cref="IImageDecoder" /> decides how image <em>bytes</em> become
    ///     pixels, and this decides how those pixels become <em>screen output</em> — so a consuming application can pick
    ///     a different way of drawing (true-pixel protocols like sixel or kitty instead of the default half-block text)
    ///     without touching anything else.
    ///     <para>
    ///         The return value is deliberately a plain string, because that is the only currency the rest of the
    ///         library deals in: a window's <c>OnRenderWindow</c> returns a string, <see cref="Core.SceneGraph" /> diffs
    ///         strings, and <see cref="ConsolePresenter" /> draws them. An implementation that draws with something
    ///         other than character cells must still describe its output in whole rows — see
    ///         <see cref="AnsiGraphics.RowPlaceholder" /> for how an image taller than one row accounts for the rows it
    ///         covers.
    ///     </para>
    /// </summary>
    /// <seealso cref="ImageRenderers" />
    /// <seealso cref="HalfBlockImageRenderer" />
    public interface IImageRenderer
    {
        /// <summary>
        ///     Renders the image, sized and colored according to <paramref name="options" />.
        /// </summary>
        /// <param name="image">The decoded image to draw. Implementations should throw on null.</param>
        /// <param name="options">
        ///     Rendering options, or null to use the defaults. Not every option applies to every renderer (a true-pixel
        ///     renderer has no use for <see cref="AnsiImageOptions.ColorMode" />, for example); an implementation is
        ///     expected to honor what it can and ignore the rest rather than throw.
        /// </param>
        /// <returns>
        ///     The image as text, rows separated by <c>Environment.NewLine</c> and with no trailing newline, ready to be
        ///     concatenated into a window's rendered output.
        /// </returns>
        string Render(PixelBuffer image, AnsiImageOptions options = null);
    }
}
