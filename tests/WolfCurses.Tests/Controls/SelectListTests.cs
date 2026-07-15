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
        public void ChooseMany_InitiallySelected_OpensCheckedAndConfirmReturnsThem()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;

            // Pre-check two items, then confirm without touching anything: the confirmed set must be exactly the
            // pre-checked set, proving the checkboxes opened in the requested state.
            SelectList.ChooseMany(app, "Pick", Colors, c => c, items => chosen = items,
                initiallySelected: new[] {"Crimson", "Sapphire"});
            app.OnTick(false);

            Send(app, "S"); // confirm

            Assert.Equal(new[] {"Crimson", "Sapphire"}, chosen);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void ChooseMany_InitiallySelected_UserEditsFromThatStartingState()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;

            SelectList.ChooseMany(app, "Pick", Colors, c => c, items => chosen = items,
                initiallySelected: new[] {"Crimson", "Sapphire"}); // indices 0 and 2 checked
            app.OnTick(false);

            Send(app, "1"); // toggle Crimson (index 0) back off
            Send(app, "4"); // toggle Amber (index 3) on
            Send(app, "S"); // confirm -> Sapphire + Amber

            Assert.Equal(new[] {"Sapphire", "Amber"}, chosen);

            app.Destroy();
        }

        [Fact]
        public void ChooseMany_ByIndex_InitiallySelected_PreChecksThoseIndices()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<int> chosen = null;

            SelectList.ChooseMany(app, "Pick", Colors, indices => chosen = indices,
                initiallySelected: new[] {1, 3});
            app.OnTick(false);

            Send(app, "S");

            Assert.Equal(new[] {1, 3}, chosen);

            app.Destroy();
        }

        [Fact]
        public void ChooseMany_InitiallySelected_IgnoresOutOfRangeIndices()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<int> chosen = null;

            SelectList.ChooseMany(app, "Pick", Colors, indices => chosen = indices,
                initiallySelected: new[] {-1, 2, 99});
            app.OnTick(false);

            Send(app, "S");

            Assert.Equal(new[] {2}, chosen); // only the in-range index survives

            app.Destroy();
        }

        [Fact]
        public void ChooseMany_InitiallySelected_IgnoresItemsNotAmongTheOptions()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;

            SelectList.ChooseMany(app, "Pick", Colors, c => c, items => chosen = items,
                initiallySelected: new[] {"Sapphire", "Chartreuse"}); // Chartreuse is not an option
            app.OnTick(false);

            Send(app, "S");

            Assert.Equal(new[] {"Sapphire"}, chosen);

            app.Destroy();
        }

        [Fact]
        public void ChooseMany_InitiallySelected_ChecksEveryOccurrenceOfADuplicateOption()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;

            // Pins the generic overload's documented duplicate contract: a wanted item that appears more than once in
            // the options starts checked at every occurrence, so confirming returns all of them. Guards against a
            // refactor to first-match semantics (which would still leave every other test green).
            var options = new[] {"A", "B", "A"};
            SelectList.ChooseMany(app, "Pick", options, x => x, items => chosen = items,
                initiallySelected: new[] {"A"});
            app.OnTick(false);

            Send(app, "S"); // confirm without touching anything

            Assert.Equal(new[] {"A", "A"}, chosen);

            app.Destroy();
        }

        [Fact]
        public void Data_InitializeWithInitialSelection_MultiSelect_ChecksInRangeIndicesOnly()
        {
            var data = new SelectListData();
            data.Initialize("t", new[] {"a", "b", "c"}, multiSelect: true, _ => { }, null,
                initiallySelected: new[] {0, 2, 5, -1});

            Assert.True(data.IsSelected(0));
            Assert.False(data.IsSelected(1));
            Assert.True(data.IsSelected(2));
            Assert.Equal(new[] {0, 2}, data.SelectedIndices());
        }

        [Fact]
        public void Data_InitializeWithInitialSelection_SingleSelect_IgnoresIt()
        {
            var data = new SelectListData();
            data.Initialize("t", new[] {"a", "b", "c"}, multiSelect: false, _ => { }, null,
                initiallySelected: new[] {0, 2});

            Assert.Empty(data.SelectedIndices());
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
