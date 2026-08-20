// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Text;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     Assembles the whole editor screen the way the MS-DOS Editor laid it out: a menu bar, a framed field with
    ///     the file name notched into its top edge, a scrollbar down the right and along the bottom, and a key-hint
    ///     strip underneath.
    ///     <para>
    ///         Pure composition. Every piece it puts together is a library control that knows nothing about this
    ///         application, and what is left here is the arrangement, which really is application-specific: another
    ///         program would want the same parts in a different order.
    ///     </para>
    ///     <para>
    ///         <b>Every row is padded to the full width.</b> A background colour only covers what is actually
    ///         written, and the presenter erases the rest of the row with whatever the terminal's default is, so a
    ///         row that stops after its last word leaves the blue field ending in a ragged edge.
    ///     </para>
    /// </summary>
    internal static class EditorChrome
    {
        /// <summary>Rows the frame and its furniture cost, which is what the document does not get.</summary>
        public const int ChromeRows = 4;

        /// <summary>Columns the frame costs: the left edge and the scrollbar down the right.</summary>
        public const int ChromeColumns = 2;

        /// <summary>Composes the screen.</summary>
        /// <param name="menuBar">The menu bar, already told how wide it is.</param>
        /// <param name="buffer">The document.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="title">What the frame's tab reads.</param>
        /// <param name="status">The key-hint strip's text.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The whole screen, newline separated.</returns>
        public static string Compose(MenuBar menuBar, TextBuffer buffer, TextViewport viewport, string title,
            string status, int width)
        {
            var sb = new StringBuilder();

            // The bar, and the open panel below it. The panel is drawn over the top of the field rather than beside
            // it, so the rows it covers come out of the document's share (see Rows below).
            sb.Append(menuBar.Render(width));

            var rows = DocumentView.Render(buffer, viewport, DosTheme.Field, DosTheme.Selection, !menuBar.IsOpen);

            // The bar spans the body rows exactly, so its two arrow caps land on the first and last of them. An
            // earlier version made it two cells longer and drew only the middle, which computed both caps and then
            // dropped them off either end.
            var vertical = new ScrollBar
            {
                Length = viewport.Height,
                Total = buffer.LineCount,
                Visible = viewport.Height,
                Position = viewport.FirstLine,
                ArrowStyle = DosTheme.ScrollArrow,
                TrackStyle = DosTheme.ScrollTrack,
                ThumbStyle = DosTheme.ScrollThumb
            };

            var cells = vertical.Cells();

            sb.Append(TopBorder(title, width)).Append(Environment.NewLine);

            for (var row = 0; row < rows.Length; row++)
            {
                sb.Append(DosTheme.Frame.Apply("│"))
                    .Append(rows[row])
                    .Append(cells[row])
                    .Append(Environment.NewLine);
            }

            sb.Append(BottomBorder(buffer, viewport, width)).Append(Environment.NewLine);
            sb.Append(DosTheme.Status.Apply(Fit(status, width)));

            return sb.ToString();
        }

        /// <summary>How many rows of document fit, once the frame and any open menu have taken theirs.</summary>
        /// <param name="consoleHeight">The console height.</param>
        /// <param name="reserved">Rows the scene graph and the prompt take outside this screen.</param>
        /// <param name="dropdownHeight">Rows an open menu panel is covering.</param>
        /// <returns>The document's height, never less than one.</returns>
        public static int Rows(int consoleHeight, int reserved, int dropdownHeight)
        {
            return Math.Max(1, consoleHeight - reserved - ChromeRows - dropdownHeight);
        }

        /// <summary>The frame's top edge, with the file name in a lit tab centred over it.</summary>
        private static string TopBorder(string title, int width)
        {
            var inner = Math.Max(0, width - 2);
            var tab = " " + (title ?? string.Empty) + " ";

            if (tab.Length > inner)
                tab = tab.Substring(0, inner);

            var before = Math.Max(0, (inner - tab.Length) / 2);
            var after = Math.Max(0, inner - tab.Length - before);

            return DosTheme.Frame.Apply("┌" + new string('─', before)) +
                   DosTheme.Title.Apply(tab) +
                   DosTheme.Frame.Apply(new string('─', after) + "┐");
        }

        /// <summary>The frame's bottom edge, which is where the sideways scrollbar lives.</summary>
        private static string BottomBorder(TextBuffer buffer, TextViewport viewport, int width)
        {
            var horizontal = new ScrollBar(horizontal: true)
            {
                Length = Math.Max(2, width - 2),
                Total = WidestLine(buffer, viewport),
                Visible = viewport.Width,
                Position = viewport.FirstColumn,
                ArrowStyle = DosTheme.ScrollArrow,
                TrackStyle = DosTheme.ScrollTrack,
                ThumbStyle = DosTheme.ScrollThumb
            };

            return DosTheme.Frame.Apply("└") + horizontal.Render() + DosTheme.Frame.Apply("┘");
        }

        /// <summary>
        ///     How wide the document is, measured over the lines on screen rather than all of them.
        ///     <para>
        ///         Deliberately a sample. The honest answer needs every line in the file, and the shipped sample is
        ///         four and a half thousand of them being measured for a bar nobody reads that precisely; what the
        ///         sideways thumb has to be is roughly right and stable while you scroll, which the visible window
        ///         gives for nothing.
        ///     </para>
        /// </summary>
        private static int WidestLine(TextBuffer buffer, TextViewport viewport)
        {
            var widest = viewport.Width;

            for (var row = 0; row < viewport.Height; row++)
            {
                var index = viewport.FirstLine + row;
                if (index >= buffer.LineCount)
                    break;

                widest = Math.Max(widest, TabStops.DisplayWidth(buffer.GetLine(index), buffer.TabWidth));
            }

            return widest;
        }

        /// <summary>Pads or trims text to exactly a width, so a styled strip covers its whole row and no more.</summary>
        private static string Fit(string text, int width)
        {
            text ??= string.Empty;

            if (text.Length > width)
                return text.Substring(0, width);

            return text.PadRight(width);
        }
    }
}
