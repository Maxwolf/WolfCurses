// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System.Collections.Generic;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Old school spinning pixel progress, normally used to show the thread is not locked by some running process.
    ///     Cycles <c>/ - \ |</c> one glyph per <see cref="Step" />, wrapping forever.
    ///     <para>
    ///         The library drives one of these internally for its own tick spinner, but it is a plain reusable widget
    ///         like <see cref="MarqueeBar" />: construct one, call <see cref="Step" /> once a beat, and print what it
    ///         returns. It is the spinner half of the indeterminate-progress pair the package advertises, the marquee
    ///         being the other.
    ///     </para>
    /// </summary>
    public sealed class SpinningPixel
    {
        private readonly List<string> _animation;

        private int _counter;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SpinningPixel" /> class.
        /// </summary>
        public SpinningPixel()
        {
            _animation = new List<string> {"/", "-", @"\", "|"};
            _counter = 0;
        }

        /// <summary>
        ///     How much color <see cref="Step" /> is allowed to use. <see cref="AnsiColorModeEnum.Auto" /> asks the
        ///     environment, which is what a running application wants; a concrete mode is how a test pins one answer
        ///     without touching process-wide state such as <c>NO_COLOR</c>. <see cref="AnsiColorModeEnum.None" /> emits
        ///     no escape sequences whatsoever, even for a glyph style that was explicitly set.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     How the spinning glyph looks. Empty by default, which is why an uncolored spinner returns exactly the
        ///     one bare character it always did — no escape, not even a reset — so anything that measured or compared
        ///     that character keeps working unchanged. This is the same contract <see cref="MarqueeBar.PointerStyle" />
        ///     keeps, scaled to the single run a spinner is.
        /// </summary>
        public TextStyle GlyphStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     prints the character found in the animation according to the current index, colored with
        ///     <see cref="GlyphStyle" /> when one is set.
        /// </summary>
        /// <returns>
        ///     The <see cref="string" />.
        /// </returns>
        public string Step()
        {
            var barText = _animation[_counter];
            _counter++;
            if (_counter == _animation.Count)
                _counter = 0;

            // TextStyle.Apply returns barText untouched — the same reference, no escapes — for an empty style or a
            // resolved mode of None, and with an empty style it never even asks the environment. So the default
            // spinner is byte-for-byte what it was before it could be colored.
            return GlyphStyle.Apply(barText, ColorMode);
        }
    }
}
