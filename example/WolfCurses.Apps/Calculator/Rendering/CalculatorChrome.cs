// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.Calculator
{
    /// <summary>
    ///     Assembles the calculator screen: a menu bar, the display, the keys under it, and the paper tape beside
    ///     them.
    ///     <para>
    ///         <b>The menu panel is drawn over the finished rows with <see cref="AnsiText.Slice" />, not built out
    ///         of runs.</b> That is the difference from the other two screens here, and it is forced: the keys and
    ///         the boxes arrive as strings a widget has already styled, so there are no runs left to slice. Slicing
    ///         a finished row is safe only because that method walks the escape grammar rather than counting
    ///         characters, which is the whole reason it exists.
    ///     </para>
    /// </summary>
    internal static class CalculatorChrome
    {
        /// <summary>The screen row the menu bar is drawn on.</summary>
        public const int BarRow = 1;

        /// <summary>The screen row the display's top border is on, which is where an open menu panel starts.</summary>
        public const int DisplayRow = 2;

        /// <summary>How many rows the display box costs: two borders, the number, and the indicators.</summary>
        public const int DisplayRows = 4;

        /// <summary>The screen row the keypad's top border is on.</summary>
        public const int KeypadRow = DisplayRow + DisplayRows;

        /// <summary>How many columns separate the keys from the tape.</summary>
        public const int Gutter = 2;

        /// <summary>Composes the screen.</summary>
        /// <param name="menuBar">The menu bar, already told how wide it is.</param>
        /// <param name="keypad">The keys, already positioned.</param>
        /// <param name="engine">The calculator itself.</param>
        /// <param name="status">The key-hint strip's text.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The whole screen, newline separated.</returns>
        public static string Compose(MenuBar menuBar, Keypad keypad, CalculatorEngine engine, string status,
            int width)
        {
            var sb = new StringBuilder();

            sb.Append(menuBar.RenderTitleBar(width)).Append(Environment.NewLine);

            var left = new List<string>(Display(engine, keypad.Width));
            left.AddRange(keypad.Render());

            var tapeWidth = Math.Max(12, width - keypad.Width - Gutter);
            var right = Tape(engine, tapeWidth, left.Count);

            var panel = menuBar.IsOpen ? menuBar.DropdownRows() : (IReadOnlyList<string>) Array.Empty<string>();
            var panelWidth = Math.Min(menuBar.DropdownWidth, width);
            var panelColumn = panelWidth <= 0
                ? 0
                : Math.Clamp(menuBar.DropdownColumn, 0, Math.Max(0, width - panelWidth));

            var gutter = DosTheme.Field.Apply(new string(' ', Gutter));

            for (var i = 0; i < left.Count; i++)
            {
                var row = left[i] + gutter + right[i];

                // The panel's first row lands on the display's top border, which is the row the bar hangs from.
                sb.Append(Overlay(row, panel, i, panelColumn, panelWidth, width)).Append(Environment.NewLine);
            }

            sb.Append(DosTheme.Status.Apply(AnsiText.Fit(status, width)));

            return sb.ToString();
        }

        /// <summary>How many rows the whole screen occupies, which is what a caller sizing the console needs.</summary>
        /// <param name="keypad">The keys.</param>
        /// <returns>The row count, the scene graph's own status line excluded.</returns>
        public static int Rows(Keypad keypad)
        {
            // Bar, display, keys, hints. Fixed rather than fitted, because a calculator's keys are the same size
            // whatever the window is: there is nothing here that would sensibly grow into a taller terminal.
            return 1 + DisplayRows + keypad.Height + 1;
        }

        /// <summary>Draws a row with the menu panel over the top of it, when the panel reaches that row.</summary>
        /// <param name="row">The finished row.</param>
        /// <param name="panel">The panel's rows, or none when no menu is open.</param>
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
        ///     The display: the number, and underneath it the two things a calculator has to admit to.
        ///     <para>
        ///         The pending operator and the memory light are not decoration. A calculator working left to right
        ///         is holding a half-finished sum that nothing else on screen would show, and a number in the
        ///         memory that the display has forgotten is exactly how somebody recalls the wrong figure an hour
        ///         later.
        ///     </para>
        /// </summary>
        /// <param name="engine">The calculator.</param>
        /// <param name="width">How wide the box is.</param>
        /// <returns>The box's rows.</returns>
        private static IReadOnlyList<string> Display(CalculatorEngine engine, int width)
        {
            var inner = Math.Max(1, width - 2);
            var style = engine.Error == null ? DosTheme.Field : DosTheme.Selection;

            var rows = new List<string>
            {
                DosTheme.Frame.Apply("┌" + new string('─', inner) + "┐"),

                new TextRow()
                    .Append("│", DosTheme.Frame)
                    .Append(AnsiText.Fit(" " + engine.Display + " ", inner, AnsiHorizontalAlignmentEnum.Right),
                        style)
                    .Append("│", DosTheme.Frame)
                    .Render(),

                new TextRow()
                    .Append("│", DosTheme.Frame)
                    .Append(AnsiText.Fit(engine.HasMemory ? "  M" : "   ", inner / 2), DosTheme.Title)
                    .Append(
                        AnsiText.Fit(CalculatorEngine.Symbol(engine.Pending) + " ", inner - inner / 2,
                            AnsiHorizontalAlignmentEnum.Right), DosTheme.Title)
                    .Append("│", DosTheme.Frame)
                    .Render(),

                DosTheme.Frame.Apply("└" + new string('─', inner) + "┘")
            };

            return rows;
        }

        /// <summary>
        ///     The paper tape, newest at the bottom, showing as much as fits.
        ///     <para>
        ///         It shows the <i>last</i> lines rather than the first, which is the only way round that stays
        ///         useful: what somebody wants to check is what they just did.
        ///     </para>
        /// </summary>
        /// <param name="engine">The calculator.</param>
        /// <param name="width">How wide the box is.</param>
        /// <param name="height">How many rows it must fill, borders included.</param>
        /// <returns>The box's rows.</returns>
        private static IReadOnlyList<string> Tape(CalculatorEngine engine, int width, int height)
        {
            var inner = Math.Max(1, width - 2);
            var visible = Math.Max(0, height - 2);
            var first = Math.Max(0, engine.Tape.Count - visible);

            var rows = new List<string>
            {
                DosTheme.Frame.Apply("┌") +
                DosTheme.Title.Apply(AnsiText.Fit(" Tape ", Math.Min(inner, 6))) +
                DosTheme.Frame.Apply(new string('─', Math.Max(0, inner - 6)) + "┐")
            };

            for (var i = 0; i < visible; i++)
            {
                var at = first + i;

                if (at >= engine.Tape.Count)
                {
                    rows.Add(new TextRow()
                        .Append("│", DosTheme.Frame)
                        .Append(' ', inner, DosTheme.Field)
                        .Append("│", DosTheme.Frame)
                        .Render());

                    continue;
                }

                var line = engine.Tape[at];

                rows.Add(new TextRow()
                    .Append("│", DosTheme.Frame)
                    .Append(" ", DosTheme.Field)
                    .Append(AnsiText.Fit(line.Value, Math.Max(1, inner - 5), AnsiHorizontalAlignmentEnum.Right),
                        line.IsTotal ? DosTheme.Title : DosTheme.Field)
                    .Append(" ", DosTheme.Field)
                    .Append(AnsiText.Fit(line.Mark, 2), DosTheme.Frame)
                    .Append(" ", DosTheme.Field)
                    .Append("│", DosTheme.Frame)
                    .Render());
            }

            rows.Add(DosTheme.Frame.Apply("└" + new string('─', inner) + "┘"));

            return rows;
        }
    }
}
