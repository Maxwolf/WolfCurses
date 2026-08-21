// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     Draws a picture of whatever is selected, using the library's own <see cref="BarChart" /> and
    ///     <see cref="LineGraph" />. Nothing here plots anything: what it does is decide which numbers are the data
    ///     and where their labels come from, which is the only part a spreadsheet has an opinion about.
    ///     <para>
    ///         <b>Labels come from the cells beside the numbers</b>, which is what makes a chart of a real sheet
    ///         readable: a column of figures with the month names in the column to its left is exactly how somebody
    ///         would have typed it, and reading the labels from there costs nothing and asks nothing of the user.
    ///         Where there is no such text, the row number or column letter is used, which at least says which
    ///         cells were charted.
    ///     </para>
    /// </summary>
    internal static class SheetChart
    {
        /// <summary>
        ///     How many numbers are gathered at most. A backstop rather than a layout decision: how many will
        ///     actually be drawn comes from the height of the screen, and this only stops a selection of ten
        ///     thousand cells from being walked in the first place.
        /// </summary>
        private const int MaximumPoints = 200;

        /// <summary>How much blank space is left inside the frame, on every side.</summary>
        private const int FramePadding = 1;

        /// <summary>
        ///     The longest a bar's label may be. Cell text can be a whole sentence, and one long label would
        ///     otherwise squeeze every bar in the chart down to nothing.
        /// </summary>
        private const int MaximumLabel = 18;

        /// <summary>How the values beside the bars are written, which is also how their width is worked out.</summary>
        private const string ValueFormat = "0.##";

        /// <summary>Draws the chart, or says why it cannot.</summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="range">What is selected.</param>
        /// <param name="kind">Which picture to draw.</param>
        /// <param name="width">How many columns there are to draw in.</param>
        /// <param name="rows">
        ///     How many rows the picture must occupy. Exactly, not at most: the chart replaces the grid rather than
        ///     being drawn beside it, so anything shorter leaves rows of the sheet showing underneath it.
        /// </param>
        /// <returns>The picture, newline separated.</returns>
        public static string Render(Sheet sheet, CellRange range, SheetChartKindEnum kind, int width, int rows)
        {
            var labels = new List<string>();
            var values = new List<double>();

            Collect(sheet, range, labels, values);

            if (values.Count == 0)
                return FitRows("Nothing to chart." + Environment.NewLine + Environment.NewLine +
                               "Select some cells with numbers in them and try again.", rows);

            return FitRows(kind == SheetChartKindEnum.Line
                ? Line(values, width, rows)
                : Bars(labels, values, width, rows), rows);
        }

        /// <summary>What the chart is of, said in words, for the caption above it.</summary>
        /// <param name="range">What is selected.</param>
        /// <returns>The caption.</returns>
        public static string Caption(CellRange range)
        {
            // A rectangle is charted by its first column only, and saying so is better than quietly charting
            // something other than what was selected.
            return range.ColumnCount > 1 && range.RowCount > 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}, first column only", range)
                : range.ToString();
        }

        /// <summary>
        ///     Gathers the numbers and their labels.
        ///     <para>
        ///         Three shapes, and which one it is decides where the labels are. A column of numbers is labelled
        ///         from the column to its left; a row of numbers from the row above it; and a rectangle is charted
        ///         by its first column, because a bar chart takes one series and picking silently among several
        ///         would be worse than picking the obvious one out loud.
        ///     </para>
        /// </summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="range">What is selected.</param>
        /// <param name="labels">Where the labels go.</param>
        /// <param name="values">Where the numbers go.</param>
        private static void Collect(Sheet sheet, CellRange range, List<string> labels, List<double> values)
        {
            if (range.RowCount == 1 && range.ColumnCount > 1)
            {
                for (var column = range.FirstColumn; column <= range.LastColumn && values.Count < MaximumPoints;
                     column++)
                {
                    Take(sheet, new CellAddress(range.FirstRow, column), new CellAddress(range.FirstRow - 1, column),
                        CellAddress.ColumnName(column), labels, values);
                }

                return;
            }

            for (var row = range.FirstRow; row <= range.LastRow && values.Count < MaximumPoints; row++)
            {
                Take(sheet, new CellAddress(row, range.FirstColumn),
                    new CellAddress(row, range.FirstColumn - 1),
                    (row + 1).ToString(CultureInfo.InvariantCulture), labels, values);
            }
        }

        /// <summary>Takes one cell, if it holds a number, together with whatever labels it.</summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="address">The cell holding the number.</param>
        /// <param name="beside">The cell that might hold its label.</param>
        /// <param name="fallback">What to call it when that cell holds no text.</param>
        /// <param name="labels">Where the labels go.</param>
        /// <param name="values">Where the numbers go.</param>
        private static void Take(Sheet sheet, CellAddress address, CellAddress beside, string fallback,
            List<string> labels, List<double> values)
        {
            var value = sheet.GetValue(address);

            // Cells that are not numbers are passed over rather than counted as zero, so selecting a column with
            // its heading included charts the figures and not a bar of nothing called "Income".
            if (!value.IsNumber)
                return;

            var label = beside.Column < address.Column || beside.Row < address.Row
                ? sheet.GetValue(beside)
                : SheetValue.Empty;

            values.Add(value.Number);
            labels.Add(label.Kind == SheetValueKindEnum.Text ? label.Text : fallback);
        }

        /// <summary>
        ///     Labelled bars, one per row, which is why this is the chart that has to be told how many rows there
        ///     are: a bar chart of fifty numbers is fifty rows tall whatever the screen says.
        /// </summary>
        /// <param name="labels">What each bar is called.</param>
        /// <param name="values">How long each bar is.</param>
        /// <param name="width">How many columns there are to draw in.</param>
        /// <param name="rows">How many rows there are to draw in.</param>
        /// <returns>The chart.</returns>
        private static string Bars(IReadOnlyList<string> labels, IReadOnlyList<double> values, int width, int rows)
        {
            // One row is kept back for the note when there is more than will fit, so that saying what was left out
            // does not itself push a bar off the bottom.
            var shown = Math.Min(values.Count, values.Count > rows ? Math.Max(1, rows - 1) : rows);
            var items = new List<BarChartValue>(shown);

            var labelWidth = 0;
            var valueWidth = 0;

            for (var i = 0; i < shown; i++)
            {
                var label = labels[i].Length > MaximumLabel ? labels[i].Substring(0, MaximumLabel) : labels[i];

                labelWidth = Math.Max(labelWidth, label.Length);
                valueWidth = Math.Max(valueWidth,
                    values[i].ToString(ValueFormat, CultureInfo.InvariantCulture).Length);

                items.Add(new BarChartValue(label, values[i]));
            }

            var chart = new BarChart
            {
                // Width is the length of the BAR, not of the row: the widget puts the label in front of it and the
                // figure after it, both sized from the data. Guessing a number here instead is what pushes the
                // frame's right-hand border off the screen, where the fit below then trims it away silently.
                Width = Math.Max(8, Body(width) - labelWidth - Separator.Length - valueWidth - 1),
                ValueFormat = ValueFormat,
                ShowTrack = true,
                ShowValues = true,
                Separator = Separator,
                Ramp = ColorRamp.Cool,
                RampMode = ColorRampModeEnum.Level,
                LabelStyle = DosTheme.Frame,
                ValueStyle = DosTheme.Frame,
                TrackStyle = new TextStyle(ConsoleColor.DarkGray, ConsoleColor.DarkBlue),
                SeparatorStyle = DosTheme.Frame
            };

            var drawn = chart.Render(items);

            // Never quietly. A chart showing eleven of fifty numbers looks exactly like a chart of eleven numbers,
            // and somebody reading it would have no way to tell.
            return shown == values.Count
                ? drawn
                : drawn + Environment.NewLine +
                  string.Format(CultureInfo.InvariantCulture, "  ... and {0} more, which will not fit on screen.",
                      values.Count - shown);
        }

        /// <summary>
        ///     A line across the values in order. Unlike bars, this fits any number of points into whatever height
        ///     it is given, so the count never has to be capped.
        /// </summary>
        /// <param name="values">The numbers.</param>
        /// <param name="width">How many columns there are to draw in.</param>
        /// <param name="rows">How many rows there are to draw in.</param>
        /// <returns>The graph.</returns>
        private static string Line(IReadOnlyList<double> values, int width, int rows)
        {
            var graph = new LineGraph
            {
                // Room left for the scale figures the graph writes down its left-hand side.
                Width = Math.Max(16, Body(width) - 10),

                // One row back for the scale line the graph draws underneath itself.
                Height = Math.Max(4, rows - 1),
                Fill = true,
                ShowAxis = true,
                ShowScale = true,
                Ramp = ColorRamp.Heat,
                AxisStyle = DosTheme.Frame,
                ScaleStyle = DosTheme.Frame
            };

            return graph.Render(values);
        }

        /// <summary>What separates a bar's label from the bar, which counts towards the row's width.</summary>
        private const string Separator = " \u2502 ";

        /// <summary>How many columns there are inside the frame, which is what a chart really has to fit into.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The usable width.</returns>
        private static int Body(int width)
        {
            return Math.Max(8, width - 2 - 2 * FramePadding);
        }

        /// <summary>
        ///     Makes the picture exactly as many rows as it was given, both ways.
        ///     <para>
        ///         <b>Padding matters as much as clipping here.</b> The chart is drawn instead of the grid rather
        ///         than over it, and the presenter only repaints the rows a frame actually has, so a picture shorter
        ///         than the sheet it replaced leaves the bottom few rows of that sheet on screen underneath it.
        ///     </para>
        ///     <para>
        ///         Clipping is applied to what a widget really produced rather than to what it was asked for, so a
        ///         widget that draws one row more than its height says can never push the way back off the bottom.
        ///     </para>
        /// </summary>
        /// <param name="chart">The chart.</param>
        /// <param name="rows">How many rows it must occupy.</param>
        /// <returns>The chart, exactly that tall.</returns>
        private static string FitRows(string chart, int rows)
        {
            var lines = chart.Split(Environment.NewLine);

            if (lines.Length == rows)
                return chart;

            if (lines.Length > rows)
                return string.Join(Environment.NewLine, lines, 0, Math.Max(1, rows - 1)) + Environment.NewLine +
                       string.Format(CultureInfo.InvariantCulture,
                           "  ... and {0} more rows than fit on this screen.",
                           lines.Length - Math.Max(1, rows - 1));

            return chart + string.Concat(Enumerable.Repeat(Environment.NewLine, rows - lines.Length));
        }

        /// <summary>
        ///     Puts the chart in a frame, with a caption saying which cells it is of.
        ///     <para>
        ///         Every row is padded to the full width so the framed panel covers the grid it is drawn over
        ///         rather than letting the sheet show through the gaps between its rows.
        ///     </para>
        /// </summary>
        /// <param name="chart">The chart itself.</param>
        /// <param name="title">What the frame's tab reads.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The framed chart.</returns>
        public static string Frame(string chart, string title, int width)
        {
            var box = new Box
            {
                Title = title,
                Border = BoxBorderEnum.Double,
                Padding = FramePadding,

                // The BODY width, not the whole box: the two borders and the padding inside them are added to it,
                // and asking for the console width here makes a box four columns too wide whose right-hand border
                // is then trimmed off by the fit below.
                MinimumWidth = Math.Max(1, width - 2 - 2 * FramePadding),
                BorderStyle = DosTheme.Frame,
                TitleStyle = DosTheme.Title
            };

            var sb = new StringBuilder();

            foreach (var line in box.Render(chart).Split(Environment.NewLine))
                sb.Append(DosTheme.Field.Apply(AnsiText.Fit(line, width))).Append(Environment.NewLine);

            return sb.ToString();
        }
    }
}
