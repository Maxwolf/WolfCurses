using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The grid of clickable keys.
    ///     <para>
    ///         The test that earns the control its place is the one asserting that where a key is drawn is where a
    ///         press on it lands, with both halves read off <c>Render</c>'s own output rather than restated. Every
    ///         hand-rolled keypad works that arithmetic out twice and the two copies drift the first time a key is
    ///         widened, which is exactly what the spanning tests here are about.
    ///     </para>
    /// </summary>
    public class KeypadTests
    {
        /// <summary>Three narrow keys over one wide one and a narrow one, which is a calculator's bottom corner.</summary>
        private static Keypad NewPad(List<string> pressed)
        {
            return new Keypad(
                new KeypadRow(
                    new KeypadButton("7", () => pressed.Add("7")),
                    new KeypadButton("8", () => pressed.Add("8")),
                    new KeypadButton("9", () => pressed.Add("9"))),
                new KeypadRow(
                    new KeypadButton("0", () => pressed.Add("0"), 2),
                    new KeypadButton(".", () => pressed.Add("."))))
            {
                ButtonWidth = 5,
                Row = 10,
                Column = 4
            };
        }

        /// <summary>
        ///     Which screen row a label was drawn on, found rather than counted.
        ///     <para>
        ///         Measured with the escapes taken off, which is not tidiness: a lit key carries styling even on a
        ///         pad nobody coloured, so indexing into the raw row finds a column several bytes past the one it
        ///         is drawn in, and only the tests that hover something would notice.
        ///     </para>
        /// </summary>
        private static int RowOf(Keypad pad, string label)
        {
            var rows = pad.Render();

            for (var i = 0; i < rows.Count; i++)
            {
                if (AnsiText.StripEscapes(rows[i]).Contains(label, StringComparison.Ordinal))
                    return pad.Row + i;
            }

            Assert.Fail("no row was drawn carrying \"" + label + "\"");
            return -1;
        }

        /// <summary>Which screen column a label was drawn in, measured the same way and for the same reason.</summary>
        private static int ColumnOf(Keypad pad, string label)
        {
            foreach (var row in pad.Render())
            {
                var at = AnsiText.StripEscapes(row).IndexOf(label, StringComparison.Ordinal);

                if (at >= 0)
                    return pad.Column + at;
            }

            Assert.Fail("no row was drawn carrying \"" + label + "\"");
            return -1;
        }

        [Fact]
        public void ThePadIsAsBigAsItsKeysAndTheRulesAroundThem()
        {
            var pad = NewPad(new List<string>());

            // Three columns of five, plus the four rules that fence them.
            Assert.Equal(3, pad.Columns);
            Assert.Equal(19, pad.Width);

            // A row of faces and a rule between each, with one round the outside.
            Assert.Equal(5, pad.Height);

            foreach (var row in pad.Render())
                Assert.Equal(pad.Width, row.Length);

            Assert.Equal(pad.Height, pad.Render().Count);
        }

        [Fact]
        public void WhereAKeyIsDrawnIsWhereAPressOnItLands()
        {
            // The whole justification for the control. Both halves come off Render's own output, so a change that
            // moved the drawing without moving the hit test fails here.
            var pressed = new List<string>();
            var pad = NewPad(pressed);

            foreach (var label in new[] {"7", "8", "9", "."})
            {
                var row = RowOf(pad, label);
                var column = ColumnOf(pad, label);

                Assert.Equal(label, pad.ButtonAt(row, column).Label);
                Assert.True(pad.Press(row, column));
            }

            Assert.Equal(new[] {"7", "8", "9", "."}, pressed);
        }

        [Fact]
        public void AKeySpanningTwoColumnsAnswersAcrossBothOfThem()
        {
            var pressed = new List<string>();
            var pad = NewPad(pressed);

            var row = RowOf(pad, "0");
            var start = ColumnOf(pad, "0");

            // Eleven columns: two lots of five, plus the rule it swallowed. Pressing anywhere along it is the
            // same key, which is the thing a fixed column width gets wrong.
            for (var offset = -3; offset <= 3; offset++)
                Assert.Equal("0", pad.ButtonAt(row, start + offset).Label);

            Assert.True(pad.Press(row, start + 3));
            Assert.Equal(new[] {"0"}, pressed);
        }

        [Fact]
        public void TheRulesBetweenTheKeysBelongToNobody()
        {
            var pressed = new List<string>();
            var pad = NewPad(pressed);

            var row = RowOf(pad, "7");

            // The pad's own left edge, and the rule between the first key and the second.
            Assert.Null(pad.ButtonAt(row, pad.Column));
            Assert.Null(pad.ButtonAt(row, pad.Column + 6));

            // Rounding a press to the nearer key would press one nobody pointed at.
            Assert.False(pad.Press(row, pad.Column));
            Assert.Empty(pressed);
        }

        [Fact]
        public void TheRulesBetweenTheRowsBelongToNobodyEither()
        {
            var pad = NewPad(new List<string>());

            // The rules are the even rows of the pad, and the faces the odd ones.
            Assert.Null(pad.ButtonAt(pad.Row, pad.Column + 3));
            Assert.Null(pad.ButtonAt(pad.Row + 2, pad.Column + 3));
            Assert.NotNull(pad.ButtonAt(pad.Row + 1, pad.Column + 3));
        }

        [Fact]
        public void APressAnywhereOffThePadIsNotThePadsBusiness()
        {
            var pressed = new List<string>();
            var pad = NewPad(pressed);

            Assert.False(pad.Press(pad.Row - 1, pad.Column + 3));
            Assert.False(pad.Press(pad.Row + pad.Height, pad.Column + 3));
            Assert.False(pad.Press(pad.Row + 1, pad.Column - 1));
            Assert.False(pad.Press(pad.Row + 1, pad.Column + pad.Width));

            Assert.Empty(pressed);
        }

        [Fact]
        public void TheSpanChangesTheJunctionsAboveItRatherThanBeingDrawnOver()
        {
            var pad = NewPad(new List<string>());
            var rows = pad.Render();

            // Between the two rows: above sits the rule dividing 8 from 9, below sits the middle of the wide zero,
            // so the junction there has a line arriving from above and none from below.
            var between = rows[2];

            Assert.Equal('┴', between[6]);

            // And where both rows have a rule, it is a proper cross.
            Assert.Equal('┼', between[12]);
        }

        [Fact]
        public void AShortRowStillClosesItsBorder()
        {
            var pad = new Keypad(
                new KeypadRow(new KeypadButton("a", () => { }), new KeypadButton("b", () => { })),
                new KeypadRow(new KeypadButton("c", () => { })))
            {
                ButtonWidth = 3
            };

            // The second row accounts for one column of two, and the pad still has to be a rectangle: an open
            // right-hand edge would leave the border hanging in the air.
            foreach (var row in pad.Render())
                Assert.Equal(pad.Width, row.Length);

            Assert.Equal('│', pad.Render()[3][pad.Width - 1]);
        }

        [Fact]
        public void HoveringLightsAKeyAndMovingOffPutsItOut()
        {
            var pad = NewPad(new List<string>());
            var row = RowOf(pad, "8");

            Assert.True(pad.Hover(row, ColumnOf(pad, "8")));
            Assert.Equal("8", pad.Hovered.Label);

            // The same cell again has not moved, which is what lets a caller skip a redraw for a pointer that
            // crossed a cell it was already in.
            Assert.False(pad.Hover(row, ColumnOf(pad, "8")));

            // Off the pad entirely. A key left lit after the pointer has gone is one the user believes they are
            // about to press.
            Assert.True(pad.Hover(row, pad.Column - 5));
            Assert.Null(pad.Hovered);
        }

        [Fact]
        public void AKeyThatCannotBePressedIsNotLitAndDoesNotRun()
        {
            var pressed = new List<string>();

            var pad = new Keypad(
                new KeypadRow(
                    new KeypadButton("MR", () => pressed.Add("mr")) {EnabledWhen = () => false},
                    new KeypadButton("7", () => pressed.Add("7"))))
            {
                ButtonWidth = 4
            };

            var column = ColumnOf(pad, "MR");

            Assert.False(pad.Press(1, column));
            Assert.False(pad.Hover(1, column));
            Assert.Null(pad.Hovered);
            Assert.Empty(pressed);

            // And the hit test still knows it is there, which is what the drawing needs.
            Assert.Equal("MR", pad.ButtonAt(1, column).Label);
        }

        [Fact]
        public void AKeyWithNothingToDoIsAsDeadAsOneSwitchedOff()
        {
            // A label with no action is a legitimate thing to put on a pad, and it must not look pressable.
            var pad = new Keypad(new KeypadRow(new KeypadButton("---")));

            Assert.False(pad.Press(1, 3));
            Assert.False(pad.Hover(1, 3));
        }

        [Fact]
        public void ADeadKeyIsDrawnDifferentlyFromALiveOne()
        {
            var grey = new TextStyle(ConsoleColor.DarkGray, ConsoleColor.Gray);

            var pad = new Keypad(
                new KeypadRow(
                    new KeypadButton("MR", () => { }) {EnabledWhen = () => false},
                    new KeypadButton("7", () => { })))
            {
                ButtonWidth = 4,
                ColorMode = AnsiColorModeEnum.Palette256,
                ButtonStyle = new TextStyle(ConsoleColor.Black, ConsoleColor.Gray),
                DisabledStyle = grey
            };

            // Without this the predicate is invisible: the key refuses the pointer and does nothing when pressed,
            // with nothing on screen saying why, which reads as a broken pad rather than as a greyed key.
            Assert.Contains(grey.OpenSequence(AnsiColorModeEnum.Palette256), pad.Render()[1],
                StringComparison.Ordinal);
        }

        [Fact]
        public void WithNoDisabledStyleADeadKeyIsPaintedLikeTheRestOfThePad()
        {
            var face = new TextStyle(ConsoleColor.Black, ConsoleColor.Gray);

            string Draw(bool enabled)
            {
                var pad = new Keypad(
                    new KeypadRow(
                        new KeypadButton("7", () => { }),
                        new KeypadButton("MR", () => { }) {EnabledWhen = () => enabled}))
                {
                    ButtonWidth = 4,
                    ColorMode = AnsiColorModeEnum.Palette256,
                    ButtonStyle = face
                };

                return pad.Render()[1];
            }

            // The compatibility half, and the reason the property is nullable. Unset means "not specified" and
            // falls back to the face style, so a pad that never asked draws exactly what it always drew; an empty
            // style would paint the key unstyled and punch a hole in a coloured pad.
            Assert.Equal(Draw(true), Draw(false));
        }

        [Fact]
        public void APadNobodyColouredIsPlainText()
        {
            var pad = NewPad(new List<string>());

            // The library's standing rule: no escape, not even a reset, when nothing asked for one.
            foreach (var row in pad.Render())
                Assert.Equal(row, AnsiText.StripEscapes(row));
        }

        [Fact]
        public void AnEmptyPadHasNoSizeAndDrawsNothing()
        {
            var pad = new Keypad();

            Assert.Equal(0, pad.Width);
            Assert.Equal(0, pad.Height);
            Assert.Empty(pad.Render());
            Assert.False(pad.Press(0, 0));
        }
    }
}
