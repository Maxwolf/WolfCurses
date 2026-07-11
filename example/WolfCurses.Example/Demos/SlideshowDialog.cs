// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Cycles through every image in the shared media folder, each rendered in the terminal with ANSI graphics.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class SlideshowDialog : SlideshowFormBase
    {
        /// <summary>Initializes a new instance of the <see cref="SlideshowDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SlideshowDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string Title => "ANSI Slideshow";

        /// <inheritdoc />
        protected override (string[] slides, string[] captions) BuildSlides()
        {
            var files = DemoImages.ImageFiles();
            var options = DemoImages.FitOptions();

            var slides = new string[files.Length];
            var captions = new string[files.Length];
            for (var i = 0; i < files.Length; i++)
            {
                slides[i] = AnsiImage.RenderFile(files[i], options);
                captions[i] = Path.GetFileName(files[i]);
            }

            return (slides, captions);
        }
    }
}
