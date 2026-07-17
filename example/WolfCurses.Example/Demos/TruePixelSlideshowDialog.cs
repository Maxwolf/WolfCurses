// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     The same slideshow as <see cref="SlideshowDialog" />, but drawn with whichever <see cref="IImageRenderer" />
    ///     the user picked in the menu — so the difference between colored half-block characters and a terminal's real
    ///     pixels can be seen side by side on the same pictures.
    ///     <para>
    ///         Note what does <em>not</em> change: the form still builds strings and returns them from
    ///         <see cref="SlideshowFormBase.OnRenderForm" />, exactly as the half-block slideshow does. Swapping the
    ///         renderer is the only difference, because an image is "just more string" whichever protocol draws it.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class TruePixelSlideshowDialog : SlideshowFormBase
    {
        /// <summary>Initializes a new instance of the <see cref="TruePixelSlideshowDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public TruePixelSlideshowDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string Title => UserData.SelectedImageRendererName ?? "True-pixel slideshow";

        /// <inheritdoc />
        protected override (string[] slides, string[] captions) BuildSlides()
        {
            var files = DemoImages.ImageFiles();
            var options = DemoImages.FitOptions();
            var renderer = UserData.SelectedImageRenderer ?? new HalfBlockImageRenderer();

            var slides = new string[files.Length];
            var captions = new string[files.Length];
            for (var i = 0; i < files.Length; i++)
            {
                // Rendering happens once here and the strings are cached, which matters more than usual for the
                // true-pixel renderers: they resample to a far larger pixel grid and, for sixel, build a palette.
                slides[i] = AnsiImage.FromFile(files[i]).ToAnsi(options, renderer);
                captions[i] = Path.GetFileName(files[i]);
            }

            return (slides, captions);
        }
    }
}
