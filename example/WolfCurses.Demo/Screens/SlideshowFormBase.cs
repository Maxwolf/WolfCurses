// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     Base for the slideshow-style demos. Unlike an <c>InputForm</c> (whose text is frozen when the form is
    ///     created), this is a plain <see cref="Form{TData}" /> whose <see cref="OnRenderForm" /> is asked for text
    ///     every render, so it can show a different image over time. All slides are rendered to their ANSI strings once
    ///     (in <see cref="OnFormPostCreate" />) and cached; ticking merely advances which cached slide is shown. Any
    ///     submitted line (ENTER) returns to the menu.
    ///     <para>
    ///         The whole frame is composed the same way, in <see cref="Compose" />, and only where
    ///         <see cref="_index" /> actually moves. Building it in <see cref="OnRenderForm" /> instead threw the cache
    ///         away one frame later: those strings are megabytes each (about 2.6 MB of half blocks and about 41 MB of
    ///         sixel at 200x50), the scene graph asks for the text on every system tick rather than once per drawn
    ///         frame, and a slide here sits still for a whole simulation tick. That is the same copy, of the same
    ///         megabytes, roughly a thousand times a second for a picture nobody changed.
    ///     </para>
    /// </summary>
    public abstract class SlideshowFormBase : Form<DemoWindowInfo>
    {
        private string[] _captions = Array.Empty<string>();
        private string _current = string.Empty;
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

            // Composed here and not lazily: Window.SetForm calls this before it returns, so no render can arrive
            // ahead of it, and a slideshow that found nothing gets its "no images" frame from the same place rather
            // than from a branch in the render method.
            Compose();

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
            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // A pure read, deliberately. The scene graph asks the focused form for its text on every system tick,
            // which is about a thousand a second, not once per drawn frame. So anything built in here is built a
            // thousand times over for a slide that changes once a second, and the slide is megabytes of ANSI.
            return _current;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            // The demo forwards the input buffer on ENTER; any such submission closes the slideshow.
            ClearForm();
        }

        /// <summary>
        ///     Composes the frame for whichever slide <see cref="_index" /> now points at, so every render until the
        ///     next one is a field read. Called from the two places the index can move: once when the slides arrive,
        ///     and once per simulation tick.
        /// </summary>
        private void Compose()
        {
            if (_slides.Length == 0)
            {
                _current = $"{Environment.NewLine}No images were found to display.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"{Title}  ({_index + 1}/{_slides.Length})  {_captions[_index]}");
            sb.AppendLine();
            sb.Append(_slides[_index]);
            _current = sb.ToString();
        }
    }
}
