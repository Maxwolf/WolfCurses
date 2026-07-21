// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     A configurable determinate progress bar you drop into a window or form's rendered text. Unlike the static
    ///     <see cref="TextProgress" /> helper, this is a reusable, styleable control: set the width, the filled/empty
    ///     glyphs, whether to wrap the bar in brackets, whether to append a percentage, and an optional leading label,
    ///     then call <see cref="Render(double,double)" /> (a value against a maximum) or
    ///     <see cref="Render(double)" /> (a fraction from 0 to 1) each time you draw the screen. The bar and the
    ///     percentage are kept consistent — a completely full bar always reads 100% and a completely empty bar 0%.
    ///     <para>
    ///         Every part of the bar can also be colored — the filled cells, the empty cells, the label, the
    ///         percentage and the brackets each take a <see cref="TextStyle" />, and <see cref="FillRamp" /> paints
    ///         the filled run from a <see cref="ColorRamp" /> instead of one flat color. All of that is off by
    ///         default and costs nothing when it is off: with every style empty and no ramp set, the bar takes a
    ///         separate uncolored code path and emits byte-for-byte what it emitted before color existed. That is a
    ///         hard guarantee, not an intention — the pinned rendering tests would fail on a single stray escape.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     private readonly ProgressBar _bar = new ProgressBar { Width = 20, Label = "Download" };
    ///     // ...
    ///     public override string OnRenderForm() => _bar.Render(bytesDone, bytesTotal);
    ///     // Download [██████████░░░░░░░░░░]  50%
    ///
    ///     // A traffic-light gauge: one color for the whole run, picked by how full it is.
    ///     var health = new ProgressBar { Width = 20, FillRamp = ColorRamp.Traffic, RampMode = ColorRampModeEnum.Level };
    ///     </code>
    /// </example>
    public sealed class ProgressBar
    {
        /// <summary>Number of cells in the bar itself (not counting brackets, label, or percentage). Must be at least 1.</summary>
        public int Width { get; set; } = 30;

        /// <summary>Glyph used for the filled portion of the bar. Defaults to a solid block.</summary>
        public char FilledChar { get; set; } = '█';

        /// <summary>Glyph used for the unfilled portion of the bar. Defaults to a light shade.</summary>
        public char EmptyChar { get; set; } = '░';

        /// <summary>Whether to wrap the bar in <c>[ ]</c> brackets.</summary>
        public bool ShowBrackets { get; set; } = true;

        /// <summary>Whether to append the percentage (rounded to a whole number) after the bar.</summary>
        public bool ShowPercentage { get; set; } = true;

        /// <summary>Optional text placed before the bar, followed by a single space when present.</summary>
        public string Label { get; set; }

        /// <summary>
        ///     How much color fidelity to commit to when this bar is styled, mirroring
        ///     <see cref="AnsiImageOptions.ColorMode" />. <see cref="AnsiColorModeEnum.Auto" /> — the default — asks
        ///     <see cref="AnsiConsole.DetectColorMode" />, which honours <c>NO_COLOR</c> and the usual terminal
        ///     environment variables. <see cref="AnsiColorModeEnum.None" /> guarantees not one escape sequence is
        ///     emitted no matter what styles are set, which is also what makes this bar testable: a test pins a
        ///     concrete mode here instead of mutating the process-wide environment other tests are reading.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     How the filled cells are painted. When <see cref="FillRamp" /> is also set the ramp supplies the
        ///     foreground and this style's background and bold still apply, so a ramp can be laid over a background
        ///     without the two fighting.
        /// </summary>
        public TextStyle FilledStyle { get; set; }

        /// <summary>
        ///     How the unfilled cells are painted. Worth setting on its own: dimming the track is usually a bigger
        ///     readability win than coloring the fill, because it is the contrast between the two that reads as
        ///     progress.
        /// </summary>
        public TextStyle EmptyStyle { get; set; }

        /// <summary>
        ///     How the leading <see cref="Label" /> is painted. The single space that separates the label from the
        ///     bar is left uncolored — it belongs to neither field, and coloring it would put a stray block of
        ///     background between them.
        /// </summary>
        public TextStyle LabelStyle { get; set; }

        /// <summary>
        ///     How the trailing percentage is painted. As with the label, the space that separates it from the bar
        ///     stays uncolored; the style covers the number and its <c>%</c>.
        /// </summary>
        public TextStyle PercentageStyle { get; set; }

        /// <summary>
        ///     How the <c>[</c> and <c>]</c> brackets are painted when <see cref="ShowBrackets" /> is on. Ignored
        ///     entirely when it is off, since there is nothing to paint.
        /// </summary>
        public TextStyle BracketStyle { get; set; }

        /// <summary>
        ///     An optional ramp that colors the filled run, overriding <see cref="FilledStyle" />'s foreground.
        ///     <see cref="RampMode" /> decides whether it is read across the bar or against the value. Null — the
        ///     default — means the fill is one flat color and the whole ramp machinery is skipped.
        /// </summary>
        public ColorRamp FillRamp { get; set; }

        /// <summary>
        ///     What <see cref="FillRamp" /> means. <see cref="ColorRampModeEnum.Spread" /> (the default) reads the
        ///     ramp <em>across the bar's width</em>, so cell <c>i</c> takes the ramp at <c>i / (Width - 1)</c> — a
        ///     rainbow bar, where each cell keeps the same color no matter how full the bar is, so the fill looks
        ///     like it is uncovering a fixed gradient rather than repainting one.
        ///     <see cref="ColorRampModeEnum.Level" /> instead colors the entire run with the ramp sampled at the
        ///     completion fraction — a traffic-light bar, where the whole thing changes color as it fills.
        /// </summary>
        public ColorRampModeEnum RampMode { get; set; } = ColorRampModeEnum.Spread;

        /// <summary>
        ///     Renders the bar for a value measured against a maximum. A non-positive maximum (or a non-finite value)
        ///     renders as empty; values outside the range are clamped so the bar never over- or under-flows.
        /// </summary>
        /// <param name="value">Current progress value.</param>
        /// <param name="maximum">The value that represents a full bar.</param>
        public string Render(double value, double maximum)
        {
            var fraction = maximum > 0 && !double.IsNaN(value) && !double.IsNaN(maximum)
                ? value / maximum
                : 0d;
            return Render(fraction);
        }

        /// <summary>
        ///     Renders the bar for a fraction of completion. The fraction is clamped to the range 0 to 1 (and a
        ///     non-finite fraction is treated as 0), so callers never have to sanitize it themselves.
        /// </summary>
        /// <param name="fraction">Completion from 0 (empty) to 1 (full).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Width" /> is less than 1.</exception>
        public string Render(double fraction)
        {
            if (Width < 1)
                throw new ArgumentOutOfRangeException(nameof(Width), Width,
                    "Progress bar width must be greater than zero.");

            if (double.IsNaN(fraction) || double.IsInfinity(fraction))
                fraction = 0d;

            fraction = Math.Clamp(fraction, 0d, 1d);

            var percent = (int) Math.Round(fraction * 100d, MidpointRounding.AwayFromZero);

            var filled = (int) Math.Round(fraction * Width, MidpointRounding.AwayFromZero);
            filled = Math.Clamp(filled, 0, Width);

            // Keep the bar and the percentage from contradicting each other: the bar is completely full only at
            // 100% and completely empty only at 0%. Without this, a fraction like 0.95 rounds the fill up to a full
            // bar (round(9.5) == Width) while the label still reads 95%.
            if (percent >= 100)
                filled = Width;
            else if (filled >= Width)
                filled = Width - 1;

            if (percent <= 0)
                filled = 0;
            else if (filled <= 0)
                filled = 1;

            // Layout is completely settled before the first color question is asked, and in that order on purpose:
            // the width guard has to throw exactly the exception it always threw, and how many cells there are has
            // to be decided before a ramp can be asked what color they should be.
            return TryResolveColorMode(out var mode)
                ? RenderStyled(filled, percent, fraction, mode)
                : RenderPlain(filled, percent);
        }

        /// <summary>
        ///     Decides whether this render emits any escape sequences at all, and if so in which color mode.
        ///     <para>
        ///         Two ways to answer no, and both matter. Nothing is styled — every style empty and no ramp — which
        ///         is the default state of the control and takes <see cref="RenderPlain" />, the untouched original
        ///         code, so an uncolored bar is byte-for-byte what it always was. Or the resolved mode is
        ///         <see cref="AnsiColorModeEnum.None" />, meaning the terminal (or <c>NO_COLOR</c>) said no, in which
        ///         case explicitly-set styles are dropped rather than degraded. The first test comes first so a bar
        ///         nobody colored never even asks the environment a question.
        ///     </para>
        /// </summary>
        /// <param name="resolved">The concrete color mode to render in; meaningless when this returns false.</param>
        /// <returns>True when styling should be emitted.</returns>
        private bool TryResolveColorMode(out AnsiColorModeEnum resolved)
        {
            resolved = AnsiColorModeEnum.None;

            if (FillRamp == null && FilledStyle.IsEmpty && EmptyStyle.IsEmpty && LabelStyle.IsEmpty &&
                PercentageStyle.IsEmpty && BracketStyle.IsEmpty)
                return false;

            resolved = ColorMode == AnsiColorModeEnum.Auto ? AnsiConsole.DetectColorMode() : ColorMode;
            return resolved != AnsiColorModeEnum.None;
        }

        /// <summary>
        ///     Composes the bar with no color of any kind. Kept as its own method, character for character as it was
        ///     before styling existed, so the uncolored path cannot drift: nothing here consults a style, so nothing
        ///     here can accidentally emit one.
        /// </summary>
        /// <param name="filled">How many cells of the bar are filled.</param>
        /// <param name="percent">The whole-number percentage to append.</param>
        private string RenderPlain(int filled, int percent)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Label))
                sb.Append(Label).Append(' ');

            if (ShowBrackets)
                sb.Append('[');

            sb.Append(FilledChar, filled);
            sb.Append(EmptyChar, Width - filled);

            if (ShowBrackets)
                sb.Append(']');

            if (ShowPercentage)
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0,3}%", percent);

            return sb.ToString();
        }

        /// <summary>
        ///     Composes the bar with color. Structurally identical to <see cref="RenderPlain" /> — same fields in the
        ///     same order, same separating spaces — with each field handed to its style. Every wrap goes through
        ///     <see cref="TextStyle.Apply(string, AnsiColorModeEnum)" />, which is a no-op for an empty style and for
        ///     an empty run, so the fields nobody colored (and the zero-length runs a 0% or 100% bar produces) stay
        ///     exactly as they were even on this path.
        /// </summary>
        /// <param name="filled">How many cells of the bar are filled.</param>
        /// <param name="percent">The whole-number percentage to append.</param>
        /// <param name="fraction">The completion fraction, needed by <see cref="ColorRampModeEnum.Level" />.</param>
        /// <param name="mode">The resolved color mode; never <see cref="AnsiColorModeEnum.Auto" /> or None here.</param>
        private string RenderStyled(int filled, int percent, double fraction, AnsiColorModeEnum mode)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Label))
                sb.Append(LabelStyle.Apply(Label, mode)).Append(' ');

            if (ShowBrackets)
                sb.Append(BracketStyle.Apply('[', 1, mode));

            AppendFilled(sb, filled, fraction, mode);
            sb.Append(EmptyStyle.Apply(EmptyChar, Width - filled, mode));

            if (ShowBrackets)
                sb.Append(BracketStyle.Apply(']', 1, mode));

            if (ShowPercentage)
            {
                // The space stays outside the style for the same reason the label's does: it separates two fields
                // and belongs to neither, so coloring it would hang a stray block off the end of the bar.
                sb.Append(' ');
                sb.Append(PercentageStyle.Apply(
                    string.Format(CultureInfo.InvariantCulture, "{0,3}%", percent), mode));
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Appends the filled run, which is the only part of the bar that can be more than one color.
        ///     <para>
        ///         Without a ramp it is a single styled run. With one in <see cref="ColorRampModeEnum.Level" /> it is
        ///         still a single run, just with the ramp choosing its color from the value. Only
        ///         <see cref="ColorRampModeEnum.Spread" /> goes cell by cell, and even then it coalesces neighbours
        ///         that came out the same color into one escape run — which is not a micro-optimization but the
        ///         difference between a stepped six-stop flag costing six sequences and costing one per cell, on a
        ///         string that is rebuilt on every frame.
        ///     </para>
        /// </summary>
        /// <param name="sb">The buffer being composed.</param>
        /// <param name="filled">How many cells of the bar are filled; zero appends nothing at all.</param>
        /// <param name="fraction">The completion fraction, used by <see cref="ColorRampModeEnum.Level" />.</param>
        /// <param name="mode">The resolved color mode.</param>
        private void AppendFilled(StringBuilder sb, int filled, double fraction, AnsiColorModeEnum mode)
        {
            if (filled <= 0)
                return;

            if (FillRamp == null)
            {
                sb.Append(FilledStyle.Apply(FilledChar, filled, mode));
                return;
            }

            if (RampMode == ColorRampModeEnum.Level)
            {
                sb.Append(RunStyle(FillRamp.Sample(fraction)).Apply(FilledChar, filled, mode));
                return;
            }

            // Spread: the gradient is anchored to the bar's whole width rather than to the drawn part, so a cell
            // keeps its color as the bar grows past it. Anchoring it to the filled length instead would repaint the
            // entire gradient every frame and read as a shimmer rather than as progress.
            //
            // Runs are broken on the *escape sequence*, not on the Rgb24 the ramp handed over. The two disagree in
            // every indexed mode: Grayscale has 26 possible answers and Palette256 has 256, so a smooth ramp across
            // forty cells produces forty colors that collapse onto a couple of dozen sequences. Comparing colors
            // there would close and reopen a run between neighbours the terminal draws identically — bytes spent, on
            // a string rebuilt every frame, saying nothing. The color compare stays in front of it as the fast path,
            // so the extra work is one OpenSequence per color *change* — exactly the number the old code already
            // built at flush time, which is why this costs nothing in TrueColor where nothing quantizes.
            //
            // runColor is left default rather than seeded with SpreadColor(0): the guard above proves filled > 0, so
            // the loop always runs and runLength == 0 skips the flush on the first cell, meaning the seed could only
            // ever be overwritten before it was read.
            var runColor = default(Rgb24);
            var runOpen = string.Empty;
            var runLength = 0;

            for (var cell = 0; cell < filled; cell++)
            {
                var color = SpreadColor(cell);
                if (runLength > 0 && SameColor(color, runColor))
                {
                    runLength++;
                    continue;
                }

                var open = RunStyle(color).OpenSequence(mode);
                if (runLength > 0 && string.Equals(open, runOpen, StringComparison.Ordinal))
                {
                    // Different color, identical escape: the same run as far as the terminal is concerned. Adopt the
                    // new color so the cheap compare keeps working for whatever follows it.
                    runColor = color;
                    runLength++;
                    continue;
                }

                AppendRun(sb, runOpen, runLength);
                runColor = color;
                runOpen = open;
                runLength = 1;
            }

            AppendRun(sb, runOpen, runLength);
        }

        /// <summary>
        ///     Appends one coalesced run of filled cells, wrapped in an already-resolved opening sequence when there
        ///     is one. A run of zero appends nothing at all — escapes included — which is what keeps the very first
        ///     flush of the loop above (where there is no run yet) from landing a stray open/close pair.
        /// </summary>
        /// <param name="sb">The buffer being composed.</param>
        /// <param name="open">The opening escape sequence, or an empty string to append the run plain.</param>
        /// <param name="count">How many cells the run covers.</param>
        private void AppendRun(StringBuilder sb, string open, int count)
        {
            if (count <= 0)
                return;

            if (open.Length == 0)
            {
                sb.Append(FilledChar, count);
                return;
            }

            sb.Append(open).Append(FilledChar, count).Append(TextStyle.ResetSequence);
        }

        /// <summary>
        ///     The ramp color for one cell of the bar, read across the full <see cref="Width" />.
        /// </summary>
        /// <param name="cell">The cell index, counting from zero at the left of the bar.</param>
        /// <returns>The color that cell should be drawn in.</returns>
        private Rgb24 SpreadColor(int cell)
        {
            // A one-cell bar has no span between its first and last cell to spread anything across, and dividing by
            // that zero span would hand Sample a NaN. It would swallow it silently, which is exactly the kind of
            // accident that survives for years, so the degenerate case says what it means: the single cell is the
            // start of the ramp.
            if (Width <= 1)
                return FillRamp.Sample(0d);

            return FillRamp.Sample(cell / (double) (Width - 1));
        }

        /// <summary>
        ///     Builds the style for a run of ramped fill: the ramp's color as the foreground, over whatever
        ///     background and weight <see cref="FilledStyle" /> asked for. The ramp overrides only the foreground,
        ///     so setting both is a composition rather than a contest.
        /// </summary>
        /// <param name="color">The ramp color for this run.</param>
        /// <returns>The style to draw the run with.</returns>
        private TextStyle RunStyle(Rgb24 color)
        {
            return FilledStyle.WithForeground(new TextColor(color));
        }

        /// <summary>
        ///     Whether two ramp colors are identical, compared channel by channel.
        ///     <see cref="Rgb24" /> declares no equality of its own, and the inherited value-type comparison would
        ///     answer the same question far more slowly on a path that runs once per cell per frame.
        /// </summary>
        /// <param name="left">The first color.</param>
        /// <param name="right">The second color.</param>
        private static bool SameColor(Rgb24 left, Rgb24 right)
        {
            return left.R == right.R && left.G == right.G && left.B == right.B;
        }
    }
}
