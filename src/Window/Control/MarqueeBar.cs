// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Progress bar that is drawn in characters and is a ping-pong marquee action bouncing back and fourth.
    /// </summary>
    public sealed class MarqueeBar
    {
        private string _bar;

        private readonly string _blankPointer;

        private int _counter;

        private DirectionEnum _currdir;

        private readonly string _pointer;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MarqueeBar" /> class.
        /// </summary>
        public MarqueeBar()
        {
            _bar = "|                         |";
            _pointer = "***";
            _blankPointer = BlankPointer();
            _currdir = DirectionEnum.Right;
            _counter = 1;
        }

        /// <summary>
        ///     How much color the bar is allowed to use. <see cref="AnsiColorModeEnum.Auto" /> asks the environment,
        ///     which is what a running application wants; a concrete mode is how a test pins one answer without
        ///     touching process-wide state such as <c>NO_COLOR</c>. <see cref="AnsiColorModeEnum.None" /> emits no
        ///     escape sequences whatsoever, even for styles that were explicitly set.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     How the moving <c>***</c> pointer looks. Empty by default, which is why an uncolored marquee still
        ///     emits exactly the twenty-seven characters plus a newline it always did.
        /// </summary>
        public TextStyle PointerStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     How everything that is not the pointer looks — the two <c>|</c> end caps and the empty track between
        ///     them. Styled as the (up to) two runs either side of the pointer rather than as one background wash, so
        ///     the pointer's own style is never something the track has to be re-opened around.
        /// </summary>
        public TextStyle TrackStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     sets the attribute blankPointer with a empty string the same length that the pointer
        /// </summary>
        /// <returns>A string filled with space characters</returns>
        private string BlankPointer()
        {
            var blank = new StringBuilder();
            for (var cont = 0; cont < _pointer.Length; cont++)
                blank.Append(" ");
            return blank.ToString();
        }

        /// <summary>
        ///     reset the bar to its original state
        /// </summary>
        private void ClearBar()
        {
            _bar = _bar.Replace(_pointer, _blankPointer);
        }

        /// <summary>remove the previous pointer and place it in a new position</summary>
        /// <param name="start">start index</param>
        /// <param name="end">end index</param>
        private void PlacePointer(int start, int end)
        {
            ClearBar();
            _bar = _bar.Remove(start, end);
            _bar = _bar.Insert(start, _pointer);
        }

        /// <summary>
        ///     Advances the marquee one frame and returns it <b>without</b> a trailing newline, so it drops inline into
        ///     a caller's rendered text the way <see cref="SpinningPixel.Step" /> and the other producers in this
        ///     namespace do — every one of them is newline-free, and a caller composing a line should not have to trim
        ///     one off. <see cref="Step" /> is exactly this plus an <see cref="Environment.NewLine" />, kept for
        ///     consumers that already rely on the trailing newline.
        ///     <para>
        ///         Color is applied to the <em>returned</em> string only, never to the stored bar. That is not a
        ///         stylistic choice: this class animates by mutating one plain string in place — it blanks the old
        ///         pointer with <c>Replace</c> and stamps the new one with <c>Remove</c>/<c>Insert</c> at absolute
        ///         indices — so one escape sequence living inside that string would shift every index past it by its
        ///         own length, and the stamping would start overwriting whatever happened to land at those offsets.
        ///         The stored bar stays plain forever; <see cref="Decorate" /> locates the pointer in it and paints a
        ///         copy.
        ///     </para>
        /// </summary>
        /// <returns>The current marquee frame, colored if a style is set, with no trailing newline.</returns>
        public string Render()
        {
            if (_currdir == DirectionEnum.Right)
            {
                PlacePointer(_counter, _pointer.Length);
                _counter++;
                if (_counter + _pointer.Length == _bar.Length)
                    _currdir = DirectionEnum.Left;
            }
            else
            {
                PlacePointer(_counter - _pointer.Length, _pointer.Length);
                _counter--;
                if (_counter == _pointer.Length)
                    _currdir = DirectionEnum.Right;
            }

            return Decorate(_bar);
        }

        /// <summary>
        ///     Advances the marquee one frame and returns it followed by an <see cref="Environment.NewLine" />. The
        ///     widget's original producer, unchanged so existing consumers keep the trailing newline they relied on;
        ///     new inline callers should prefer <see cref="Render" />, which returns the same frame without it. The
        ///     newline is appended after decoration, so any style opened for the bar is closed before the line break
        ///     and never bleeds onto whatever the owner renders next.
        /// </summary>
        /// <returns>The current marquee frame, colored if a style is set, followed by a newline.</returns>
        public string Step()
        {
            return Render() + Environment.NewLine;
        }

        /// <summary>
        ///     Paints a copy of the plain bar: the pointer in <see cref="PointerStyle" />, everything either side of
        ///     it in <see cref="TrackStyle" />.
        ///     <para>
        ///         Returns the very string it was handed when neither style asks for anything or the resolved mode is
        ///         <see cref="AnsiColorModeEnum.None" />, which is the whole of the byte-identical default path — an
        ///         untouched marquee never reaches a <see cref="StringBuilder" /> at all.
        ///     </para>
        /// </summary>
        /// <param name="bar">The plain bar text, exactly as stored.</param>
        /// <returns>The bar, colored if there is any color to apply.</returns>
        private string Decorate(string bar)
        {
            if (PointerStyle.IsEmpty && TrackStyle.IsEmpty)
                return bar;

            var pointerOpen = PointerStyle.OpenSequence(ColorMode);
            var trackOpen = TrackStyle.OpenSequence(ColorMode);
            if (pointerOpen.Length == 0 && trackOpen.Length == 0)
                return bar;

            // The pointer is located in the plain bar, which is the only place it can be found reliably.
            var at = bar.IndexOf(_pointer, StringComparison.Ordinal);
            if (at < 0)
                return AppendRun(new StringBuilder(bar.Length + 16), trackOpen, bar, 0, bar.Length).ToString();

            var sb = new StringBuilder(bar.Length + 48);
            AppendRun(sb, trackOpen, bar, 0, at);
            AppendRun(sb, pointerOpen, bar, at, _pointer.Length);
            AppendRun(sb, trackOpen, bar, at + _pointer.Length, bar.Length - at - _pointer.Length);
            return sb.ToString();
        }

        /// <summary>
        ///     Appends one slice of the bar, wrapped in an open/reset pair only when there is both something to open
        ///     and something to wrap. A zero-length slice — the track ahead of a pointer parked at the left edge —
        ///     contributes nothing rather than an empty pair of escapes.
        /// </summary>
        /// <param name="sb">The builder to append to.</param>
        /// <param name="open">The style's opening sequence, or an empty string for no style.</param>
        /// <param name="bar">The plain bar text.</param>
        /// <param name="start">Index of the first character of the slice.</param>
        /// <param name="count">How many characters the slice holds.</param>
        /// <returns>The same builder, for chaining.</returns>
        private static StringBuilder AppendRun(StringBuilder sb, string open, string bar, int start, int count)
        {
            if (count <= 0)
                return sb;

            if (open.Length == 0)
                return sb.Append(bar, start, count);

            return sb.Append(open).Append(bar, start, count).Append(TextStyle.ResetSequence);
        }

        /// <summary>
        ///     The direction.
        /// </summary>
        private enum DirectionEnum
        {
            /// <summary>
            ///     The right.
            /// </summary>
            Right,

            /// <summary>
            ///     The left.
            /// </summary>
            Left
        };
    }
}