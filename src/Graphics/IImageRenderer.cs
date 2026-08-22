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
        ///     A short name for how this renderer draws — "sixel", "kitty", "half blocks" — for a status line or a
        ///     picker. Defaults to the implementing type's name, which is a serviceable answer for a renderer that
        ///     does not care to give a better one.
        /// </summary>
        string Name => GetType().Name;

        /// <summary>
        ///     Whether this renderer paints <b>real pixels</b> — sixel, kitty — rather than character cells.
        ///     <para>
        ///         The question an application asks when it has to decide something about layout: how many rows a
        ///         picture needs to be worth showing, whether to fall back to text, whether a thumbnail will read at
        ///         all. Half blocks get two pixels per row, so eight chess squares across twenty rows is under four
        ///         pixels a square and a knight is the same smudge as a bishop; the same rows of sixel are a picture.
        ///     </para>
        ///     <para>
        ///         <b>This exists because the alternative was type-testing the built-in classes</b>, which is what
        ///         both example applications were reduced to doing — and which quietly gets the wrong answer for
        ///         exactly the renderers this seam exists to allow, since a third-party true-pixel renderer is not
        ///         <see cref="SixelImageRenderer" /> or <see cref="KittyImageRenderer" />. Defaults to false for the
        ///         same reason <see cref="AnsiConsole.DetectGraphicsProtocol()" /> is biased to
        ///         <see cref="AnsiGraphicsProtocolEnum.None" />: guessing wrong this way costs a plainer picture,
        ///         guessing wrong the other way costs a screen full of escape garbage.
        ///     </para>
        /// </summary>
        bool DrawsTruePixels => false;

        /// <summary>
        ///     How many image pixels this renderer puts into one character cell, across and down.
        ///     <para>
        ///         The question a <b>source</b> asks rather than a caller drawing one picture: given a window this
        ///         many columns by this many rows, what size should the pixels arrive at? Anything streaming -
        ///         frames off a pipe, a camera, a plot being regenerated - can then produce them at that size and
        ///         skip the resample entirely, and resampling is the dominant cost in this whole stack (see the
        ///         measurements on <see cref="PixelBuffer.Resize" />). Handing a renderer a picture already the
        ///         right size is not an optimisation so much as the difference between thirty frames a second and
        ///         three.
        ///     </para>
        ///     <para>
        ///         The defaults are half blocks' own numbers - one pixel across, <b>two</b> down, since that
        ///         renderer's whole trick is an upper and a lower half in each cell - because that is what a
        ///         renderer built out of character cells is, and because guessing small is the safe way to guess:
        ///         too few pixels costs a coarser picture, too many costs an upscale nobody wanted. The true-pixel
        ///         renderers already carry these as constructor knobs and answer with what they were told.
        ///     </para>
        /// </summary>
        int CellPixelWidth => 1;

        /// <summary>
        ///     How many image pixels tall one character cell is. See <see cref="CellPixelWidth" />; two by default,
        ///     which is half blocks' upper and lower half.
        /// </summary>
        int CellPixelHeight => 2;

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
