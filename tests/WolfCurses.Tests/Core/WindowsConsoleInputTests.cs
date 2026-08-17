using System;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfCurses.Core;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     The two translations that turn a Windows console input record into something the rest of the library
    ///     speaks.
    ///     <para>
    ///         <b>These are the only part of the mouse feature a machine with no console can check, which is why
    ///         they are pure static functions taking scalars rather than methods on the reader.</b> Everything else
    ///         — whether the console hands over mouse records at all, whether QuickEdit ate the click first, whether
    ///         the terminal even has a pointer — is unobservable from here and has to be confirmed by a person.
    ///     </para>
    /// </summary>
    public class WindowsConsoleInputTests
    {
        private const uint ShiftPressed = 0x0010;
        private const uint LeftCtrlPressed = 0x0008;
        private const uint LeftAltPressed = 0x0002;
        private const uint MouseWheeled = 0x0004;
        private const uint MouseMoved = 0x0001;

        [Fact]
        public void TheInteropRecordMatchesWhatWindowsActuallyWrites()
        {
            // The one failure in this feature that corrupts silently instead of crashing. INPUT_RECORD is a union:
            // key and mouse records are read through the same sixteen bytes, so a field at the wrong offset reads a
            // mouse coordinate out of a key's flags and never faults while doing it. Twenty bytes total, and each
            // offset is checked against the wincon.h layout rather than against whatever the compiler chose.
            var record = typeof (WindowsConsoleInput)
                .GetNestedType("InputRecord", BindingFlags.NonPublic);

            Assert.NotNull(record);
            Assert.Equal(20, Marshal.SizeOf(record));

            var expected = new (string Field, int Offset)[]
            {
                ("EventType", 0),
                ("KeyDown", 4),
                ("RepeatCount", 8),
                ("VirtualKeyCode", 10),
                ("UnicodeChar", 14),
                ("KeyControlKeyState", 16),
                ("MousePositionX", 4),
                ("MousePositionY", 6),
                ("ButtonState", 8),
                ("MouseControlKeyState", 12),
                ("EventFlags", 16)
            };

            foreach (var (field, offset) in expected)
                Assert.Equal(offset, (int) Marshal.OffsetOf(record, field));
        }

        [Fact]
        public void TheCharacterFieldIsWideEnoughForACharacter()
        {
            // Declared as a ushort and never as a char, because a char field marshals under the STRUCT's CharSet -
            // which defaults to Ansi, one byte - and would silently truncate every character above U+00FF. The
            // total size cannot catch that, since the mis-sized layout pads back to the same twenty bytes.
            var record = typeof (WindowsConsoleInput).GetNestedType("InputRecord", BindingFlags.NonPublic);
            var character = record.GetField("UnicodeChar",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(character);
            Assert.Equal(typeof (ushort), character.FieldType);
        }

        // ------------------------------------------------------------ keys

        [Fact]
        public void AKeyGoingDownBecomesTheKeyPressItObviouslyIs()
        {
            var translated = WindowsConsoleInput.TryTranslateKey(true, 0x41, 'a', 0, out var key);

            Assert.True(translated);
            Assert.Equal(ConsoleKey.A, key.Key);
            Assert.Equal('a', key.KeyChar);
            Assert.Equal((ConsoleModifiers) 0, key.Modifiers);
        }

        [Fact]
        public void AKeyComingBackUpIsNotAPress()
        {
            // Console.ReadKey reports presses only, and every override in this library was written expecting that.
            // Reporting releases too would fire each handler twice per keystroke.
            Assert.False(WindowsConsoleInput.TryTranslateKey(false, 0x41, 'a', 0, out _));
        }

        [Theory]
        [InlineData(0x10)] // Shift
        [InlineData(0x11)] // Control
        [InlineData(0x12)] // Alt
        [InlineData(0x14)] // CapsLock
        [InlineData(0x90)] // NumLock
        [InlineData(0x91)] // ScrollLock
        public void AModifierPressedOnItsOwnIsNotAKeyPress(ushort virtualKey)
        {
            Assert.False(WindowsConsoleInput.TryTranslateKey(true, virtualKey, '\0', 0, out _));
        }

        [Fact]
        public void ModifiersRideAlongWithTheKeyTheyModify()
        {
            var translated = WindowsConsoleInput.TryTranslateKey(
                true, 0x41, '', ShiftPressed | LeftCtrlPressed, out var key);

            Assert.True(translated);
            Assert.True(key.Modifiers.HasFlag(ConsoleModifiers.Shift));
            Assert.True(key.Modifiers.HasFlag(ConsoleModifiers.Control));
            Assert.False(key.Modifiers.HasFlag(ConsoleModifiers.Alt));
        }

        [Fact]
        public void AnArrowKeyStillArrivesEvenThoughItHasNoCharacter()
        {
            // The whole reason there are two input paths in this library: an arrow has no character to give the
            // buffer, so if it did not survive translation every steered game would stop working.
            var translated = WindowsConsoleInput.TryTranslateKey(true, 0x26, '\0', 0, out var key);

            Assert.True(translated);
            Assert.Equal(ConsoleKey.UpArrow, key.Key);
            Assert.Equal('\0', key.KeyChar);
        }

        [Theory]
        [InlineData(0x24)] // Home, within the navigation cluster
        [InlineData(0x61)] // numpad 1
        [InlineData(0x2D)] // Insert
        public void AltHeldOverTheNumpadIsCharacterCompositionRatherThanKeyPresses(ushort virtualKey)
        {
            // Windows composes a character out of Alt plus digits and delivers it separately. Passing the individual
            // keys through as well would fire an arrow or Home press for every composed character typed.
            Assert.False(WindowsConsoleInput.TryTranslateKey(true, virtualKey, '\0', LeftAltPressed, out _));
        }

        [Fact]
        public void AVirtualKeyCodeTooLargeToBeAConsoleKeyIsDroppedRatherThanThrown()
        {
            // ConsoleKeyInfo's constructor throws on one of these, and throwing here would come out of the middle of
            // a tick, past a catch that is only looking for InvalidOperationException.
            var exception = Record.Exception(
                () => WindowsConsoleInput.TryTranslateKey(true, 0x1FF, 'x', 0, out _));

            Assert.Null(exception);
            Assert.False(WindowsConsoleInput.TryTranslateKey(true, 0x1FF, 'x', 0, out _));
        }

        // ------------------------------------------------------------ mouse

        [Fact]
        public void AButtonGoingDownBecomesAPressAtThatCell()
        {
            var translated = WindowsConsoleInput.TryTranslateMousePress(
                40, 12, 0, 0x0001, 0, 0, 0, out var mouse);

            Assert.True(translated);
            Assert.Equal(40, mouse.Column);
            Assert.Equal(12, mouse.Row);
            Assert.Equal(MouseButtonEnum.Left, mouse.Button);
        }

        [Fact]
        public void TheRowIsMeasuredFromTheWindowAndNotFromTheScrollbackBuffer()
        {
            // THE trap. Windows reports the position in screen-BUFFER space, so on a console that has scrolled the
            // buffer row and the window row differ by however far it scrolled. Without this correction every click
            // lands that many rows away and nothing anywhere reports a failure.
            var translated = WindowsConsoleInput.TryTranslateMousePress(
                5, 312, 300, 0x0001, 0, 0, 0, out var mouse);

            Assert.True(translated);
            Assert.Equal(12, mouse.Row);
        }

        [Fact]
        public void AButtonAlreadyHeldIsNotANewPress()
        {
            // Which is what makes a drag produce nothing: the button was down before and is down now.
            Assert.False(WindowsConsoleInput.TryTranslateMousePress(
                40, 12, 0, 0x0001, 0x0001, 0, MouseMoved, out _));
        }

        [Fact]
        public void ReleasingAButtonIsNotAPress()
        {
            Assert.False(WindowsConsoleInput.TryTranslateMousePress(
                40, 12, 0, 0, 0x0001, 0, 0, out _));
        }

        [Fact]
        public void MovingThePointerWithNothingHeldIsNotAPress()
        {
            Assert.False(WindowsConsoleInput.TryTranslateMousePress(
                40, 12, 0, 0, 0, 0, MouseMoved, out _));
        }

        [Fact]
        public void TheWheelIsNotAClickEvenThoughTheConsoleReportsItLikeOne()
        {
            // A wheel record arrives with its notch count in the HIGH word of the same field the buttons live in, so
            // an implementation that only diffed button states would see every scroll as a button going down - and a
            // fire-on-click game would empty its magazine when the player scrolled.
            const uint oneNotchUp = 0x00780000;

            Assert.False(WindowsConsoleInput.TryTranslateMousePress(
                40, 12, 0, oneNotchUp, 0, 0, MouseWheeled, out _));
        }

        [Fact]
        public void AWheelNotchDoesNotLeaveAPhantomButtonBehindIt()
        {
            // The masking half of the same trap: after a wheel record the remembered button state must not include
            // the notch count, or the NEXT real click diffs against garbage.
            const uint oneNotchUp = 0x00780000;

            var translated = WindowsConsoleInput.TryTranslateMousePress(
                40, 12, 0, 0x0001, oneNotchUp, 0, 0, out var mouse);

            Assert.True(translated, "a real left press arriving after a wheel notch was swallowed");
            Assert.Equal(MouseButtonEnum.Left, mouse.Button);
        }

        [Theory]
        [InlineData(0x0001, MouseButtonEnum.Left)]
        [InlineData(0x0002, MouseButtonEnum.Right)]
        [InlineData(0x0004, MouseButtonEnum.Middle)]
        public void EachButtonIsReportedAsItself(uint buttonBit, MouseButtonEnum expected)
        {
            // Windows numbers these in its own order - bit 1 is the RIGHTMOST button, not the middle one - so this
            // is a mapping and not a cast, and getting it wrong silently swaps two buttons.
            var translated = WindowsConsoleInput.TryTranslateMousePress(
                1, 1, 0, buttonBit, 0, 0, 0, out var mouse);

            Assert.True(translated);
            Assert.Equal(expected, mouse.Button);
        }

        [Fact]
        public void ModifiersHeldDuringAClickComeThrough()
        {
            var translated = WindowsConsoleInput.TryTranslateMousePress(
                1, 1, 0, 0x0001, 0, ShiftPressed | LeftAltPressed, 0, out var mouse);

            Assert.True(translated);
            Assert.True(mouse.Modifiers.HasFlag(ConsoleModifiers.Shift));
            Assert.True(mouse.Modifiers.HasFlag(ConsoleModifiers.Alt));
            Assert.False(mouse.Modifiers.HasFlag(ConsoleModifiers.Control));
        }

        [Fact]
        public void AClickAboveTheTopOfTheWindowIsRefusedRatherThanReportedNegative()
        {
            Assert.False(WindowsConsoleInput.TryTranslateMousePress(
                5, 2, 300, 0x0001, 0, 0, 0, out _));
        }
    }
}
