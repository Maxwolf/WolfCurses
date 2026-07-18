using System;
using System.IO;
using System.Text.RegularExpressions;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Drives the file dialog with the arrow-key + ENTER style through the real tick/input pipeline: Down to an
    ///     entry, ENTER to descend or choose, with the cursor resetting to the top of every freshly revealed listing
    ///     — a highlight carried across a directory change would point at whatever coincidentally sits in its old
    ///     row. Typed navigation from the older tests keeps working alongside.
    /// </summary>
    public class FileDialogKeyboardTests : IDisposable
    {
        private readonly string _root;

        public FileDialogKeyboardTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "wolfcurses_fdkey_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(Path.Combine(_root, "Alpha"));
            File.WriteAllText(Path.Combine(_root, "Alpha", "inside.png"), "x");
            File.WriteAllText(Path.Combine(_root, "image.png"), "x");
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

        private static void PressKey(SimulationApp app, ConsoleKey key, char keyChar = '\0')
        {
            app.InputManager.SendConsoleKey(new ConsoleKeyInfo(keyChar, key, false, false, false));
            app.OnTick(false);
        }

        private static void PressEnter(SimulationApp app)
        {
            app.InputManager.SendConsoleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
            app.OnTick(false);
        }

        private static string Rendered(SimulationApp app)
        {
            var text = app.WindowManager.FocusedWindow.OnRenderWindow();
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        [Fact]
        public void TheCursorOpensOnTheFirstEntry()
        {
            var app = new FileDialogHostApp();
            FileDialog.OpenFile(app, _root, new[] {".png"}, _ => { });
            app.OnTick(false);

            // Root (.png filter) lists: 1=.., 2=Alpha (dir), 3=image.png.
            Assert.Contains(">  1. .. (up one level)", Rendered(app));

            app.Destroy();
        }

        [Fact]
        public void DownDownEnterDescendsIntoAFolderAndTheCursorResetsToItsTop()
        {
            var app = new FileDialogHostApp();
            FileDialog.OpenFile(app, _root, new[] {".png"}, _ => { });
            app.OnTick(false);

            PressKey(app, ConsoleKey.DownArrow); // onto Alpha
            PressEnter(app);                     // descend

            var rendered = Rendered(app);
            Assert.Contains("Alpha", rendered);
            // The new listing's first entry is highlighted, not whatever row number the cursor used to be on.
            Assert.Contains(">  1. .. (up one level)", rendered);

            app.Destroy();
        }

        [Fact]
        public void ArrowsAndEnterAloneCanWalkInAndChooseAFile()
        {
            var app = new FileDialogHostApp();
            string picked = null;
            FileDialog.OpenFile(app, _root, new[] {".png"}, path => picked = path);
            app.OnTick(false);

            PressKey(app, ConsoleKey.DownArrow); // Alpha
            PressEnter(app);                     // descend -> 1=.., 2=inside.png
            PressKey(app, ConsoleKey.DownArrow); // inside.png
            PressEnter(app);                     // choose it

            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Alpha", "inside.png")), picked);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void EnterOnTheParentEntryGoesUpLikeItSays()
        {
            var app = new FileDialogHostApp();
            FileDialog.OpenFile(app, Path.Combine(_root, "Alpha"), new[] {".png"}, _ => { });
            app.OnTick(false);

            PressEnter(app); // cursor opens on "..", so ENTER goes up to root

            Assert.Contains("image.png", Rendered(app));

            app.Destroy();
        }

        [Fact]
        public void TypedNumbersStillNavigateAndStillWinOverTheHighlight()
        {
            var app = new FileDialogHostApp();
            string picked = null;
            FileDialog.OpenFile(app, _root, new[] {".png"}, path => picked = path);
            app.OnTick(false);

            // Highlight is on "..", but the typed 3 chooses image.png.
            PressKey(app, ConsoleKey.D3, '3');
            PressEnter(app);

            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "image.png")), picked);

            app.Destroy();
        }
    }
}
