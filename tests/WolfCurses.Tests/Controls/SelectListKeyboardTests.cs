using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WolfCurses.Controls;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Drives the <see cref="SelectList" /> picker with the arrow-key/SPACE/ENTER style through the real
    ///     tick/input pipeline, alongside the typed style the older tests pin. The subtleties covered: the cursor is
    ///     visible from the first frame, SPACE toggles the highlighted row and scrubs the space the input buffer
    ///     swallowed, ENTER confirms, the page follows the highlight, and the two input styles share one cursor.
    /// </summary>
    public class SelectListKeyboardTests
    {
        private static readonly string[] _colors = {"Crimson", "Emerald", "Sapphire", "Amber", "Teal"};

        private static void PressKey(SimulationApp app, ConsoleKey key, char keyChar = '\0')
        {
            // Through SendConsoleKey, the way a real console session arrives: printable keys land in the buffer AND
            // the key path, which is exactly the duality the SPACE handling exists to deal with.
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
        public void TheCursorIsVisibleOnTheFirstOptionFromTheFirstFrame()
        {
            var app = new ControlsHostApp();
            SelectList.Choose(app, "Pick", _colors, _ => { });
            app.OnTick(false);

            Assert.Contains(">  1. Crimson", Rendered(app));

            app.Destroy();
        }

        [Fact]
        public void ArrowsMoveTheCursorAndEnterChoosesIt()
        {
            var app = new ControlsHostApp();
            var chosen = -1;
            SelectList.Choose(app, "Pick", _colors, index => chosen = index);
            app.OnTick(false);

            PressKey(app, ConsoleKey.DownArrow);
            PressKey(app, ConsoleKey.DownArrow);
            Assert.Contains(">  3. Sapphire", Rendered(app));

            PressEnter(app);

            Assert.Equal(2, chosen);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void SpaceTogglesTheHighlightedRowAndEnterConfirmsTheSet()
        {
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;
            SelectList.ChooseMany(app, "Pick", _colors, c => c, items => chosen = items);
            app.OnTick(false);

            PressKey(app, ConsoleKey.Spacebar, ' '); // Crimson on
            PressKey(app, ConsoleKey.DownArrow);
            PressKey(app, ConsoleKey.Spacebar, ' '); // Emerald on
            Assert.Contains("[x] Crimson", Rendered(app));
            Assert.Contains("[x] Emerald", Rendered(app));

            PressKey(app, ConsoleKey.Spacebar, ' '); // Emerald back off
            Assert.Contains("[ ] Emerald", Rendered(app));

            PressEnter(app);

            Assert.Equal(new[] {"Crimson"}, chosen);
            Assert.Null(app.WindowManager.FocusedWindow);

            app.Destroy();
        }

        [Fact]
        public void TheSpaceThatMeansToggleNeverLingersInTheInputBuffer()
        {
            // A space is printable, so it enters the buffer as well as the key path — deliberately. The toggle
            // handler scrubs it; without that, every toggle would nudge the echoed prompt one blank to the right.
            var app = new ControlsHostApp();
            SelectList.ChooseMany(app, "Pick", _colors, c => c, _ => { });
            app.OnTick(false);

            PressKey(app, ConsoleKey.Spacebar, ' ');

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);

            app.Destroy();
        }

        [Fact]
        public void WalkingOffTheBottomOfAPageTurnsThePage()
        {
            var app = new ControlsHostApp();
            var many = Enumerable.Range(1, 25).Select(n => $"Item {n}").ToArray();
            SelectList.Choose(app, "Pick", many, _ => { });
            app.OnTick(false);

            // Ten steps down from the first row: highlight lands on the 11th item, which lives on page 2.
            for (var i = 0; i < 10; i++)
                PressKey(app, ConsoleKey.DownArrow);

            var rendered = Rendered(app);
            Assert.Contains("Page 2/3", rendered);
            Assert.Contains(">  1. Item 11", rendered);

            app.Destroy();
        }

        [Fact]
        public void TypedPagingParksTheCursorOnTheRevealedPage()
        {
            // A highlight left pointing at an off-screen row would let ENTER choose something invisible; the typed
            // paging commands therefore pull the cursor to the top of the page they show.
            var app = new ControlsHostApp();
            var many = Enumerable.Range(1, 25).Select(n => $"Item {n}").ToArray();
            var chosen = -1;
            SelectList.Choose(app, "Pick", many, index => chosen = index);
            app.OnTick(false);

            foreach (var c in "N")
                app.InputManager.AddCharToInputBuffer(c);
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);

            Assert.Contains(">  1. Item 11", Rendered(app));

            PressEnter(app);
            Assert.Equal(10, chosen);

            app.Destroy();
        }

        [Fact]
        public void TogglingByTypedNumberMovesTheSharedCursorThere()
        {
            var app = new ControlsHostApp();
            SelectList.ChooseMany(app, "Pick", _colors, c => c, _ => { });
            app.OnTick(false);

            foreach (var c in "4")
                app.InputManager.AddCharToInputBuffer(c);
            app.InputManager.SendInputBufferAsCommand();
            app.OnTick(false);

            var rendered = Rendered(app);
            Assert.Contains(">  4. [x] Amber", rendered);

            app.Destroy();
        }

        [Fact]
        public void EnterOnAnUntouchedMultiSelectConfirmsItsInitialSet()
        {
            // The initiallySelected flow, keyboard style: open pre-checked, press ENTER, get the same set back.
            var app = new ControlsHostApp();
            IReadOnlyList<string> chosen = null;
            SelectList.ChooseMany(app, "Pick", _colors, c => c, items => chosen = items,
                initiallySelected: new[] {"Emerald", "Teal"});
            app.OnTick(false);

            PressEnter(app);

            Assert.Equal(new[] {"Emerald", "Teal"}, chosen);

            app.Destroy();
        }

        [Fact]
        public void TypedNumbersStillWinOverTheHighlight()
        {
            // The shared rule with window menus: a non-empty buffer is more specific than the cursor.
            var app = new ControlsHostApp();
            var chosen = -1;
            SelectList.Choose(app, "Pick", _colors, index => chosen = index);
            app.OnTick(false);

            PressKey(app, ConsoleKey.DownArrow);
            PressKey(app, ConsoleKey.D5, '5');
            PressEnter(app);

            Assert.Equal(4, chosen);

            app.Destroy();
        }
    }
}
