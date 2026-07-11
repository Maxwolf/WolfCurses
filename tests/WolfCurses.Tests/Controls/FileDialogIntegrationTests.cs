using System;
using System.IO;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Drives the file dialog through the real tick and input pipeline (via a minimal host simulation) to prove the
    ///     end-to-end flow: opening the dialog, navigating into a folder, choosing a file, selecting a folder, and
    ///     cancelling — each ultimately firing the right callback and closing the dialog.
    /// </summary>
    public class FileDialogIntegrationTests : IDisposable
    {
        private readonly string _root;

        public FileDialogIntegrationTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "wolfcurses_fdint_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(Path.Combine(_root, "Alpha"));
            File.WriteAllText(Path.Combine(Path.Combine(_root, "Alpha"), "inside.png"), "x");
            File.WriteAllText(Path.Combine(_root, "image.png"), "x");
            File.WriteAllText(Path.Combine(_root, "notes.txt"), "x");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static void Send(SimulationApp app, string command)
        {
            foreach (var c in command)
                app.InputManager.AddCharToInputBuffer(c);
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);
        }

        [Fact]
        public void OpenFile_ChoosingAFile_FiresCallbackWithPathAndClosesDialog()
        {
            var app = new FileDialogHostApp();
            string picked = null;

            // Root (.png filter) lists: 1=.., 2=Alpha (dir), 3=image.png. notes.txt is filtered out.
            FileDialog.OpenFile(app, _root, new[] { ".png" }, path => picked = path);
            app.OnTick(false);

            Send(app, "3"); // choose image.png

            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "image.png")), picked);
            Assert.Null(app.WindowManager.FocusedWindow); // dialog closed, nothing left underneath

            app.Destroy();
        }

        [Fact]
        public void OpenFile_NavigateIntoFolderThenChooseFile()
        {
            var app = new FileDialogHostApp();
            string picked = null;

            FileDialog.OpenFile(app, _root, new[] { ".png" }, path => picked = path);
            app.OnTick(false);

            Send(app, "2"); // navigate into Alpha -> lists 1=.., 2=inside.png
            Send(app, "2"); // choose inside.png

            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Alpha", "inside.png")), picked);

            app.Destroy();
        }

        [Fact]
        public void SelectFolder_SelectCurrentFolder_FiresCallbackWithFolderPath()
        {
            var app = new FileDialogHostApp();
            string picked = null;

            FileDialog.SelectFolder(app, _root, path => picked = path);
            app.OnTick(false);

            Send(app, "2"); // navigate into Alpha (folder mode shows no files)
            Send(app, "S"); // select this folder

            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Alpha")), picked);

            app.Destroy();
        }

        [Fact]
        public void Cancel_FiresCancelCallbackAndClosesDialog()
        {
            var app = new FileDialogHostApp();
            var cancelled = false;

            FileDialog.OpenFile(app, _root, null, _ => { }, () => cancelled = true);
            app.OnTick(false);

            Send(app, "C");

            Assert.True(cancelled);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void OpenFile_WindowNotAllowed_ThrowsHelpfully()
        {
            var app = new TestSimulationApp(); // does not allow FileDialogWindow
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FileDialog.OpenFile(app, _root, null, _ => { }));
            Assert.Contains("AllowedWindows", ex.Message);
            app.Destroy();
        }

        [Fact]
        public void OpenFile_NumberNotOnCurrentPage_IsIgnored()
        {
            var many = Path.Combine(_root, "Many");
            Directory.CreateDirectory(many);
            for (var i = 0; i < 20; i++)
                File.WriteAllText(Path.Combine(many, $"file{i:D2}.txt"), "x");

            var app = new FileDialogHostApp();
            string picked = null;

            // 21 entries (".." + 20 files) => two pages; page 1 shows only numbers 1..12.
            FileDialog.OpenFile(app, many, null, path => picked = path);
            app.OnTick(false);

            Send(app, "13"); // not shown on the current page -> ignored, no wrong-entry selection
            Assert.Null(picked);
            Assert.NotNull(app.WindowManager.FocusedWindow); // dialog still open

            Send(app, "12"); // a real on-page entry (a file) -> selected
            Assert.NotNull(picked);

            app.Destroy();
        }

        [Fact]
        public void Show_WhenADialogIsAlreadyClosing_ThrowsInsteadOfSilentlyFailing()
        {
            var app = new FileDialogHostApp();
            FileDialog.OpenFile(app, _root, null, _ => { });
            app.OnTick(false);

            // Simulate the dialog mid-close (as it is while inside a confirm/cancel callback).
            app.WindowManager.FocusedWindow.RemoveWindowNextTick();

            Assert.Throws<InvalidOperationException>(() => FileDialog.OpenFile(app, _root, null, _ => { }));

            app.Destroy();
        }
    }
}
