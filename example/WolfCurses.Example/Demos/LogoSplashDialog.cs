// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Diagnostics;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Startup splash: the WolfCurses wordmark in ASCII art, with a rainbow sliding through it, shown before the
    ///     main menu appears. Pressing ENTER dismisses it and reveals the menu.
    ///     <para>
    ///         This used to draw <c>media/logo.jpg</c> through the ANSI image stack, which was a fine demonstration of
    ///         that stack and a slightly odd first impression: a library whose whole subject is text opened with a
    ///         photograph. Letters drawn out of letters say what it is, need no file beside the executable to say it,
    ///         and survive a terminal that can do nothing but ASCII — the picture demos are three keystrokes away for
    ///         anyone who wants pixels.
    ///     </para>
    ///     <para>
    ///         <b>It is also the smallest possible demonstration of the color vocabulary in <c>src/Graphics/</c>.</b>
    ///         Every cell takes its color from <see cref="ColorRamp.Rainbow" /> at a position that depends on the
    ///         column, the row and the elapsed time, which is all a sliding gradient is. Nothing here is special-cased
    ///         in the library, and nothing here asks the terminal what it can do: the styles are left at
    ///         <see cref="AnsiColorModeEnum.Auto" />, so <c>NO_COLOR</c>, a 256-color terminal and the
    ///         <b>Force render type</b> menu's global override all reach this screen without it knowing they exist. On
    ///         a terminal that resolves to <see cref="AnsiColorModeEnum.None" /> the escapes vanish entirely and the
    ///         wordmark is still perfectly legible, because it was only ever characters.
    ///     </para>
    ///     <para>
    ///         Deliberately <b>not</b> an <c>InputForm</c>, which every other press-ENTER screen here is: that base
    ///         builds its text once in <c>OnFormPostCreate</c> and hands back the same string forever, which is exactly
    ///         right for a dialog and cannot animate. This is a plain <see cref="Form{T}" /> that recomposes on its own
    ///         clock, the same shape <see cref="AnimatedGifDialog" /> uses and for the same reason.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class LogoSplashDialog : Form<ExampleWindowInfo>
    {
        /// <summary>
        ///     The wordmark, in the shape a FIGlet "standard" font draws it. Rows are ragged rather than padded to a
        ///     common width — trailing spaces on a source line are the first thing an editor eats, and a row's own
        ///     length is never measured: the block is drawn flush left, so only the leading spaces carry the shape.
        /// </summary>
        private static readonly string[] _banner =
        {
            @"__        __        _   __",
            @"\ \      / /  ___  | | / _|",
            @" \ \ /\ / /  / _ \ | || |_",
            @"  \ V  V /  | (_) || ||  _|",
            @"   \_/\_/    \___/ |_||_|",
            @"  ____",
            @" / ___| _   _  _ __  ___   ___  ___",
            @"| |    | | | || '__|/ __| / _ \/ __|",
            @"| |___ | |_| || |   \__ \|  __/\__ \",
            @" \____| \__,_||_|   |___/ \___||___/"
        };

        /// <summary>
        ///     How many columns one complete pass of the ramp is stretched across. A little under the wordmark's own
        ///     width, so slightly more than a full spectrum is on screen at once and the motion is legible.
        /// </summary>
        private const double CycleColumns = 30.0;

        /// <summary>How fast the gradient slides, in columns a second — a full cycle every two and a half seconds.</summary>
        private const double ColumnsPerSecond = 12.0;

        /// <summary>
        ///     How far the gradient is nudged along for each row down the banner, which is the whole difference between
        ///     a rainbow that slides sideways and one that sweeps diagonally.
        /// </summary>
        private const double RowSkew = 2.0;

        /// <summary>
        ///     How often the banner is recomposed. Twenty-five times a second is smooth to the eye and about forty
        ///     times less work than recomposing on every system tick, which is the rate this form is actually ticked
        ///     at; every recomposition also changes the text, so it costs a redraw of the whole screen as well.
        /// </summary>
        private static readonly TimeSpan _framePeriod = TimeSpan.FromMilliseconds(40);

        /// <summary>
        ///     Runs for the life of the splash and is never restarted, so the gradient's position is a pure function of
        ///     how long the screen has been up. Accumulating a phase per frame instead would let rounding drift.
        /// </summary>
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>When the next recomposition is due, measured on <see cref="_clock" />.</summary>
        private TimeSpan _nextFrame;

        /// <summary>The composed banner, rebuilt on the clock and handed back unchanged by every render in between.</summary>
        private string _rendered;

        /// <summary>Initializes a new instance of the <see cref="LogoSplashDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public LogoSplashDialog(IWindow window) : base(window)
        {
            _rendered = Compose(0.0);
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "Press ENTER to continue";
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // On the system tick, not the simulation tick: the simulation ticks once a second, and a gradient that
            // moved once a second would not read as movement at all.
            if (_clock.Elapsed < _nextFrame)
                return;

            _nextFrame = _clock.Elapsed + _framePeriod;
            _rendered = Compose(_clock.Elapsed.TotalSeconds);
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick, so it hands back a string that is already built.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Composes the whole splash with the gradient at the position it has reached after so many seconds.
        ///     <para>
        ///         Flush left, deliberately: everything else the window draws — the module header above, the prompt
        ///         below — starts at column zero, and a centered block reads as floating away from them. It also means
        ///         the splash never asks the console how wide it is, so it composes to the same bytes whatever the
        ///         window is doing.
        ///     </para>
        /// </summary>
        private static string Compose(double seconds)
        {
            var sb = new StringBuilder();
            sb.AppendLine();

            for (var row = 0; row < _banner.Length; row++)
            {
                AppendColoredRow(sb, _banner[row], row, seconds);
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.Append("a text user interface library for .NET console applications");
            return sb.ToString();
        }

        /// <summary>
        ///     Writes one row of the banner, coloring it cell by cell but emitting an escape only where the escape
        ///     actually changes.
        ///     <para>
        ///         Two things keep the frame small, and both are the library's own lessons about styled text. Runs are
        ///         compared on the <b>resolved escape sequence</b> rather than on the color that produced it, because
        ///         quantization happens downstream: neighbouring columns of a smooth ramp are distinct
        ///         <see cref="Rgb24" /> values that a 256-color or grayscale terminal draws with the identical
        ///         sequence, and comparing colors would spend a reset and an open between two cells that look the
        ///         same. And a space is skipped entirely — a foreground color paints nothing on one — which matters
        ///         here more than anywhere, since ASCII art is mostly space.
        ///     </para>
        /// </summary>
        private static void AppendColoredRow(StringBuilder sb, string line, int row, double seconds)
        {
            var open = string.Empty;

            for (var column = 0; column < line.Length; column++)
            {
                var glyph = line[column];
                var wanted = glyph == ' '
                    ? string.Empty
                    : new TextStyle(ColorRamp.Rainbow.Sample(Sweep(column, row, seconds))).OpenSequence();

                if (!string.Equals(wanted, open, StringComparison.Ordinal))
                {
                    if (open.Length > 0)
                        sb.Append(TextStyle.ResetSequence);

                    sb.Append(wanted);
                    open = wanted;
                }

                sb.Append(glyph);
            }

            if (open.Length > 0)
                sb.Append(TextStyle.ResetSequence);
        }

        /// <summary>
        ///     Where on the ramp a given cell sits at a given moment, as the 0-1 position <see cref="ColorRamp.Sample" />
        ///     takes.
        ///     <para>
        ///         The fold at the end is the whole trick. <see cref="ColorRamp.Rainbow" /> runs red to violet and stops
        ///         there, so simply wrapping the position round would snap violet back to red once a cycle and send a
        ///         hard seam travelling across the wordmark. Folding the wrapped position back on itself instead —
        ///         0-1 then 1-0 — makes the sequence red to violet to red, which is seamless in both directions and
        ///         mirrors the spectrum rather than cutting it.
        ///     </para>
        /// </summary>
        private static double Sweep(double column, double row, double seconds)
        {
            var position = (column - seconds * ColumnsPerSecond + row * RowSkew) / CycleColumns;
            var wrapped = position - Math.Floor(position);
            return wrapped <= 0.5 ? wrapped * 2.0 : (1.0 - wrapped) * 2.0;
        }
    }
}
