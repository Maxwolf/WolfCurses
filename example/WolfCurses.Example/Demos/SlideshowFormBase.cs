// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Base for the slideshow-style demos. Unlike an <c>InputForm</c> (whose text is frozen when the form is
    ///     created), this is a plain <see cref="Form{TData}" /> whose <see cref="OnRenderForm" /> is asked for text
    ///     every render, so it can show a different image over time. All slides are rendered to their ANSI strings once
    ///     (in <see cref="OnFormPostCreate" />) and cached; ticking merely advances which cached slide is shown. Any
    ///     submitted line (ENTER) returns to the menu.
    /// </summary>
    public abstract class SlideshowFormBase : Form<ExampleWindowInfo>
    {
        private string[] _captions = Array.Empty<string>();
        private int _index;
        private string[] _slides = Array.Empty<string>();

        /// <summary>Initializes a new instance of the <see cref="SlideshowFormBase" /> class.</summary>
        /// <param name="window">The parent window.</param>
        protected SlideshowFormBase(IWindow window) : base(window)
        {
        }

        /// <summary>Short label shown above the image.</summary>
        protected abstract string Title { get; }

        /// <summary>Builds the cached ANSI slides and their captions. Called once, after the form is attached.</summary>
        protected abstract (string[] slides, string[] captions) BuildSlides();

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();
            (_slides, _captions) = BuildSlides();

            // The image fills the screen, so put the "return" instruction on the prompt line rather than leaving the
            // menu's "What is your choice?" at the bottom.
            ParentWindow.PromptText = "Press ENTER or ESC to return to the menu";
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Advance one image per simulation tick (about once a second); ignore the many fast system ticks.
            if (systemTick || _slides.Length == 0)
                return;

            _index = (_index + 1) % _slides.Length;
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            if (_slides.Length == 0)
                return $"{Environment.NewLine}No images were found to display.";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"{Title}  ({_index + 1}/{_slides.Length})  {_captions[_index]}");
            sb.AppendLine();
            sb.Append(_slides[_index]);
            return sb.ToString();
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            // The example forwards the input buffer on ENTER; any such submission closes the slideshow.
            ClearForm();
        }
    }
}
