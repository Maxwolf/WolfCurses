using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The pull-down menu bar. The reason it exists is that it <b>keeps its layout</b>: the library's numbered
    ///     menus reflow into columns they do not retain, so nothing afterwards knows which cell holds which item,
    ///     which is why clicking one was never supported. Everything else here is ordinary menu behaviour; the test
    ///     that earns the control its place is the one asserting that where an entry is drawn is where a click on it
    ///     lands.
    /// </summary>
    public class MenuBarTests
    {
        private static string StripSgr(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        private static (MenuBar bar, List<string> ran) NewBar()
        {
            var ran = new List<string>();

            var bar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("New", () => ran.Add("new")),
                    new MenuBarEntry("Open", () => ran.Add("open"), "F3"),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Exit", () => ran.Add("exit"))),
                new MenuBarMenu("Edit",
                    new MenuBarEntry("Cut", () => ran.Add("cut"), "Ctrl+X"),
                    new MenuBarEntry("Copy", () => ran.Add("copy"), "Ctrl+C")),
                new MenuBarMenu("Search",
                    new MenuBarEntry("Find", () => ran.Add("find"))));

            return (bar, ran);
        }

        private static ConsoleKeyInfo Key(ConsoleKey key, ConsoleModifiers modifiers = 0)
        {
            return new ConsoleKeyInfo(
                (char) 0,
                key,
                (modifiers & ConsoleModifiers.Shift) != 0,
                (modifiers & ConsoleModifiers.Alt) != 0,
                (modifiers & ConsoleModifiers.Control) != 0);
        }

        [Fact]
        public void ABarStartsClosedAndDrawsOnlyItsTitles()
        {
            var (bar, _) = NewBar();

            Assert.False(bar.IsOpen);
            Assert.Equal(0, bar.DropdownHeight);

            var rows = StripSgr(bar.Render(40)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(rows);
            Assert.Contains("File", rows[0], StringComparison.Ordinal);
            Assert.Contains("Edit", rows[0], StringComparison.Ordinal);
        }

        [Fact]
        public void WhereAnEntryIsDrawnIsWhereAClickOnItLands()
        {
            // The whole justification for the control. Both halves are read off Render's own output rather than
            // restated, so a change that moved the drawing without moving the hit test fails here.
            var (bar, ran) = NewBar();
            bar.BarRow = 0;
            bar.Open(0);

            var rows = StripSgr(bar.Render(60)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Row 0 is the bar, row 1 the panel's top border, so entry N is on row N + 2.
            var openRow = Array.FindIndex(rows, row => row.Contains("Open", StringComparison.Ordinal));
            Assert.True(openRow > 0, "the Open entry was not drawn:\n" + string.Join('\n', rows));

            var openColumn = rows[openRow].IndexOf("Open", StringComparison.Ordinal);
            Assert.Equal(1, bar.EntryAt(openRow, openColumn));

            bar.HandleMouse(openRow, openColumn);
            Assert.Equal(new[] {"open"}, ran);
        }

        [Fact]
        public void ClickingATitleOpensThatMenuAndClickingItAgainShutsIt()
        {
            var (bar, _) = NewBar();
            bar.BarRow = 0;

            var titles = StripSgr(bar.Render(60)).Split('\n')[0];
            var editColumn = titles.IndexOf("Edit", StringComparison.Ordinal);

            Assert.True(bar.HandleMouse(0, editColumn));
            Assert.True(bar.IsOpen);
            Assert.Equal(1, bar.OpenIndex);

            Assert.True(bar.HandleMouse(0, editColumn));
            Assert.False(bar.IsOpen);
        }

        [Fact]
        public void EveryTitleIsHitTestableAtTheColumnItIsDrawnAt()
        {
            var (bar, _) = NewBar();
            var titles = StripSgr(bar.Render(60)).Split('\n')[0];

            for (var i = 0; i < bar.Menus.Count; i++)
            {
                var column = titles.IndexOf(bar.Menus[i].Title, StringComparison.Ordinal);
                Assert.True(column >= 0, $"{bar.Menus[i].Title} was not drawn");
                Assert.Equal(i, bar.TitleAt(column));
            }
        }

        [Fact]
        public void ClickingOffTheMenuShutsItAndSwallowsThePress()
        {
            // Dismissing a menu must not also do whatever was underneath it, which is what the consumed return is
            // for: the owner sees the press was spent and leaves its document alone.
            var (bar, ran) = NewBar();
            bar.BarRow = 0;
            bar.Open(0);

            Assert.True(bar.HandleMouse(40, 70));

            Assert.False(bar.IsOpen);
            Assert.Empty(ran);
        }

        [Fact]
        public void APressWithNothingOpenAndNoTitleUnderItIsNotTheMenuBarsBusiness()
        {
            var (bar, _) = NewBar();
            bar.BarRow = 0;

            Assert.False(bar.HandleMouse(10, 5));
            Assert.False(bar.HandleMouse(0, 500));
        }

        [Fact]
        public void AltAndTheFirstLetterOpensAMenu()
        {
            var (bar, _) = NewBar();

            Assert.True(bar.HandleKey(Key(ConsoleKey.E, ConsoleModifiers.Alt)));

            Assert.True(bar.IsOpen);
            Assert.Equal(1, bar.OpenIndex);
        }

        [Fact]
        public void F10OpensAndShutsTheBarBecauseAltIsNotAlwaysDelivered()
        {
            // The reason every text-mode application had F10. ALT is not reliably reported as a modifier: terminals
            // swallow it, send an escape prefix instead, or hand it to the window manager, so a bar reachable only
            // by ALT simply does not open on those. This was found by running the editor rather than by a test,
            // which is worth remembering about anything that depends on how a terminal reports modifiers.
            var (bar, _) = NewBar();

            Assert.True(bar.HandleKey(Key(ConsoleKey.F10)));
            Assert.True(bar.IsOpen);
            Assert.Equal(0, bar.OpenIndex);

            Assert.True(bar.HandleKey(Key(ConsoleKey.F10)));
            Assert.False(bar.IsOpen);
        }

        [Fact]
        public void TheLetterThatOpensAMenuIsUnderlined()
        {
            // Underline rather than colour, because which key opens a menu is not something a colour can say, and
            // because it is what every text-mode application did. SGR 4 is the sequence.
            var (bar, _) = NewBar();
            bar.BarStyle = new TextStyle(ConsoleColor.Black, ConsoleColor.Gray);
            bar.ColorMode = AnsiColorModeEnum.Palette256;

            var row = bar.Render(60).Split('\n')[0];

            // Underline is written first in the parameter list, so the run opens "ESC[4;<colours>m".
            Assert.Contains("[4", row, StringComparison.Ordinal);

            // Exactly the access key and no more: the whole title underlined says something different, and the
            // visible text is unchanged by any of it.
            Assert.StartsWith(" File  Edit  Search ", StripSgr(row), StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnstyledBarStillCarriesNoEscapesWhenAccessKeysAreOff()
        {
            // The compatibility stance survives the feature: a bar nobody coloured and nobody asked to mark up is
            // still plain text.
            var (bar, _) = NewBar();
            bar.ShowAccessKeys = false;

            Assert.DoesNotContain('\x1b', bar.Render(60));
        }

        [Fact]
        public void ARightAlignedMenuIsDrawnAtTheEdgeAndHitTestsThere()
        {
            var ran = new List<string>();
            var bar = new MenuBar(
                new MenuBarMenu("File", new MenuBarEntry("New", () => ran.Add("new"))),
                new MenuBarMenu("Help", new MenuBarEntry("About", () => ran.Add("about"))) {AlignRight = true});

            var row = StripSgr(bar.Render(60)).Split('\n')[0];
            var help = row.IndexOf("Help", StringComparison.Ordinal);

            Assert.True(help > 40, $"Help was not laid at the right-hand edge: column {help}");
            Assert.Equal(1, bar.TitleAt(help));
        }

        [Fact]
        public void WithNothingOpenAnOrdinaryKeyIsLeftForWhoeverOwnsTheScreen()
        {
            // The false return is what lets an editor keep typing while the menu bar is shut.
            var (bar, _) = NewBar();

            Assert.False(bar.HandleKey(Key(ConsoleKey.A)));
            Assert.False(bar.HandleKey(Key(ConsoleKey.Enter)));
            Assert.False(bar.HandleKey(Key(ConsoleKey.DownArrow)));
        }

        [Fact]
        public void WithAMenuOpenEveryKeyIsSwallowed()
        {
            // The other side of it: nothing leaks through to the document behind an open menu.
            var (bar, _) = NewBar();
            bar.Open(0);

            Assert.True(bar.HandleKey(Key(ConsoleKey.A)));
            Assert.True(bar.HandleKey(Key(ConsoleKey.F7)));
        }

        [Fact]
        public void TheArrowKeysWalkTheEntriesAndSkipTheSeparator()
        {
            // File is New, Open, a rule, Exit. Walking down from Open must land on Exit rather than stalling on a
            // line that cannot be chosen.
            var (bar, _) = NewBar();
            bar.Open(0);
            Assert.Equal(0, bar.HighlightIndex);

            bar.HandleKey(Key(ConsoleKey.DownArrow));
            Assert.Equal(1, bar.HighlightIndex);

            bar.HandleKey(Key(ConsoleKey.DownArrow));
            Assert.Equal(3, bar.HighlightIndex);
        }

        [Fact]
        public void TheEntriesWrapRoundTheEndsOfTheMenu()
        {
            var (bar, _) = NewBar();
            bar.Open(0);

            bar.HandleKey(Key(ConsoleKey.UpArrow));
            Assert.Equal(3, bar.HighlightIndex);

            bar.HandleKey(Key(ConsoleKey.DownArrow));
            Assert.Equal(0, bar.HighlightIndex);
        }

        [Fact]
        public void LeftAndRightMoveBetweenMenusAndWrapRoundTheBar()
        {
            var (bar, _) = NewBar();
            bar.Open(0);

            bar.HandleKey(Key(ConsoleKey.RightArrow));
            Assert.Equal(1, bar.OpenIndex);

            bar.HandleKey(Key(ConsoleKey.LeftArrow));
            bar.HandleKey(Key(ConsoleKey.LeftArrow));
            Assert.Equal(2, bar.OpenIndex);
        }

        [Fact]
        public void EnterRunsTheHighlightedEntryAndShutsTheMenu()
        {
            var (bar, ran) = NewBar();
            bar.Open(1);

            bar.HandleKey(Key(ConsoleKey.Enter));

            Assert.Equal(new[] {"cut"}, ran);
            Assert.False(bar.IsOpen);
        }

        [Fact]
        public void TheMenuIsShutBeforeTheActionRunsRatherThanAfter()
        {
            // An action is free to open a dialog or rebuild this very menu. Closing afterwards would either reach
            // into whatever the action put up or quietly undo it, and both are the kind of bug that only appears
            // once somebody writes a menu item that opens another menu.
            var openWhenActionRan = true;
            MenuBar bar = null;

            bar = new MenuBar(new MenuBarMenu("File",
                new MenuBarEntry("Go", () => openWhenActionRan = bar.IsOpen)));

            bar.Open(0);
            bar.HandleKey(Key(ConsoleKey.Enter));

            Assert.False(openWhenActionRan);
        }

        [Fact]
        public void EscapeShutsTheMenuAndSaysItWasSpent()
        {
            // The consumed return matters more than the close: an application that treats ESC as "leave" needs to
            // know the menu took it, or opening a menu and dismissing it would drop the user out of the program.
            var (bar, _) = NewBar();
            bar.Open(0);

            Assert.True(bar.HandleKey(Key(ConsoleKey.Escape)));
            Assert.False(bar.IsOpen);

            Assert.False(bar.HandleKey(Key(ConsoleKey.Escape)));
        }

        [Fact]
        public void ADisabledEntryIsDrawnAndCannotBeChosen()
        {
            // Drawn, because a menu whose items come and go is one nobody can learn the shape of.
            var ran = new List<string>();
            var paste = new MenuBarEntry("Paste", () => ran.Add("paste")) {IsEnabled = false};
            var bar = new MenuBar(new MenuBarMenu("Edit",
                new MenuBarEntry("Copy", () => ran.Add("copy")),
                paste));

            bar.Open(0);
            Assert.Equal(0, bar.HighlightIndex);

            bar.HandleKey(Key(ConsoleKey.DownArrow));
            Assert.Equal(0, bar.HighlightIndex);

            Assert.Contains("Paste", StripSgr(bar.Render(40)), StringComparison.Ordinal);
        }

        [Fact]
        public void AMenuWithNothingChoosableOpensOntoNothingAndEnterDoesNothingAtAll()
        {
            // Including not closing. ENTER means "run the highlighted entry" and there is no highlighted entry, so
            // the honest answer is that nothing happens; dismissing the menu instead would be a surprise, and ESC
            // and a click elsewhere both still shut it.
            var bar = new MenuBar(new MenuBarMenu("Empty", MenuBarEntry.Separator()));

            bar.Open(0);

            Assert.True(bar.IsOpen);
            Assert.Equal(-1, bar.HighlightIndex);
            Assert.Null(bar.Highlighted);

            Assert.True(bar.HandleKey(Key(ConsoleKey.Enter)));
            Assert.True(bar.IsOpen);

            Assert.True(bar.HandleKey(Key(ConsoleKey.Escape)));
            Assert.False(bar.IsOpen);
        }

        [Fact]
        public void TheReportedPanelHeightIsTheNumberOfRowsActuallyDrawn()
        {
            // The owner shortens its own content by this much, so a panel that drew more rows than it admitted to
            // would push the bottom of the screen off.
            var (bar, _) = NewBar();
            bar.Open(0);

            var rows = StripSgr(bar.Render(60)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(bar.DropdownHeight, rows.Length - 1);
        }

        [Fact]
        public void ShortcutsAreShownBesideTheEntriesTheyRun()
        {
            var (bar, _) = NewBar();
            bar.Open(1);

            var panel = StripSgr(bar.Render(60));

            Assert.Contains("Ctrl+X", panel, StringComparison.Ordinal);
            Assert.Contains("Ctrl+C", panel, StringComparison.Ordinal);
        }

        [Fact]
        public void ThePanelIsARectangleWhateverIsInIt()
        {
            // Every row the same width, or the border is not a border. Measured with the escapes taken off, since a
            // highlighted row carries them and they have length but no width.
            var (bar, _) = NewBar();
            bar.Open(0);

            var rows = StripSgr(bar.Render(60))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .ToArray();

            Assert.All(rows, row => Assert.Equal(rows[0].Length, row.Length));
        }

        [Fact]
        public void OpeningOutOfRangeShutsTheBarRatherThanThrowing()
        {
            var (bar, _) = NewBar();

            bar.Open(99);
            Assert.False(bar.IsOpen);

            bar.Open(-1);
            Assert.False(bar.IsOpen);
        }

        [Fact]
        public void AnEntryThatSaysWhenItIsEnabledIsAskedAgainRatherThanRemembered()
        {
            // The whole reason the predicate exists. Nothing tells a menu that a selection appeared, so an entry
            // built dead has to come alive by itself; a flag somebody has to remember to set is one that is stale
            // exactly as often as it is forgotten.
            var hasSelection = false;
            var ran = new List<string>();

            var bar = new MenuBar(new MenuBarMenu("Edit",
                new MenuBarEntry("Copy", () => ran.Add("copy")),
                new MenuBarEntry("Cut", () => ran.Add("cut")) {EnabledWhen = () => hasSelection}));

            bar.Open(0);
            bar.HandleKey(Key(ConsoleKey.DownArrow));
            Assert.Equal(0, bar.HighlightIndex);

            hasSelection = true;

            bar.HandleKey(Key(ConsoleKey.DownArrow));
            Assert.Equal(1, bar.HighlightIndex);

            bar.HandleKey(Key(ConsoleKey.Enter));
            Assert.Equal(new[] {"cut"}, ran);
        }

        [Fact]
        public void AnEntryThatGoesDeadWhileTheMenuIsOpenCannotStillBeChosen()
        {
            // Enablement is read again when an entry is chosen, not only when the panel was drawn. Asking only at
            // draw time would leave the cursor sitting on an entry that had since stopped meaning anything, and
            // ENTER would run it.
            var enabled = true;
            var ran = 0;

            var bar = new MenuBar(new MenuBarMenu("Edit",
                new MenuBarEntry("Paste", () => ran++) {EnabledWhen = () => enabled}));

            bar.Open(0);
            Assert.Equal(0, bar.HighlightIndex);

            enabled = false;

            bar.HandleKey(Key(ConsoleKey.Enter));
            Assert.Equal(0, ran);

            bar.HandleMouse(1, 2);
            Assert.Equal(0, ran);
        }

        [Fact]
        public void ACheckedEntryIsMarkedAndAnUncheckedOneStillReservesTheColumn()
        {
            // The second half is the one that matters. If only the ticked entry were indented, the labels in one
            // menu would sit at two different places and would move as the setting changed.
            var wrap = true;
            var bar = new MenuBar(new MenuBarMenu("Options",
                new MenuBarEntry("Word Wrap", () => { }) {CheckedWhen = () => wrap},
                new MenuBarEntry("Match Case", () => { }) {CheckedWhen = () => false}));

            bar.Open(0);

            var rows = StripSgr(bar.Render(40)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var wrapRow = rows.Single(row => row.Contains("Word Wrap", StringComparison.Ordinal));
            var caseRow = rows.Single(row => row.Contains("Match Case", StringComparison.Ordinal));

            Assert.Contains(bar.CheckMark, wrapRow);
            Assert.DoesNotContain(bar.CheckMark, caseRow);

            Assert.Equal(wrapRow.IndexOf("Word Wrap", StringComparison.Ordinal),
                caseRow.IndexOf("Match Case", StringComparison.Ordinal));
        }

        [Fact]
        public void TheMarkIsAskedForEachTimeRatherThanRememberedFromWhenTheMenuWasBuilt()
        {
            var wrap = false;
            var bar = new MenuBar(new MenuBarMenu("Options",
                new MenuBarEntry("Word Wrap", () => { }) {CheckedWhen = () => wrap}));

            bar.Open(0);
            Assert.DoesNotContain(bar.CheckMark, StripSgr(bar.Render(40)));

            wrap = true;
            Assert.Contains(bar.CheckMark, StripSgr(bar.Render(40)));
        }

        [Fact]
        public void APanelWithCheckMarksIsStillARectangleAndStillFitsItsShortcuts()
        {
            // The check column is added to the menu's content width rather than taken out of it, or a marked entry
            // would push its own shortcut past the border it is supposed to sit inside.
            var bar = new MenuBar(new MenuBarMenu("Search",
                new MenuBarEntry("Find Next", () => { }, "F3") {CheckedWhen = () => true},
                new MenuBarEntry("Change All", () => { }, "Ctrl+H")));

            bar.Open(0);

            var rows = StripSgr(bar.Render(60)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

            Assert.All(rows, row => Assert.Equal(rows[0].Length, row.Length));
            Assert.Contains(rows, row => row.Contains("F3", StringComparison.Ordinal));
            Assert.Contains(rows, row => row.Contains("Ctrl+H", StringComparison.Ordinal));
        }

        [Fact]
        public void AMenuWithNothingCheckableReservesNoColumnAtAll()
        {
            // The compatibility half: check marks are an addition, so a menu that never asked for one is drawn
            // exactly as wide as it always was, with its labels exactly where they were.
            var plain = new MenuBar(new MenuBarMenu("Edit", new MenuBarEntry("Cut", () => { }, "Ctrl+X")));
            var checkable = new MenuBar(new MenuBarMenu("Edit",
                new MenuBarEntry("Cut", () => { }, "Ctrl+X") {CheckedWhen = () => false}));

            plain.Open(0);
            checkable.Open(0);

            var plainRow = StripSgr(plain.Render(40)).Split('\n')[1];
            var checkableRow = StripSgr(checkable.Render(40)).Split('\n')[1];

            Assert.Equal(0, plain.Menus[0].CheckColumns);
            Assert.Equal(2, checkable.Menus[0].CheckColumns);
            Assert.Equal(plainRow.Length + 2, checkableRow.Length);
        }

        [Fact]
        public void AnEntryWithNoPredicateStillAnswersToItsFlag()
        {
            // The compatibility half: EnabledWhen is an addition, so an entry that was never given one behaves
            // exactly as it did, both ways round.
            var entry = new MenuBarEntry("Save", () => { });
            Assert.True(entry.IsEnabled);

            entry.IsEnabled = false;
            Assert.False(entry.IsEnabled);
            Assert.False(entry.IsSelectable);

            entry.IsEnabled = true;
            Assert.True(entry.IsSelectable);
        }
    }
}
