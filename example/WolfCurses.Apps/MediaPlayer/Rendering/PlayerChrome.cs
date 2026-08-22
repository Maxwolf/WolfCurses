// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     Assembles the player screen: the menu bar, a strip saying what is open, the picture, the scrub bar and
    ///     the key hints.
    ///     <para>
    ///         <b>The picture has no frame around it, and cannot have one.</b> A true-pixel picture is a single
    ///         escape blob of no visible width that paints across a dozen rows, so nothing may sit beside it -
    ///         which rules out a border, a scrollbar, a second column, and every other thing the rest of this suite
    ///         puts around its content. It is the one screen here that is full width by necessity rather than by
    ///         taste.
    ///     </para>
    ///     <para>
    ///         <b>A dropped menu and a picture cannot share a row either</b>, for the same reason: a payload row
    ///         cannot be sliced by column. So <see cref="AnsiGraphics.IsPictureRow" /> is asked about every row the
    ///         panel reaches, and a row that belongs to a picture is given over to the menu entirely rather than
    ///         cut in half. On a terminal drawing half blocks the picture is ordinary text and the panel overlays
    ///         it as it does everywhere else in the suite; on one drawing real pixels the picture stands aside for
    ///         as long as the menu is down and comes back when it shuts. The behaviour is different because the
    ///         terminals are, which is more honest than blanking it for everyone.
    ///     </para>
    /// </summary>
    internal static class PlayerChrome
    {
        /// <summary>The screen row the menu bar is drawn on.</summary>
        public const int BarRow = 1;

        /// <summary>The screen row saying what is open, which is where an open menu panel starts too.</summary>
        public const int InfoRow = 2;

        /// <summary>The screen row the picture starts on.</summary>
        public const int StageRow = InfoRow + 1;

        /// <summary>How many rows the picture gets.</summary>
        public const int StageRows = 16;

        /// <summary>The screen row the scrub bar is drawn on.</summary>
        public const int TimelineRow = StageRow + StageRows;

        /// <summary>Composes the screen.</summary>
        /// <param name="menuBar">The menu bar.</param>
        /// <param name="stage">The picture's rows, already exactly <see cref="StageRows" /> of them.</param>
        /// <param name="timeline">The scrub bar, already positioned.</param>
        /// <param name="info">What the strip above the picture says.</param>
        /// <param name="status">The key-hint strip's text.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The whole screen, newline separated.</returns>
        public static string Compose(MenuBar menuBar, IReadOnlyList<string> stage, Timeline timeline, string info,
            string status, int width)
        {
            var sb = new StringBuilder();

            sb.Append(menuBar.RenderTitleBar(width)).Append(Environment.NewLine);

            var rows = new List<string>(StageRows + 2) {DosTheme.Header.Apply(AnsiText.Fit(" " + info, width))};

            rows.AddRange(stage);
            rows.Add(Fill(timeline.Render(), width));

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

        /// <summary>
        ///     Draws a row with the menu panel over the top of it, when the panel reaches that row.
        ///     <para>
        ///         A row belonging to a picture is replaced rather than cut, since cutting one hands the terminal
        ///         half an escape sequence and the rest of it as text.
        ///     </para>
        /// </summary>
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

            if (AnsiGraphics.IsPictureRow(row))
                return DosTheme.Field.Apply(new string(' ', panelColumn)) + panel[panelRow];

            return AnsiText.Slice(row, 0, panelColumn) + panel[panelRow] +
                   AnsiText.Slice(row, panelColumn + panelWidth, width - panelColumn - panelWidth);
        }

        /// <summary>
        ///     Makes a row exactly the width of the screen, and leaves a picture's own rows alone: padding one
        ///     would put spaces beside a payload that must have nothing beside it.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The row.</returns>
        public static string Fill(string row, int width)
        {
            if (AnsiGraphics.IsPictureRow(row))
                return row;

            var visible = AnsiText.VisibleLength(row);

            if (visible == width)
                return row;

            return visible < width
                ? row + new string(' ', width - visible)
                : AnsiText.Slice(row, 0, width);
        }
    }
}
