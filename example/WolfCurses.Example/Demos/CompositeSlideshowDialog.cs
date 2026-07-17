// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Cycles through the media images with the transparent penguin (<c>transparent_test.png</c>) alpha-composited
    ///     on top of each one, demonstrating that a transparent image and the image beneath it are both visible.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class CompositeSlideshowDialog : SlideshowFormBase
    {
        /// <summary>Initializes a new instance of the <see cref="CompositeSlideshowDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public CompositeSlideshowDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string Title => "ANSI Compositing";

        /// <inheritdoc />
        protected override (string[] slides, string[] captions) BuildSlides()
        {
            // The penguin is asked for by name rather than picked out of the slideshow list, which no longer contains
            // it: the list is the photographs, and the penguin is the thing being put on top of them.
            var backgrounds = DemoImages.SlideshowImages();
            if (backgrounds.Length == 0 || !File.Exists(DemoImages.PenguinPath))
                return (Array.Empty<string>(), Array.Empty<string>());

            var penguin = AnsiImage.FromFile(DemoImages.PenguinPath);
            var options = DemoImages.FitOptions();

            var slides = new string[backgrounds.Length];
            var captions = new string[backgrounds.Length];
            for (var i = 0; i < backgrounds.Length; i++)
            {
                var background = AnsiImage.FromFile(backgrounds[i]);

                // Scale the penguin to about 60% of the background's shorter side, then center it on top.
                var size = Math.Max(1, (int) (Math.Min(background.Width, background.Height) * 0.6));
                var composed = background.Overlay(penguin.Resize(size, size));

                slides[i] = composed.ToAnsi(options);
                captions[i] = $"penguin over {Path.GetFileName(backgrounds[i])}";
            }

            return (slides, captions);
        }
    }
}
