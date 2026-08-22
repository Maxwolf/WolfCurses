// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     Every card at once, as a table, showing whichever fields have been asked for.
    ///     <para>
    ///         <b>The columns are as wide as their contents and no wider</b>, which is why the scrolling is the
    ///         library's <see cref="TableViewport" /> rather than arithmetic here: how far a sideways step moves
    ///         depends on the widths of the columns it scrolls off, so it cannot be done by subtraction. Choosing
    ///         a different set of fields to show changes every width at once, which is the case that makes the
    ///         point.
    ///     </para>
    ///     <para>
    ///         <b>A value is flattened here and wrapped on the card</b>, deliberately opposite treatments of the
    ///         same field. A note may hold line breaks, and a row of a table is a row: written raw it would push
    ///         everything after it onto the line below, over whatever was drawn there, which is exactly the bleed
    ///         the word processor's control pictures exist to stop. The card has room to wrap it and this does not.
    ///     </para>
    /// </summary>
    internal static class CardListView
    {
        /// <summary>The screen row the column headings are drawn on.</summary>
        public const int HeaderRow = CardChrome.BodyRow + 1;

        /// <summary>The screen row the first card is drawn on.</summary>
        public const int FirstRow = HeaderRow + 1;

        /// <summary>The screen column the table's first column starts in, one past the box's border.</summary>
        public const int TableColumn = 1;

        /// <summary>The widest a column is allowed to get, however long the values in it are.</summary>
        private const int MaxColumnWidth = 22;

        /// <summary>How many blank columns pad a cell, one either side of its text.</summary>
        private const int Gutter = 2;

        /// <summary>How many cards the table can show at once.</summary>
        /// <param name="height">How many rows the body gets.</param>
        /// <returns>The visible row count, never less than one.</returns>
        public static int VisibleRows(int height)
        {
            return Math.Max(1, height - 3);
        }

        /// <summary>How wide the table is, which is the box less its borders and the scrollbar.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The table's width.</returns>
        public static int TableWidth(int width)
        {
            return Math.Max(1, width - 3);
        }

        /// <summary>
        ///     How wide each shown column has to be: the widest thing in it, its heading included, capped so one
        ///     long address cannot push everything else off the screen.
        /// </summary>
        /// <param name="deck">The cards.</param>
        /// <param name="shown">Which fields are shown, in order.</param>
        /// <returns>The widths, gutters included.</returns>
        public static int[] ColumnWidths(CardDeck deck, IReadOnlyList<int> shown)
        {
            var widths = new int[shown.Count];

            for (var i = 0; i < shown.Count; i++)
            {
                var field = shown[i];
                var width = Card.FieldNames[field].Length;

                foreach (var card in deck.Cards)
                    width = Math.Max(width, Flatten(card[field]).Length);

                widths[i] = Math.Min(width, MaxColumnWidth) + Gutter;
            }

            return widths;
        }

        /// <summary>Draws the view.</summary>
        /// <param name="deck">The cards.</param>
        /// <param name="shown">Which fields are shown, in order.</param>
        /// <param name="widths">How wide each of those is.</param>
        /// <param name="viewport">Where the table has been scrolled to.</param>
        /// <param name="selected">Which card the cursor is on.</param>
        /// <param name="heading">What is notched into the left of the top edge.</param>
        /// <param name="counter">What is notched into the right of it.</param>
        /// <param name="width">The console width.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <returns>The view's rows.</returns>
        public static IReadOnlyList<string> Render(CardDeck deck, IReadOnlyList<int> shown, IReadOnlyList<int> widths,
            TableViewport viewport, int selected, string heading, string counter, int width, int height)
        {
            var table = TableWidth(width);
            var visible = VisibleRows(height);

            var rows = new List<string>(height)
            {
                CardChrome.Titled(heading, counter, width),
                CardChrome.Framed(Headings(shown, widths, viewport, table) + DosTheme.Header.Apply(" "))
            };

            var bar = new ScrollBar
            {
                Length = visible,
                Total = Math.Max(deck.Count, visible),
                Visible = visible,
                Position = viewport.FirstRow,
                ArrowStyle = DosTheme.ScrollArrow,
                TrackStyle = DosTheme.ScrollTrack,
                ThumbStyle = DosTheme.ScrollThumb
            };

            var cells = bar.Cells();

            for (var i = 0; i < visible; i++)
            {
                var at = viewport.RowAt(i);

                rows.Add(CardChrome.Framed(
                    (at >= 0 && at < deck.Count
                        ? Row(deck.Cards[at], shown, widths, viewport, table, at == selected)
                        : DosTheme.Field.Apply(new string(' ', table))) + cells[i]));
            }

            rows.Add(CardChrome.Bottom(width));

            return rows;
        }

        /// <summary>Which card is drawn on a screen row, or -1 for the headings, the borders and the blanks.</summary>
        /// <param name="deck">The cards.</param>
        /// <param name="viewport">Where the table has been scrolled to.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <param name="row">The screen row pressed.</param>
        /// <returns>The card's index, or -1.</returns>
        public static int CardAt(CardDeck deck, TableViewport viewport, int height, int row)
        {
            var offset = row - FirstRow;

            if (offset < 0 || offset >= VisibleRows(height))
                return -1;

            var at = viewport.RowAt(offset);

            return at >= 0 && at < deck.Count ? at : -1;
        }

        /// <summary>Draws the column headings.</summary>
        /// <param name="shown">Which fields are shown.</param>
        /// <param name="widths">How wide each is.</param>
        /// <param name="viewport">Where the table has been scrolled to.</param>
        /// <param name="table">How wide the table is.</param>
        /// <returns>The row's inside, less the scrollbar cell.</returns>
        private static string Headings(IReadOnlyList<int> shown, IReadOnlyList<int> widths, TableViewport viewport,
            int table)
        {
            var line = new TextRow();
            var drawn = 0;

            foreach (var column in Drawn(shown, widths, viewport))
            {
                line.Append(AnsiText.Fit(" " + Card.FieldNames[shown[column]], widths[column]), DosTheme.Header);
                drawn += widths[column];
            }

            return line.Append(' ', table - drawn, DosTheme.Header).Render();
        }

        /// <summary>Draws one card's row.</summary>
        /// <param name="card">The card.</param>
        /// <param name="shown">Which fields are shown.</param>
        /// <param name="widths">How wide each is.</param>
        /// <param name="viewport">Where the table has been scrolled to.</param>
        /// <param name="table">How wide the table is.</param>
        /// <param name="chosen">Whether the cursor is on this card.</param>
        /// <returns>The row's inside, less the scrollbar cell.</returns>
        private static string Row(Card card, IReadOnlyList<int> shown, IReadOnlyList<int> widths,
            TableViewport viewport, int table, bool chosen)
        {
            var style = chosen ? DosTheme.Selection : DosTheme.Field;
            var line = new TextRow();
            var drawn = 0;

            foreach (var column in Drawn(shown, widths, viewport))
            {
                line.Append(Cell(Flatten(card[shown[column]]), widths[column]), style);
                drawn += widths[column];
            }

            return line.Append(' ', table - drawn, style).Render();
        }

        /// <summary>Which columns are on screen, asked of the viewport so drawing and hit testing agree.</summary>
        /// <param name="shown">Which fields are shown.</param>
        /// <param name="widths">How wide each is.</param>
        /// <param name="viewport">Where the table has been scrolled to.</param>
        /// <returns>The column indexes to draw, left to right.</returns>
        private static IEnumerable<int> Drawn(IReadOnlyList<int> shown, IReadOnlyList<int> widths,
            TableViewport viewport)
        {
            var visible = viewport.VisibleColumns(widths);

            for (var i = 0; i < visible; i++)
            {
                var column = viewport.FirstColumn + i;

                if (column >= 0 && column < shown.Count)
                    yield return column;
            }
        }

        /// <summary>
        ///     One cell's text, padded to its column and marked when it had to be cut.
        ///     <para>
        ///         The mark is the same courtesy <see cref="FieldList.Overflow" /> pays: a cell that simply stops
        ///         mid-word says nothing about whether there was more, and here there always might be, since the
        ///         column is capped. The card view is where the whole of it is.
        ///     </para>
        /// </summary>
        /// <param name="value">The value, already flattened.</param>
        /// <param name="width">The column's width, gutters included.</param>
        /// <returns>The cell.</returns>
        private static string Cell(string value, int width)
        {
            var room = Math.Max(0, width - Gutter);

            if (value.Length > room && room > 0)
                value = value.Substring(0, room - 1) + '…';

            return AnsiText.Fit(" " + value, width);
        }

        /// <summary>
        ///     A value with its line breaks turned into spaces, so a note stays on the row it was drawn on. Used
        ///     for measuring as well as for drawing, or a column would be sized for text nobody sees.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The value on one line.</returns>
        public static string Flatten(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\n', ' ')
                .Replace('\r', ' ');
        }
    }
}
