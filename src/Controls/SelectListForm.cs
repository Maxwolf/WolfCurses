// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Draws the selection list (framed with a <see cref="Box" />) and turns typed input into navigation and
    ///     selection. A page of options is numbered 1..n; a number chooses (single-select) or toggles a checkbox
    ///     (multi-select), and letter commands page, confirm, select all/none, or cancel.
    /// </summary>
    [ParentWindow(typeof (SelectListWindow))]
    public sealed class SelectListForm : Form<SelectListData>
    {
        /// <summary>How many options are shown per page.</summary>
        private const int PageSize = 10;

        /// <summary>Initializes a new instance of the <see cref="SelectListForm" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SelectListForm(IWindow window) : base(window)
        {
        }

        private SelectListData Data => UserData;

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            var data = Data;
            if (data == null || !data.Initialized)
            {
                ParentWindow.PromptText = "Opening...";
                return Environment.NewLine + "Opening list...";
            }

            var total = data.Options.Count;
            var totalPages = TotalPages(total);
            var page = ClampPage(data.PageIndex, totalPages);
            var start = page * PageSize;

            var body = new StringBuilder();
            if (total == 0)
            {
                body.AppendLine("(nothing to choose)");
            }
            else
            {
                for (var i = 0; i < PageSize && start + i < total; i++)
                {
                    var index = start + i;
                    var checkbox = data.MultiSelect ? data.IsSelected(index) ? "[x] " : "[ ] " : string.Empty;
                    body.AppendLine($"{i + 1,2}. {checkbox}{data.Options[index]}");
                }
            }

            if (totalPages > 1)
            {
                body.AppendLine();
                body.Append($"Page {page + 1}/{totalPages}");
            }

            var title = string.IsNullOrEmpty(data.Title) ? data.MultiSelect ? "Select" : "Choose" : data.Title;
            var boxed = new Box {Title = title, Padding = 0}.Render(body.ToString().TrimEnd('\r', '\n'));

            ParentWindow.PromptText = BuildPrompt(data, totalPages);

            return Environment.NewLine + boxed;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            var data = Data;
            if (data == null || !data.Initialized)
                return;

            input = (input ?? string.Empty).Trim();
            if (input.Length == 0)
                return;

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                SelectByNumber(data, number);
                return;
            }

            switch (input.ToUpperInvariant())
            {
                case "N":
                    data.PageIndex = ClampPage(data.PageIndex + 1, TotalPages(data.Options.Count));
                    break;
                case "P":
                    data.PageIndex = ClampPage(data.PageIndex - 1, TotalPages(data.Options.Count));
                    break;
                case "S":
                    if (data.MultiSelect)
                        Confirm(data, data.SelectedIndices());
                    break;
                case "A":
                    if (data.MultiSelect)
                        data.SelectAll();
                    break;
                case "X":
                    if (data.MultiSelect)
                        data.SelectNone();
                    break;
                case "C":
                    Cancel(data);
                    break;
            }
        }

        /// <summary>Acts on the visible option the user referenced by its on-screen number.</summary>
        private void SelectByNumber(SelectListData data, int number)
        {
            // Only the numbers actually drawn on the current page (1..PageSize) are valid, so a number like 11 on a
            // full page can never resolve to a hidden option on the next page.
            if (number < 1 || number > PageSize)
                return;

            var page = ClampPage(data.PageIndex, TotalPages(data.Options.Count));
            var index = page * PageSize + (number - 1);
            if (index < 0 || index >= data.Options.Count)
                return;

            if (data.MultiSelect)
                data.Toggle(index);
            else
                Confirm(data, new[] {index});
        }

        /// <summary>Reports the chosen indices to the caller and closes the dialog.</summary>
        private void Confirm(SelectListData data, IReadOnlyList<int> indices)
        {
            var callback = data.OnChosen;
            ParentWindow.RemoveWindowNextTick();
            callback?.Invoke(indices);
        }

        /// <summary>Reports cancellation to the caller and closes the dialog.</summary>
        private void Cancel(SelectListData data)
        {
            var callback = data.OnCancelled;
            ParentWindow.RemoveWindowNextTick();
            callback?.Invoke();
        }

        private static string BuildPrompt(SelectListData data, int totalPages)
        {
            var paging = totalPages > 1 ? "  [N]ext  [P]rev" : string.Empty;
            return data.MultiSelect
                ? $"# toggles,  [S]elect  [A]ll  [X]clear{paging}  [C]ancel:"
                : $"Type a number to choose{paging},  [C]ancel:";
        }

        private static int TotalPages(int count)
        {
            return Math.Max(1, (count + PageSize - 1) / PageSize);
        }

        private static int ClampPage(int page, int totalPages)
        {
            if (page < 0)
                return 0;
            if (page >= totalPages)
                return totalPages - 1;
            return page;
        }
    }
}
