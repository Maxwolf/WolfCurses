// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     Assembles the whole spreadsheet screen: a menu bar, a framed grid with lettered columns and numbered
    ///     rows, scrollbars down the right and along the bottom, the cell entry line, and a key-hint strip.
    ///     <para>
    ///         Pure composition, and almost all of it is <see cref="TextRow" />: every row of the grid is built as
    ///         plain runs with a style beside each, and only turned into escapes at the moment it is drawn. That is
    ///         what makes the open menu panel simple here, where the sibling editor needed three separate
    ///         hand-written work-arounds for the same problem: the panel is a slice of the row, the panel, and
    ///         another slice.
    ///     </para>
    ///     <para>
    ///         <b>Every row is padded to the full width.</b> A background colour only covers what was written, and
    ///         the presenter erases the rest of the line with the terminal's own colour, so a row that stops after
    ///         its last cell leaves the field ending in a ragged edge.
    ///     </para>
    /// </summary>
    internal static class SheetChrome
    {
        /// <summary>Rows the frame and its furniture cost, which is what the grid does not get.</summary>
        public const int ChromeRows = 6;

        /// <summary>Columns the frame costs: the left edge and the scrollbar down the right.</summary>
        public const int ChromeColumns = 2;

        /// <summary>How wide the row-number gutter is, which fits three digits and a space.</summary>
        public const int GutterWidth = 5;

        /// <summary>The screen row the menu bar is drawn on.</summary>
        public const int BarRow = 1;

        /// <summary>The screen row the frame's top edge is on, which is where an open menu panel starts.</summary>
        public const int BorderRow = 2;

        /// <summary>The screen row the lettered column headings are on.</summary>
        public const int HeaderRow = 3;

        /// <summary>The screen row the first row of cells is drawn on.</summary>
        public const int GridTopRow = 4;

        /// <summary>Composes the screen.</summary>
        /// <param name="menuBar">The menu bar, already told how wide it is.</param>
        /// <param name="sheet">The grid.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="cursor">The cell the keyboard is on.</param>
        /// <param name="selection">The selected rectangle, which is the cursor alone when nothing is swept.</param>
        /// <param name="pointer">The cell the mouse is over, or null when it is somewhere else.</param>
        /// <param name="title">What the frame's tab reads.</param>
        /// <param name="entry">The cell entry line, under the grid, still as runs so its caret can be drawn.</param>
        /// <param name="status">The key-hint strip's text.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The whole screen, newline separated.</returns>
        public static string Compose(MenuBar menuBar, Sheet sheet, TableViewport viewport, CellAddress cursor,
            CellRange selection, CellAddress? pointer, string title, TextRow entry, string status, int width)
        {
            var sb = new StringBuilder();
            var bodyWidth = Math.Max(1, width - ChromeColumns);

            // The bar only. The open panel is drawn OVER the grid further down rather than appended here: a panel
            // that added rows would shove the whole sheet down the screen every time a menu opened.
            sb.Append(menuBar.RenderTitleBar(width)).Append(Environment.NewLine);

            var panel = menuBar.IsOpen ? menuBar.DropdownRows() : (IReadOnlyList<string>) Array.Empty<string>();
            var panelWidth = Math.Min(menuBar.DropdownWidth, width);
            var panelColumn = panelWidth <= 0
                ? 0
                : Math.Clamp(menuBar.DropdownColumn, 0, Math.Max(0, width - panelWidth));

            // The panel's first row covers the frame's top edge rather than starting under it, or the border shows
            // between the bar and the menu and the menu reads as floating loose rather than dropping out of its
            // own title.
            sb.Append(Overlay(TopBorder(title, width), panel, 0, panelColumn, panelWidth, width))
                .Append(Environment.NewLine);

            sb.Append(Overlay(HeaderRowText(sheet, viewport, cursor, bodyWidth), panel, 1, panelColumn, panelWidth,
                width)).Append(Environment.NewLine);

            var bar = VerticalBar(sheet, viewport);
            var cells = bar.Cells();

            for (var row = 0; row < viewport.Rows; row++)
            {
                var line = GridRow(sheet, viewport, row, cursor, selection, pointer, bodyWidth, cells[row]);

                // Two rows have already been drawn over by the panel: the border and the column headings.
                sb.Append(Overlay(line, panel, row + 2, panelColumn, panelWidth, width)).Append(Environment.NewLine);
            }

            sb.Append(BottomBorder(sheet, viewport, width)).Append(Environment.NewLine);
            sb.Append(entry.PadTo(width, DosTheme.Field).Render(0, width)).Append(Environment.NewLine);
            sb.Append(DosTheme.Status.Apply(AnsiText.Fit(status, width)));

            return sb.ToString();
        }

        /// <summary>
        ///     How many rows of cells fit, once the frame has taken its share. Deliberately unaffected by an open
        ///     menu, since the panel is drawn over the grid and the grid keeps its size.
        /// </summary>
        /// <param name="consoleHeight">The console height.</param>
        /// <param name="reserved">Rows the scene graph and the prompt take outside this screen.</param>
        /// <returns>The grid's height, never less than one.</returns>
        public static int Rows(int consoleHeight, int reserved)
        {
            return Math.Max(1, consoleHeight - reserved - ChromeRows);
        }

        /// <summary>The scrollbar as the frame draws it, so that a press is measured against the bar on screen.</summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <returns>The vertical bar.</returns>
        public static ScrollBar VerticalBar(Sheet sheet, TableViewport viewport)
        {
            return new ScrollBar
            {
                Length = viewport.Rows,
                Total = sheet.RowCount,
                Visible = viewport.Rows,
                Position = viewport.FirstRow,
                ArrowStyle = DosTheme.ScrollArrow,
                TrackStyle = DosTheme.ScrollTrack,
                ThumbStyle = DosTheme.ScrollThumb
            };
        }

        /// <summary>
        ///     Draws a row with the menu panel over the top of it, when the panel reaches that row.
        ///     <para>
        ///         The whole of what <see cref="TextRow" /> is for. The row is cut at a column, the panel goes in,
        ///         and the rest of the row follows: none of which can be done to a finished styled string without
        ///         cutting an escape sequence in half.
        ///     </para>
        /// </summary>
        /// <param name="row">The row, built as runs.</param>
        /// <param name="panel">The panel's rows, or none when no menu is open.</param>
        /// <param name="panelRow">Which of the panel's rows belongs on this screen row.</param>
        /// <param name="panelColumn">Which screen column the panel starts at.</param>
        /// <param name="panelWidth">How wide the panel is.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The finished row.</returns>
        private static string Overlay(TextRow row, IReadOnlyList<string> panel, int panelRow, int panelColumn,
            int panelWidth, int width)
        {
            if (panelRow < 0 || panelRow >= panel.Count || panelWidth <= 0)
                return row.Render();

            return row.Render(0, panelColumn) + panel[panelRow] +
                   row.Render(panelColumn + panelWidth, width - panelColumn - panelWidth);
        }

        /// <summary>The frame's top edge, with the file name in a tab centred over it.</summary>
        /// <param name="title">What the tab reads.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The row.</returns>
        private static TextRow TopBorder(string title, int width)
        {
            var inner = Math.Max(0, width - 2);
            var tab = " " + (title ?? string.Empty) + " ";

            if (tab.Length > inner)
                tab = tab.Substring(0, inner);

            var before = Math.Max(0, (inner - tab.Length) / 2);

            return new TextRow()
                .Append('┌', 1, DosTheme.Frame)
                .Append('─', before, DosTheme.Frame)
                .Append(tab, DosTheme.Title)
                .Append('─', Math.Max(0, inner - tab.Length - before), DosTheme.Frame)
                .Append('┐', 1, DosTheme.Frame);
        }

        /// <summary>The lettered column headings, with the cursor's own column picked out.</summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="cursor">The cell the keyboard is on.</param>
        /// <param name="bodyWidth">How wide the body is.</param>
        /// <returns>The row.</returns>
        private static TextRow HeaderRowText(Sheet sheet, TableViewport viewport, CellAddress cursor, int bodyWidth)
        {
            var row = new TextRow().Append('│', 1, DosTheme.Frame);

            // The gutter's own heading is blank: it is the corner where the row numbers meet the column letters.
            row.Append(new string(' ', GutterWidth), DosTheme.Header);

            var widths = sheet.ColumnWidths;
            var visible = viewport.VisibleColumns(widths);

            for (var i = 0; i < visible; i++)
            {
                var column = viewport.FirstColumn + i;
                var style = column == cursor.Column ? DosTheme.HeaderActive : DosTheme.Header;

                row.Append(
                    AnsiText.Fit(CellAddress.ColumnName(column), sheet.GetColumnWidth(column),
                        AnsiHorizontalAlignmentEnum.Center), style);
            }

            row.PadTo(bodyWidth + 1, DosTheme.Header);

            return row.Append('│', 1, DosTheme.Frame);
        }

        /// <summary>One row of cells, with its number down the left and its share of the scrollbar on the right.</summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="screenRow">Which row of the grid, counting from the top of the window.</param>
        /// <param name="cursor">The cell the keyboard is on.</param>
        /// <param name="selection">The selected rectangle.</param>
        /// <param name="pointer">The cell the mouse is over.</param>
        /// <param name="bodyWidth">How wide the body is.</param>
        /// <param name="scrollCell">This row's cell of the scrollbar, already styled.</param>
        /// <returns>The row.</returns>
        private static TextRow GridRow(Sheet sheet, TableViewport viewport, int screenRow, CellAddress cursor,
            CellRange selection, CellAddress? pointer, int bodyWidth, string scrollCell)
        {
            var sheetRow = viewport.RowAt(screenRow);
            var row = new TextRow().Append('│', 1, DosTheme.Frame);

            row.Append(
                AnsiText.Fit((sheetRow + 1).ToString(CultureInfo.InvariantCulture) + " ", GutterWidth,
                    AnsiHorizontalAlignmentEnum.Right),
                sheetRow == cursor.Row ? DosTheme.HeaderActive : DosTheme.Header);

            var widths = sheet.ColumnWidths;
            var visible = viewport.VisibleColumns(widths);
            var drawn = 0;

            while (drawn < visible)
            {
                var column = viewport.FirstColumn + drawn;
                var merge = sheet.MergeAt(sheetRow, column);

                if (merge != null)
                {
                    // Whatever of the merge is on screen, drawn once. When its leftmost cell has been scrolled off
                    // the text comes to rest against the left edge, which is what every spreadsheet does with a
                    // heading too wide for the window.
                    var last = Math.Min(merge.Value.LastColumn, viewport.FirstColumn + visible - 1);
                    var span = 0;

                    for (var i = column; i <= last; i++)
                        span += sheet.GetColumnWidth(i);

                    row.Append(Content(sheet, merge.Value.Anchor, span),
                        Style(merge.Value.Anchor, cursor, selection, pointer));

                    drawn += last - column + 1;
                    continue;
                }

                var address = new CellAddress(sheetRow, column);

                row.Append(Content(sheet, address, sheet.GetColumnWidth(column)),
                    Style(address, cursor, selection, pointer));

                drawn++;
            }

            row.PadTo(bodyWidth + 1, DosTheme.Field);

            return row.Append(scrollCell);
        }

        /// <summary>
        ///     What a cell shows, fitted to its column.
        ///     <para>
        ///         Numbers go to the right and text to the left, which is not decoration: it is what makes a column
        ///         of figures line up at the decimal point and therefore comparable at a glance. The trailing space
        ///         is what keeps one cell's text from touching the next one's.
        ///     </para>
        /// </summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="address">Which cell.</param>
        /// <param name="width">How wide it is drawn.</param>
        /// <returns>The text.</returns>
        private static string Content(Sheet sheet, CellAddress address, int width)
        {
            var value = sheet.GetValue(address);
            var alignment = value.IsNumber || value.IsError
                ? AnsiHorizontalAlignmentEnum.Right
                : AnsiHorizontalAlignmentEnum.Left;

            return AnsiText.Fit(value.Display(), Math.Max(1, width - 1), alignment) + " ";
        }

        /// <summary>
        ///     How a cell is drawn: the cursor, the rest of a swept selection, the cell under the mouse, or plain.
        ///     <para>
        ///         The order is the priority. The cursor wins over the selection because it is inside it, and the
        ///         pointer loses to both, since two identically lit cells would leave no telling which one the
        ///         keyboard is about to type into.
        ///     </para>
        /// </summary>
        /// <param name="address">Which cell.</param>
        /// <param name="cursor">The cell the keyboard is on.</param>
        /// <param name="selection">The selected rectangle.</param>
        /// <param name="pointer">The cell the mouse is over.</param>
        /// <returns>Its style.</returns>
        private static TextStyle Style(CellAddress address, CellAddress cursor, CellRange selection,
            CellAddress? pointer)
        {
            if (address == cursor)
                return DosTheme.Selection;

            if (selection.Contains(address))
                return DosTheme.Highlight;

            if (pointer.HasValue && pointer.Value == address)
                return DosTheme.Pointer;

            return DosTheme.Field;
        }

        /// <summary>The frame's bottom edge, which is where the sideways scrollbar lives.</summary>
        /// <param name="sheet">The grid.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The row.</returns>
        private static string BottomBorder(Sheet sheet, TableViewport viewport, int width)
        {
            var horizontal = new ScrollBar(horizontal: true)
            {
                Length = Math.Max(2, width - 2),
                Total = sheet.ColumnCount,
                Visible = Math.Max(1, viewport.VisibleColumns(sheet.ColumnWidths)),
                Position = viewport.FirstColumn,
                ArrowStyle = DosTheme.ScrollArrow,
                TrackStyle = DosTheme.ScrollTrack,
                ThumbStyle = DosTheme.ScrollThumb
            };

            return DosTheme.Frame.Apply("└") + horizontal.Render() + DosTheme.Frame.Apply("┘");
        }
    }
}
