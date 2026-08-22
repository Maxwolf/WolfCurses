// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     A position along something that has a length, drawn as a bar you can click: the transport under a video,
    ///     the scrubber under a piece of audio, the playhead of an animation, a long job with an end in sight.
    ///     <para>
    ///         <b>It keeps its layout</b>, for the same reason <see cref="MenuBar" />, <see cref="Keypad" />,
    ///         <see cref="MonthGrid" /> and <see cref="FieldList" /> do: the bar's columns are worked out once and
    ///         read by both the drawing and the hit test, so the moment a click seeks to is the moment drawn under
    ///         the pointer.
    ///     </para>
    ///     <para>
    ///         <b>Both ends are exact, and that is the off-by-one.</b> The first column is zero and the <i>last</i>
    ///         column is the whole duration, so the scale divides by one less than the width. Divide by the width
    ///         and the end of the bar is unreachable: the last second of a film cannot be seeked to and the marker
    ///         never arrives at the right-hand end even when playback has finished. That is the same arithmetic
    ///         <see cref="ScrollBar" /> records about its thumb, from the opposite side.
    ///     </para>
    ///     <para>
    ///         <b>An unknown length is a real state and is drawn as one.</b> A pipe and a live stream have no end,
    ///         so the bar draws empty, no marker is placed, and <see cref="TimeAt" /> answers null rather than
    ///         inventing a moment: seeking a stream to forty percent of nothing means nothing.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     _bar.Position = _clock.Position;
    ///     _bar.Duration = _clock.Duration;
    ///
    ///     var at = _bar.TimeAt(mouse.Row, mouse.Column);
    ///     if (at != null)
    ///         _clock.SeekTo(at.Value);
    ///     </code>
    /// </example>
    public sealed class Timeline
    {
        /// <summary>How many screen columns the whole control occupies, times included.</summary>
        public int Width { get; set; } = 40;

        /// <summary>Where the playhead is.</summary>
        public TimeSpan Position { get; set; }

        /// <summary>How long the whole thing runs for, or <see cref="TimeSpan.Zero" /> when that is not known.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>The screen row the control is drawn on, which the hit test is measured against.</summary>
        public int Row { get; set; }

        /// <summary>The screen column the control's left edge is in.</summary>
        public int Column { get; set; }

        /// <summary>Whether the elapsed and total times are written either side of the bar.</summary>
        public bool ShowTimes { get; set; } = true;

        /// <summary>What the part already played is drawn with.</summary>
        public char FilledChar { get; set; } = '━';

        /// <summary>What the part still to come is drawn with.</summary>
        public char TrackChar { get; set; } = '─';

        /// <summary>What the playhead is drawn with.</summary>
        public char MarkerChar { get; set; } = '●';

        /// <summary>Which colour vocabulary the styles resolve through; pinnable per instance for tests.</summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>How the played part is painted.</summary>
        public TextStyle FilledStyle { get; set; }

        /// <summary>How the part still to come is painted.</summary>
        public TextStyle TrackStyle { get; set; }

        /// <summary>How the playhead is painted.</summary>
        public TextStyle MarkerStyle { get; set; }

        /// <summary>How the times either side are painted.</summary>
        public TextStyle TimeStyle { get; set; }

        /// <summary>
        ///     How many columns the bar itself gets, which is the width less whatever the times take. Never less
        ///     than one, or there would be no bar to click and no column to put the marker in.
        /// </summary>
        public int BarWidth
        {
            get
            {
                var width = Math.Max(1, Width);

                return ShowTimes ? Math.Max(1, width - LabelWidth * 2 - 2) : width;
            }
        }

        /// <summary>The screen column the bar's own left edge is in, past the elapsed time when one is shown.</summary>
        public int BarColumn => Column + (ShowTimes ? LabelWidth + 1 : 0);

        /// <summary>How wide a time label is, measured from the longer of the two so the bar does not move.</summary>
        private int LabelWidth => Math.Max(Format(Position).Length, Format(Duration).Length);

        /// <summary>Draws the control as one row.</summary>
        /// <returns>The row, exactly <see cref="Width" /> visible columns wide.</returns>
        public string Render()
        {
            var bar = BarWidth;
            var marker = MarkerColumn(bar);
            var row = new TextRow {ColorMode = ColorMode};

            if (ShowTimes)
            {
                row.Append(AnsiText.Fit(Format(Position), LabelWidth, AnsiHorizontalAlignmentEnum.Right), TimeStyle);
                row.Append(" ", TimeStyle);
            }

            for (var i = 0; i < bar; i++)
            {
                if (i == marker)
                    row.Append(MarkerChar.ToString(), MarkerStyle);
                else if (i < marker)
                    row.Append(FilledChar.ToString(), FilledStyle);
                else
                    row.Append(TrackChar.ToString(), TrackStyle);
            }

            if (ShowTimes)
            {
                row.Append(" ", TimeStyle);
                row.Append(AnsiText.Fit(Format(Duration), LabelWidth), TimeStyle);
            }

            return row.Render();
        }

        /// <summary>
        ///     Which moment a cell of the bar stands for, or null for anywhere that is not the bar: the times either
        ///     side, another row, and every column of a control whose length is not known.
        /// </summary>
        /// <param name="row">The screen row pressed.</param>
        /// <param name="column">The screen column pressed.</param>
        /// <returns>The moment, or null.</returns>
        public TimeSpan? TimeAt(int row, int column)
        {
            if (row != Row || Duration <= TimeSpan.Zero)
                return null;

            var bar = BarWidth;
            var offset = column - BarColumn;

            if (offset < 0 || offset >= bar)
                return null;

            if (bar <= 1)
                return TimeSpan.Zero;

            // Divides by one less than the width, so the last cell is the whole duration rather than one cell short
            // of it. The other way round, the end of a film is a place the pointer cannot reach.
            return TimeSpan.FromSeconds(Duration.TotalSeconds * offset / (bar - 1));
        }

        /// <summary>Where the playhead is drawn, in screen columns, or -1 when there is no playhead to draw.</summary>
        /// <returns>The screen column, or -1.</returns>
        public int MarkerColumn()
        {
            var marker = MarkerColumn(BarWidth);

            return marker < 0 ? -1 : BarColumn + marker;
        }

        /// <summary>
        ///     How a moment is written: minutes and seconds under an hour, hours as well over it. A two-minute song
        ///     reading <c>0:02:13</c> is noise, and an hour-long film reading <c>73:41</c> is arithmetic.
        /// </summary>
        /// <param name="time">The moment. Negative reads as zero.</param>
        /// <returns>The text.</returns>
        public static string Format(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
                time = TimeSpan.Zero;

            var hours = (int) time.TotalHours;

            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, time.Minutes, time.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", time.Minutes, time.Seconds);
        }

        /// <summary>Which cell of the bar the playhead sits in, or -1 when the length is not known.</summary>
        /// <param name="bar">How many columns the bar has.</param>
        /// <returns>The cell, or -1.</returns>
        private int MarkerColumn(int bar)
        {
            if (Duration <= TimeSpan.Zero || bar <= 0)
                return -1;

            if (bar == 1)
                return 0;

            var fraction = Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0d, 1d);

            return Math.Clamp((int) Math.Round(fraction * (bar - 1)), 0, bar - 1);
        }
    }
}
