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
    ///     Draws the selection list (framed with a <see cref="Box" />) and turns input into navigation and selection.
    ///     Two input styles are first-class and always both live: the arrow keys move a highlight ncurses-style —
    ///     ENTER chooses it (single-select) and SPACE toggles its checkbox (multi-select) — while typed input keeps
    ///     its original meanings, a number choosing/toggling by its on-screen label and letter commands paging,
    ///     confirming, selecting all/none, or cancelling.
    ///     <para>
    ///         The highlight index is global (0..options-1) and the displayed page is derived from it, so walking off
    ///         the bottom of a page turns the page and PageUp/PageDown jump a page at a time; the typed paging
    ///         commands move the highlight to the top of the page they reveal so the cursor is always on a row that
    ///         is actually on screen.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (SelectListWindow))]
    public sealed class SelectListForm : Form<SelectListData>
    {
        /// <summary>How many options are shown per page.</summary>
        private const int PageSize = 10;

        /// <summary>
        ///     The arrow-key highlight. Unlike a window menu it is visible from the first frame — a modal picker's
        ///     whole job is to be pointed at, so it opens with the cursor on the first option.
        /// </summary>
        private readonly ListNavigator _navigator = new() {PageSize = PageSize};

        /// <summary>Initializes a new instance of the <see cref="SelectListForm" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SelectListForm(IWindow window) : base(window)
        {
        }

        private SelectListData Data => UserData;

        /// <summary>
        ///     Keeps the highlight sized to the option list and visible from the start. Safe to call every render and
        ///     key press; both do, because the data object is configured after the form exists.
        /// </summary>
        private void SyncHighlight(SelectListData data)
        {
            _navigator.Resize(data.Options.Count);
            if (!_navigator.HasSelection && data.Options.Count > 0)
                _navigator.Select(0);
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            var data = Data;
            if (data == null || !data.Initialized)
                return;

            SyncHighlight(data);
            if (_navigator.HandleKey(key))
            {
                // The page follows the highlight, so walking off the bottom of a page turns it.
                if (_navigator.HasSelection)
                    data.PageIndex = _navigator.Index / PageSize;
                return;
            }

            if (key == ConsoleKey.Spacebar && data.MultiSelect && _navigator.HasSelection)
            {
                data.Toggle(_navigator.Index);

                // The space that meant "toggle" also landed in the input buffer as text — a space is printable, and
                // keys are deliberately offered to both paths — so scrub the buffer rather than leave the prompt
                // quietly filling with invisible spaces (or a half-typed number that no longer means anything).
                SimUnit?.InputManager?.ClearBuffer();
            }
        }

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

            SyncHighlight(data);

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
                    var row = $"{i + 1,2}. {checkbox}{data.Options[index]}";
                    body.AppendLine(ListNavigator.DecorateRow(row,
                        _navigator.HasSelection && _navigator.Index == index));
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
            {
                // ENTER with nothing typed: the highlighted option for single-select, the checked set for multi —
                // the same confirmation [S] performs, because ENTER is what every curses list means by "done".
                SyncHighlight(data);
                if (data.MultiSelect)
                    Confirm(data, data.SelectedIndices());
                else if (_navigator.HasSelection)
                    Confirm(data, new[] {_navigator.Index});
                return;
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                SelectByNumber(data, number);
                return;
            }

            switch (input.ToUpperInvariant())
            {
                case "N":
                    TurnToPage(data, data.PageIndex + 1);
                    break;
                case "P":
                    TurnToPage(data, data.PageIndex - 1);
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
            {
                data.Toggle(index);

                // Pull the highlight onto the toggled row so the two input styles read as one cursor, not two.
                _navigator.Select(index);
            }
            else
            {
                Confirm(data, new[] {index});
            }
        }

        /// <summary>
        ///     Turns to the given page (clamped) and parks the highlight on its first row. The typed paging commands
        ///     reveal a different page than the highlight was on, and a highlight pointing at an off-screen row would
        ///     let ENTER choose something the user cannot see.
        /// </summary>
        private void TurnToPage(SelectListData data, int page)
        {
            data.PageIndex = ClampPage(page, TotalPages(data.Options.Count));
            _navigator.Select(data.PageIndex * PageSize);
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
                ? $"SPACE toggles, ENTER confirms (or #,  [S]elect  [A]ll  [X]clear{paging}),  [C]ancel:"
                : $"ENTER chooses the highlighted (or type a number){paging},  [C]ancel:";
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
