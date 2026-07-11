using System;
using System.Collections.Generic;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Drives the <see cref="SelectList" /> picker through the real tick/input pipeline: single- and multi-select
    ///     flows, cancellation, out-of-page number safety, and the open guards.
    /// </summary>
    public class SelectListTests
    {
        private static readonly string[] Colors = {"Crimson", "Emerald", "Sapphire", "Amber", "Teal"};

        private static void Send(SimulationApp app, string command)
        {
            foreach (var c in command)
                app.InputManager.AddCharToInputBuffer(c);
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);
        }

        [Fact]
        public void Choose_ByNumber_FiresIndexAndClosesDialog()
        {
            var app = new ControlsHostApp();
            var chosen = -1;

            SelectList.Choose(app, "Pick", Colors, index => chosen = index);
            app.OnTick(false);

            Send(app, "3"); // Sapphire

            Assert.Equal(2, chosen);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Choose_Generic_ReturnsTheItem()
        {
            var app = new ControlsHostApp();
            string chosen = null;

            SelectList.Choose(app, "Pick", Colors, c => c, item => chosen = item);
            app.OnTick(false);

            Send(app, "1");

            Assert.Equal("Crimson", chosen);

            app.Destroy();
        }

        [Fact]
        public void ChooseMany_ToggleThenSelect_ReturnsCheckedItems()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;

            SelectList.ChooseMany(app, "Pick", Colors, c => c, items => chosen = items);
            app.OnTick(false);

            Send(app, "2"); // toggle Emerald on
            Send(app, "4"); // toggle Amber on
            Send(app, "2"); // toggle Emerald back off
            Assert.NotNull(app.WindowManager.FocusedWindow); // still open while toggling

            Send(app, "S"); // confirm

            Assert.Equal(new[] {"Amber"}, chosen);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void ChooseMany_SelectAllThenConfirm_ReturnsEveryItem()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;

            SelectList.ChooseMany(app, "Pick", Colors, c => c, items => chosen = items);
            app.OnTick(false);

            Send(app, "A"); // select all
            Send(app, "S"); // confirm

            Assert.Equal(Colors.Length, chosen.Count);

            app.Destroy();
        }

        [Fact]
        public void Cancel_FiresCancelCallbackAndCloses()
        {
            var app = new ControlsHostApp();
            var cancelled = false;

            SelectList.Choose(app, "Pick", Colors, _ => { }, () => cancelled = true);
            app.OnTick(false);

            Send(app, "C");

            Assert.True(cancelled);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void Choose_NumberBeyondCurrentPage_IsIgnored()
        {
            var app = new ControlsHostApp();
            var many = new List<string>();
            for (var i = 0; i < 25; i++)
                many.Add("Item" + i);
            var chosen = -1;

            // 25 options => three pages; page 1 shows numbers 1..10 only.
            SelectList.Choose(app, "Pick", many, index => chosen = index);
            app.OnTick(false);

            Send(app, "11"); // not on the current page
            Assert.Equal(-1, chosen);
            Assert.NotNull(app.WindowManager.FocusedWindow);

            Send(app, "N"); // next page: shows items 11..20 as numbers 1..10
            Send(app, "1"); // first item on page 2 == Item10

            Assert.Equal(10, chosen);

            app.Destroy();
        }

        [Fact]
        public void Choose_WindowNotAllowed_ThrowsHelpfully()
        {
            var app = new TestSimulationApp(); // does not allow SelectListWindow
            var ex = Assert.Throws<InvalidOperationException>(() =>
                SelectList.Choose(app, "Pick", Colors, _ => { }));
            Assert.Contains("AllowedWindows", ex.Message);
            app.Destroy();
        }

        [Fact]
        public void Choose_WhenAlreadyOpen_ThrowsInsteadOfDroppingTheFirstCallback()
        {
            var app = new ControlsHostApp();
            SelectList.Choose(app, "First", Colors, _ => { });
            app.OnTick(false);

            // A second open while the first is still showing must throw, not silently reconfigure the window.
            Assert.Throws<InvalidOperationException>(() => SelectList.Choose(app, "Second", Colors, _ => { }));

            app.Destroy();
        }

        [Fact]
        public void Choose_WhenAlreadyClosing_ThrowsInsteadOfSilentlyFailing()
        {
            var app = new ControlsHostApp();
            SelectList.Choose(app, "Pick", Colors, _ => { });
            app.OnTick(false);

            app.WindowManager.FocusedWindow.RemoveWindowNextTick();

            Assert.Throws<InvalidOperationException>(() => SelectList.Choose(app, "Pick", Colors, _ => { }));

            app.Destroy();
        }
    }
}
