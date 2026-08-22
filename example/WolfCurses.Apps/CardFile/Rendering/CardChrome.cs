// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     Assembles the card file screen: the menu bar, the row of letter tabs, whichever view is showing, and the
    ///     key hints along the bottom. Owns the pieces the two views share.
    ///     <para>
    ///         <b>Both views are exactly the same height</b>, so switching between them moves nothing on screen,
    ///         the same rule the planner's four follow and the same reason the month grid always draws six weeks.
    ///     </para>
    ///     <para>
    ///         The menu panel is drawn over the finished rows with <see cref="AnsiText.Slice" />, as the calculator
    ///         and the planner do it: what arrives here has already been styled by a widget, so there are no runs
    ///         left to slice and the escape grammar has to be walked instead. <b>Rows are padded to the full width
    ///         first</b>, because the letter tabs are narrower than the screen and a slice of a short row hands
    ///         back a short row, which would put the panel at the wrong column rather than out of range.
    ///     </para>
    /// </summary>
    internal static class CardChrome
    {
        /// <summary>The screen row the menu bar is drawn on.</summary>
        public const int BarRow = 1;

        /// <summary>The screen row the letter tabs start on, which is where an open menu panel starts too.</summary>
        public const int TabRow = 2;

        /// <summary>How many rows the letter tabs occupy: their faces, and a rule above and below.</summary>
        public const int TabRows = 3;

        /// <summary>The screen column the letter tabs' left edge is in.</summary>
        public const int TabColumn = 0;

        /// <summary>The screen row the body's box starts on.</summary>
        public const int BodyRow = TabRow + TabRows;

        /// <summary>How many rows the body gets, whichever view is showing.</summary>
        public const int BodyRows = 15;

        /// <summary>The screen row the card's first field is drawn on, one inside the box's top edge.</summary>
        public const int FieldRow = BodyRow + 1;

        /// <summary>The screen column the card's fields start in, one past the box's border.</summary>
        public const int FieldColumn = 1;

        /// <summary>Composes the screen.</summary>
        /// <param name="menuBar">The menu bar.</param>
        /// <param name="tabs">The letter tabs, already positioned.</param>
        /// <param name="view">Which way the card file is being looked at.</param>
        /// <param name="deck">The cards.</param>
        /// <param name="fields">The chosen card's fields, already filled in and positioned.</param>
        /// <param name="shown">Which fields the list shows as columns.</param>
        /// <param name="widths">How wide each of those columns is.</param>
        /// <param name="viewport">Where the list has been scrolled to.</param>
        /// <param name="selected">Which card the cursor is on.</param>
        /// <param name="heading">What is notched into the left of the box's top edge.</param>
        /// <param name="status">The key-hint strip's text.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The whole screen, newline separated.</returns>
        public static string Compose(MenuBar menuBar, Keypad tabs, CardViewEnum view, CardDeck deck,
            FieldList fields, IReadOnlyList<int> shown, IReadOnlyList<int> widths, TableViewport viewport,
            int selected, string heading, string status, int width)
        {
            var sb = new StringBuilder();

            sb.Append(menuBar.RenderTitleBar(width)).Append(Environment.NewLine);

            var rows = new List<string>(TabRows + BodyRows);

            // The tabs are narrower than the console, and what goes in the space beside them is what says the
            // strip is a thing you can use. Discoverability rather than decoration: a row of letters explains
            // itself to nobody.
            var legend = new[]
            {
                deck.Count == 1 ? "1 card" : deck.Count + " cards",
                "Type a letter to",
                "flip to that tab."
            };

            var strip = tabs.Render();

            for (var i = 0; i < strip.Count; i++)
                rows.Add(Fill(strip[i] + "  " + legend[i], width));

            var counter = deck.Count == 0
                ? "no cards"
                : selected + 1 + " of " + deck.Count;

            rows.AddRange(view == CardViewEnum.List
                ? CardListView.Render(deck, shown, widths, viewport, selected, heading, counter, width, BodyRows)
                : CardView.Render(fields, heading, counter, width, BodyRows));

            var panel = menuBar.IsOpen ? menuBar.DropdownRows() : (IReadOnlyList<string>) Array.Empty<string>();
            var panelWidth = Math.Min(menuBar.DropdownWidth, width);
            var panelColumn = panelWidth <= 0
                ? 0
                : Math.Clamp(menuBar.DropdownColumn, 0, Math.Max(0, width - panelWidth));

            for (var i = 0; i < rows.Count; i++)
                sb.Append(Overlay(rows[i], panel, i, panelColumn, panelWidth, width)).Append(Environment.NewLine);

            sb.Append(DosTheme.Status.Apply(AnsiText.Fit(status, width)));

            return sb.ToString();
        }

        /// <summary>Draws a row with the menu panel over the top of it, when the panel reaches that row.</summary>
        /// <param name="row">The finished row.</param>
        /// <param name="panel">The panel's rows.</param>
        /// <param name="panelRow">Which of the panel's rows belongs on this screen row.</param>
        /// <param name="panelColumn">Which screen column the panel starts at.</param>
        /// <param name="panelWidth">How wide the panel is.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The finished row.</returns>
        private static string Overlay(string row, IReadOnlyList<string> panel, int panelRow, int panelColumn,
            int panelWidth, int width)
        {
            if (panelRow < 0 || panelRow >= panel.Count || panelWidth <= 0)
                return row;

            return AnsiText.Slice(row, 0, panelColumn) + panel[panelRow] +
                   AnsiText.Slice(row, panelColumn + panelWidth, width - panelColumn - panelWidth);
        }

        /// <summary>
        ///     Makes a row exactly the width of the screen. The letter tabs need it because they are narrower than
        ///     the console, and a slice of a short row hands back a short row: the menu panel would then land at
        ///     whatever column that ended at rather than at the one it was asked for.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The row, exactly that wide.</returns>
        private static string Fill(string row, int width)
        {
            var visible = AnsiText.VisibleLength(row);

            if (visible == width)
                return row;

            return visible < width
                ? row + new string(' ', width - visible)
                : AnsiText.Slice(row, 0, width);
        }

        /// <summary>
        ///     A box's top edge with something notched into each end. The right-hand notch is what the two views
        ///     put their card counter in, and it is trimmed away before the title is when there is no room for
        ///     both: knowing which file is open matters more than knowing which card of how many.
        /// </summary>
        /// <param name="left">What the left-hand tab reads.</param>
        /// <param name="right">What the right-hand tab reads; empty for none.</param>
        /// <param name="width">How wide the box is.</param>
        /// <returns>The row.</returns>
        public static string Titled(string left, string right, int width)
        {
            var inner = Math.Max(0, width - 2);

            var leftTab = Tab(left);
            var rightTab = Tab(right);

            if (leftTab.Length + rightTab.Length > inner)
                rightTab = string.Empty;

            if (leftTab.Length > inner)
                leftTab = leftTab.Substring(0, inner);

            return new TextRow()
                .Append("┌", DosTheme.Frame)
                .Append(leftTab, DosTheme.Title)
                .Append('─', inner - leftTab.Length - rightTab.Length, DosTheme.Frame)
                .Append(rightTab, DosTheme.Title)
                .Append("┐", DosTheme.Frame)
                .Render();
        }

        /// <summary>A box's bottom edge.</summary>
        /// <param name="width">How wide the box is.</param>
        /// <returns>The row.</returns>
        public static string Bottom(int width)
        {
            return DosTheme.Frame.Apply("└" + new string('─', Math.Max(0, width - 2)) + "┘");
        }

        /// <summary>
        ///     One row of a box's inside, bordered either end.
        ///     <para>
        ///         The content arrives <b>already styled</b>, which is why this concatenates rather than building a
        ///         <see cref="TextRow" />: a row's text is plain by contract, and handing it something carrying
        ///         escapes would have it count them as columns. What makes this line up at all is that
        ///         <see cref="FieldList" /> promises every row it draws is exactly as wide as it says.
        ///     </para>
        /// </summary>
        /// <param name="content">The row's inside, already painted.</param>
        /// <returns>The row.</returns>
        public static string Framed(string content)
        {
            return DosTheme.Frame.Apply("│") + content + DosTheme.Frame.Apply("│");
        }

        /// <summary>An empty row of a box's inside.</summary>
        /// <param name="width">How wide the box is.</param>
        /// <returns>The row.</returns>
        public static string Blank(int width)
        {
            return Framed(DosTheme.Field.Apply(new string(' ', Math.Max(0, width - 2))));
        }

        /// <summary>Wraps a notch's text in spaces, or gives nothing back for nothing.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The notch.</returns>
        private static string Tab(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : " " + text + " ";
        }
    }
}
