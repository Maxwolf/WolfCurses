// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Draws the file/folder browser and turns the user's typed input into navigation. A page of entries is shown
    ///     numbered 1..n; the user types a number to open an entry (navigate into a drive/folder, or choose a file),
    ///     or a letter command to move up, list drives, page, select the current folder, or cancel.
    /// </summary>
    [ParentWindow(typeof (FileDialogWindow))]
    public sealed class FileDialogForm : Form<FileDialogData>
    {
        /// <summary>How many entries are shown per page.</summary>
        private const int PageSize = 12;

        /// <summary>Initializes a new instance of the <see cref="FileDialogForm" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public FileDialogForm(IWindow window) : base(window)
        {
        }

        private FileDialogData Data => UserData;

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "Type a number or a letter command:";

            var data = Data;
            var sb = new StringBuilder();
            sb.AppendLine();

            if (data == null || !data.Initialized)
            {
                sb.Append("Opening browser...");
                return sb.ToString();
            }

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
                    sb.AppendLine($"  {i + 1,2}. {Describe(data.Entries[start + i])}");
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
                return;

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                SelectByNumber(data, number);
                return;
            }

            switch (input.ToUpperInvariant())
            {
                case "U":
                    data.GoUp();
                    break;
                case "D":
                    data.LoadDrives();
                    break;
                case "N":
                    data.PageIndex = ClampPage(data.PageIndex + 1, TotalPages(data));
                    break;
                case "P":
                    data.PageIndex = ClampPage(data.PageIndex - 1, TotalPages(data));
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
            var index = page * PageSize + (number - 1);
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
                    break;
                case FileDialogEntryKindEnum.Drive:
                case FileDialogEntryKindEnum.Directory:
                    data.LoadDirectory(entry.FullPath);
                    break;
                case FileDialogEntryKindEnum.File:
                    Confirm(data, entry.FullPath);
                    break;
            }
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
