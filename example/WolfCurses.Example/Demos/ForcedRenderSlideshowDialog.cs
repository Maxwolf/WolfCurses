// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     The same slideshow as <see cref="SlideshowDialog" />, but drawn with whichever render type the user forced in
    ///     the menu — so the whole ladder can be compared on the same pictures, from a terminal's real pixels down
    ///     through colored half blocks to the shaded ASCII that a colorless terminal gets.
    ///     <para>
    ///         Note what does <em>not</em> change: the form still builds strings and returns them from
    ///         <see cref="SlideshowFormBase.OnRenderForm" />, exactly as the half-block slideshow does. The render type
    ///         is the only difference, because an image is "just more string" whichever protocol draws it.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class ForcedRenderSlideshowDialog : SlideshowFormBase
    {
        /// <summary>Initializes a new instance of the <see cref="ForcedRenderSlideshowDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public ForcedRenderSlideshowDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string Title => UserData.SelectedImageRendererName ?? "Forced render type";

        /// <inheritdoc />
        protected override (string[] slides, string[] captions) BuildSlides()
        {
            var files = DemoImages.ImageFiles();
            var renderer = UserData.SelectedImageRenderer ?? new HalfBlockImageRenderer();

            // The two halves of a forced render type: which renderer draws the pixels, and how much color it may spend
            // doing it. Only the half-block renderer reads the color mode; sixel and kitty ignore it.
            var options = DemoImages.FitOptions();
            options.ColorMode = UserData.SelectedImageColorMode;

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
