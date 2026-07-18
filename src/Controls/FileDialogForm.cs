// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Draws the file/folder browser and turns input into navigation. Two input styles are both always live: the
    ///     arrow keys move a highlight over the entries ncurses-style and ENTER opens the highlighted one (descending
    ///     into a drive or folder, or choosing a file), while typed input keeps its original meanings — a number
    ///     opens an entry by its on-screen label, and letter commands move up, list drives, page, select the current
    ///     folder, or cancel.
    ///     <para>
    ///         The highlight index is global over the whole entry list and the displayed page is derived from it, so
    ///         walking off the bottom of a page turns the page. Whenever the directory changes — by any style — the
    ///         cursor resets to the first entry of the new listing, because a highlight carried across a directory
    ///         change would point at whatever coincidentally occupies its old position.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (FileDialogWindow))]
    public sealed class FileDialogForm : Form<FileDialogData>
    {
        /// <summary>How many entries are shown per page.</summary>
        private const int PageSize = 12;

        /// <summary>
        ///     The arrow-key highlight over the entries, visible from the first frame like every modal control's.
        /// </summary>
        private readonly ListNavigator _navigator = new() {PageSize = PageSize};

        /// <summary>Initializes a new instance of the <see cref="FileDialogForm" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public FileDialogForm(IWindow window) : base(window)
        {
        }

        private FileDialogData Data => UserData;

        /// <summary>Keeps the highlight sized to the entry list and visible from the start.</summary>
        private void SyncHighlight(FileDialogData data)
        {
            _navigator.Resize(data.Entries.Count);
            if (!_navigator.HasSelection && data.Entries.Count > 0)
                _navigator.Select(0);
        }

        /// <summary>
        ///     Parks the cursor on the first entry of a freshly revealed listing. The data object already reset its
        ///     page; the highlight must follow, or ENTER would open whatever sits where the cursor used to be.
        /// </summary>
        private void ResetHighlight(FileDialogData data)
        {
            _navigator.Resize(data.Entries.Count);
            if (data.Entries.Count > 0)
                _navigator.Select(0);
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            var data = Data;
            if (data == null || !data.Initialized)
                return;

            SyncHighlight(data);
            if (_navigator.HandleKey(key) && _navigator.HasSelection)
            {
                // The page follows the highlight, so walking off the bottom of a page turns it.
                data.PageIndex = _navigator.Index / PageSize;
            }
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "ENTER opens the highlighted, or type a number / letter command:";

            var data = Data;
            var sb = new StringBuilder();
            sb.AppendLine();

            if (data == null || !data.Initialized)
            {
                sb.Append("Opening browser...");
                return sb.ToString();
            }

            SyncHighlight(data);

            sb.AppendLine(data.Mode == FileDialogModeEnum.SelectFolder ? "Select Folder" : "Open File");
            if (data.CurrentDirectory == null)
            {
                sb.AppendLine("Choose a drive:");
            }
            else
            {
                sb.AppendLine($"Folder: {data.CurrentDirectory}");
                if (data.Mode == FileDialogModeEnum.OpenFile)
                    sb.AppendLine($"Showing: {FilterText(data)}");
            }

            sb.AppendLine();

            var totalPages = TotalPages(data);
            var page = ClampPage(data.PageIndex, totalPages);
            var start = page * PageSize;
            if (data.Entries.Count == 0)
            {
                sb.AppendLine("  (nothing to show here)");
            }
            else
            {
                for (var i = 0; i < PageSize && start + i < data.Entries.Count; i++)
                {
                    var index = start + i;
                    var row = $"{i + 1,2}. {Describe(data.Entries[index])}";
                    sb.AppendLine(ListNavigator.DecorateRow(row,
                        _navigator.HasSelection && _navigator.Index == index));
                }
            }

            sb.AppendLine();

            if (!string.IsNullOrEmpty(data.ErrorMessage))
                sb.AppendLine(data.ErrorMessage);

            var help = new StringBuilder($"Page {page + 1}/{totalPages}   [U]p  [D]rives  ");
            if (totalPages > 1)
                help.Append("[N]ext  [P]rev  ");
            if (data.Mode == FileDialogModeEnum.SelectFolder && data.CurrentDirectory != null)
                help.Append("[S]elect this folder  ");
            help.Append("[C]ancel");
            sb.Append(help);

            return sb.ToString();
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
                // ENTER with nothing typed opens the highlighted entry — the browsing gesture every curses file
                // browser teaches: Down, Down, ENTER, deeper.
                SyncHighlight(data);
                if (_navigator.HasSelection)
                    OpenEntry(data, _navigator.Index);
                return;
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                SelectByNumber(data, number);
                return;
            }

            switch (input.ToUpperInvariant())
            {
                case "U":
                    data.GoUp();
                    ResetHighlight(data);
                    break;
                case "D":
                    data.LoadDrives();
                    ResetHighlight(data);
                    break;
                case "N":
                    TurnToPage(data, data.PageIndex + 1);
                    break;
                case "P":
                    TurnToPage(data, data.PageIndex - 1);
                    break;
                case "S":
                    if (data.Mode == FileDialogModeEnum.SelectFolder && data.CurrentDirectory != null)
                        Confirm(data, data.CurrentDirectory);
                    break;
                case "C":
                    Cancel(data);
                    break;
            }
        }

        /// <summary>Acts on the visible entry the user selected by its on-screen number.</summary>
        private void SelectByNumber(FileDialogData data, int number)
        {
            // Only the numbers actually drawn on the current page (1..PageSize) are valid; without this a number like
            // 13 on a full page would resolve to a hidden entry on the next page and silently pick the wrong item.
            if (number < 1 || number > PageSize)
                return;

            var page = ClampPage(data.PageIndex, TotalPages(data));
            OpenEntry(data, page * PageSize + (number - 1));
        }

        /// <summary>
        ///     Opens the entry at a global index — into a drive or folder, up a level, or out of the dialog with a
        ///     chosen file. Shared by the typed numbers and the highlight's ENTER, so both styles cannot drift apart.
        ///     After any branch that changed the listing the cursor is parked back on the first entry.
        /// </summary>
        private void OpenEntry(FileDialogData data, int index)
        {
            if (index < 0 || index >= data.Entries.Count)
                return;

            var entry = data.Entries[index];
            switch (entry.Kind)
            {
                case FileDialogEntryKindEnum.ParentDirectory:
                    if (entry.FullPath != null)
                        data.LoadDirectory(entry.FullPath);
                    else
                        data.LoadDrives();
                    ResetHighlight(data);
                    break;
                case FileDialogEntryKindEnum.Drive:
                case FileDialogEntryKindEnum.Directory:
                    data.LoadDirectory(entry.FullPath);
                    ResetHighlight(data);
                    break;
                case FileDialogEntryKindEnum.File:
                    Confirm(data, entry.FullPath);
                    break;
            }
        }

        /// <summary>
        ///     Turns to the given page (clamped) and parks the highlight on its first row, so the cursor is always on
        ///     a row that is actually on screen.
        /// </summary>
        private void TurnToPage(FileDialogData data, int page)
        {
            data.PageIndex = ClampPage(page, TotalPages(data));
            _navigator.Select(data.PageIndex * PageSize);
        }

        /// <summary>Reports the chosen path to the caller and closes the dialog.</summary>
        private void Confirm(FileDialogData data, string path)
        {
            var callback = data.OnPathSelected;
            ParentWindow.RemoveWindowNextTick();
            callback?.Invoke(path);
        }

        /// <summary>Reports cancellation to the caller and closes the dialog.</summary>
        private void Cancel(FileDialogData data)
        {
            var callback = data.OnCancelled;
            ParentWindow.RemoveWindowNextTick();
            callback?.Invoke();
        }

        private static int TotalPages(FileDialogData data)
        {
            return Math.Max(1, (data.Entries.Count + PageSize - 1) / PageSize);
        }

        private static int ClampPage(int page, int totalPages)
        {
            if (page < 0)
                return 0;
            if (page >= totalPages)
                return totalPages - 1;
            return page;
        }

        private static string FilterText(FileDialogData data)
        {
            return data.Extensions.Length == 0 ? "all files" : "files: " + string.Join(", ", data.Extensions);
        }

        private static string Describe(FileDialogEntry entry)
        {
            switch (entry.Kind)
            {
                case FileDialogEntryKindEnum.ParentDirectory:
                    return ".. (up one level)";
                case FileDialogEntryKindEnum.Drive:
                    return $"[drive] {entry.Name}";
                case FileDialogEntryKindEnum.Directory:
                    return $"[dir]  {entry.Name}";
                default:
                    return entry.Name;
            }
        }
    }
}
