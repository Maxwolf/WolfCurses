using System;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Covers the pure highlight-navigation widget every arrow-driven list shares. The subtleties pinned here are
    ///     the hidden-until-first-move state (the library's rendering-compatibility stance), which end each arrow
    ///     summons the highlight onto, wrap versus clamp, and resize never leaving the index dangling past the end.
    /// </summary>
    public class ListNavigatorTests
    {
        [Fact]
        public void StartsWithNoSelectionSoOwnersRenderExactlyAsBefore()
        {
            var navigator = new ListNavigator(5);

            Assert.False(navigator.HasSelection);
        }

        [Fact]
        public void DownFromNowhereSelectsTheFirstItem()
        {
            var navigator = new ListNavigator(5);

            Assert.True(navigator.HandleKey(ConsoleKey.DownArrow));
            Assert.True(navigator.HasSelection);
            Assert.Equal(0, navigator.Index);
        }

        [Fact]
        public void UpFromNowhereSelectsTheLastItem()
        {
            // "Previous" from no selection means the far end — the same meaning the key has in every ncurses list.
            var navigator = new ListNavigator(5);

            Assert.True(navigator.HandleKey(ConsoleKey.UpArrow));
            Assert.Equal(4, navigator.Index);
        }

        [Fact]
        public void StepsMoveOneAtATimeAndWrapAroundTheEnds()
        {
            var navigator = new ListNavigator(3);
            navigator.HandleKey(ConsoleKey.DownArrow); // 0
            navigator.HandleKey(ConsoleKey.DownArrow); // 1
            navigator.HandleKey(ConsoleKey.DownArrow); // 2
            navigator.HandleKey(ConsoleKey.DownArrow); // wraps to 0

            Assert.Equal(0, navigator.Index);

            navigator.HandleKey(ConsoleKey.UpArrow); // wraps back to 2
            Assert.Equal(2, navigator.Index);
        }

        [Fact]
        public void WithWrapOffTheEndsAreWalls()
        {
            var navigator = new ListNavigator(3) {Wrap = false};
            navigator.Select(2);

            navigator.HandleKey(ConsoleKey.DownArrow);
            Assert.Equal(2, navigator.Index);

            navigator.Select(0);
            navigator.HandleKey(ConsoleKey.UpArrow);
            Assert.Equal(0, navigator.Index);
        }

        [Fact]
        public void HorizontalNavigatorsUseLeftAndRightAndIgnoreUpAndDown()
        {
            // Only one axis belongs to the navigator so the owner can spend the other one; a button bar steered by
            // Left/Right must leave Up/Down unconsumed.
            var navigator = new ListNavigator(3, horizontal: true);

            Assert.True(navigator.HandleKey(ConsoleKey.RightArrow));
            Assert.Equal(0, navigator.Index);
            Assert.False(navigator.HandleKey(ConsoleKey.DownArrow));
            Assert.False(navigator.HandleKey(ConsoleKey.UpArrow));

            Assert.True(navigator.HandleKey(ConsoleKey.LeftArrow));
            Assert.Equal(2, navigator.Index);
        }

        [Fact]
        public void VerticalNavigatorsLeaveLeftAndRightForTheOwner()
        {
            var navigator = new ListNavigator(3);

            Assert.False(navigator.HandleKey(ConsoleKey.LeftArrow));
            Assert.False(navigator.HandleKey(ConsoleKey.RightArrow));
            Assert.False(navigator.HasSelection);
        }

        [Fact]
        public void HomeAndEndJumpToTheEnds()
        {
            var navigator = new ListNavigator(9);
            navigator.Select(4);

            Assert.True(navigator.HandleKey(ConsoleKey.End));
            Assert.Equal(8, navigator.Index);

            Assert.True(navigator.HandleKey(ConsoleKey.Home));
            Assert.Equal(0, navigator.Index);
        }

        [Fact]
        public void PageKeysJumpByThePageAndClampRatherThanWrap()
        {
            // A single step wrapping around an end is expected; a page jump teleporting past one is disorienting.
            var navigator = new ListNavigator(25) {PageSize = 10};
            navigator.Select(0);

            navigator.HandleKey(ConsoleKey.PageDown);
            Assert.Equal(10, navigator.Index);

            navigator.HandleKey(ConsoleKey.PageDown);
            navigator.HandleKey(ConsoleKey.PageDown);
            Assert.Equal(24, navigator.Index);

            navigator.HandleKey(ConsoleKey.PageUp);
            Assert.Equal(14, navigator.Index);
        }

        [Fact]
        public void PageKeysAreNotConsumedWhenThereIsNoPageSize()
        {
            var navigator = new ListNavigator(5);

            Assert.False(navigator.HandleKey(ConsoleKey.PageDown));
            Assert.False(navigator.HandleKey(ConsoleKey.PageUp));
        }

        [Fact]
        public void AnEmptyListHandlesNothing()
        {
            var navigator = new ListNavigator();

            Assert.False(navigator.HandleKey(ConsoleKey.DownArrow));
            Assert.False(navigator.HandleKey(ConsoleKey.Home));
            Assert.False(navigator.HasSelection);
        }

        [Fact]
        public void SelectClampsIntoRangeAndShowsTheHighlight()
        {
            var navigator = new ListNavigator(3);

            navigator.Select(99);
            Assert.True(navigator.HasSelection);
            Assert.Equal(2, navigator.Index);

            navigator.Select(-5);
            Assert.Equal(0, navigator.Index);
        }

        [Fact]
        public void ResizeKeepsAValidHighlightAndClampsADanglingOne()
        {
            // The store-window pattern: commands are cleared and rebuilt, and the highlight must never be left
            // pointing past the end of the new list for the owner to index with.
            var navigator = new ListNavigator(5);
            navigator.Select(4);

            navigator.Resize(3);
            Assert.True(navigator.HasSelection);
            Assert.Equal(2, navigator.Index);

            navigator.Resize(10);
            Assert.Equal(2, navigator.Index);
        }

        [Fact]
        public void ResizeToEmptyHidesTheHighlightEntirely()
        {
            var navigator = new ListNavigator(5);
            navigator.Select(2);

            navigator.Resize(0);

            Assert.False(navigator.HasSelection);
            Assert.False(navigator.HandleKey(ConsoleKey.DownArrow));
        }

        [Fact]
        public void NegativeCountsAreRefusedLoudly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ListNavigator(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ListNavigator(3).Resize(-2));
        }

        [Fact]
        public void DecorateRowKeepsUnhighlightedRowsAlignedWithTwoPlainSpaces()
        {
            // The unhighlighted shape is the compatibility contract: two spaces, no escapes, byte-identical to what
            // menus rendered before the navigator existed.
            Assert.Equal("  1. First", ListNavigator.DecorateRow("1. First", false));
        }

        [Fact]
        public void DecorateRowMarksTheHighlightedRowWithACursorWhateverTheColorMode()
        {
            // The "> " marker is the always-present half of the highlight; inverse video is decoration that depends
            // on the environment's color mode, so the assertion strips escapes rather than pinning them.
            var decorated = ListNavigator.DecorateRow("1. First", true);
            var visible = StripEscapes(decorated);

            Assert.Equal("> 1. First", visible);
        }

        private static string StripEscapes(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }
    }
}
