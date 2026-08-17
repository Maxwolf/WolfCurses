// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Runtime.InteropServices;

namespace WolfCurses.Core
{
    /// <summary>
    ///     Reads the Windows console input queue directly, so mouse presses can be seen at all.
    ///     <para>
    ///         <b>This exists because <c>Console.KeyAvailable</c> destroys mouse events.</b> It peeks the console
    ///         queue and, for every record that is not a key-down, calls <c>ReadConsoleInput</c> to throw it away
    ///         before returning — so the very call <see cref="InputManager" /> makes to find out whether a key is
    ///         waiting is what shreds the mouse. Worse, that discard loop is <i>unbounded</i>: it walks past every
    ///         leading non-key-down record, so a click sitting behind a key-<i>up</i> is destroyed whenever the
    ///         player releases a key while pointing. There is therefore no way to run a mouse reader beside
    ///         <c>Console.ReadKey</c>; whichever path is live has to own the read, and this class only becomes live
    ///         while a host has asked for the mouse.
    ///     </para>
    ///     <para>
    ///         The two translations are <b>pure static functions taking scalars</b>
    ///         (<see cref="TryTranslateKey" />, <see cref="TryTranslateMousePress" />) precisely so the interesting
    ///         half can be tested with hand-built records on a machine that has no console at all — which is every
    ///         continuous-integration machine, and was the machine this was written on.
    ///     </para>
    /// </summary>
    internal sealed class WindowsConsoleInput
    {
        private const int StdInputHandle = -10;

        private const uint EnableMouseInput = 0x0010;
        private const uint EnableQuickEditMode = 0x0040;
        private const uint EnableExtendedFlags = 0x0080;

        /// <summary>Console input event types, from wincon.h.</summary>
        private const ushort KeyEvent = 0x0001;

        private const ushort MouseEvent = 0x0002;

        /// <summary>Mouse event flags. A plain press has none of them set.</summary>
        private const uint MouseWheeled = 0x0004;

        private const uint MouseHorizontalWheeled = 0x0008;

        /// <summary>Control key state bits shared by key and mouse records.</summary>
        private const uint RightAltPressed = 0x0001;

        private const uint LeftAltPressed = 0x0002;
        private const uint RightCtrlPressed = 0x0004;
        private const uint LeftCtrlPressed = 0x0008;
        private const uint ShiftPressed = 0x0010;

        /// <summary>How many records to pull per syscall. Reused, so a busy frame allocates nothing.</summary>
        private readonly InputRecord[] _records = new InputRecord[32];

        private readonly IntPtr _handle;
        private readonly uint _savedMode;

        /// <summary>Which buttons were down last time, so a press can be told from a release or a drag.</summary>
        private uint _previousButtons;

        private WindowsConsoleInput(IntPtr handle, uint savedMode)
        {
            _handle = handle;
            _savedMode = savedMode;
        }

        /// <summary>The installed reader, or null when no host has successfully asked for the mouse.</summary>
        internal static WindowsConsoleInput Active { get; private set; }

        /// <summary>
        ///     Puts the console into mouse-reporting mode and installs the reader, or changes nothing and answers
        ///     false.
        /// </summary>
        /// <returns>TRUE when the mouse is now readable.</returns>
        internal static bool TryEnable()
        {
            if (Active != null)
                return true;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            try
            {
                var handle = GetStdHandle(StdInputHandle);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                    return false;

                // Fails for a redirected handle, which is how a headless host is recognised without asking.
                if (!GetConsoleMode(handle, out var mode))
                    return false;

                // READ-MODIFY-WRITE, always, starting from what the console actually reported. Building the mode out
                // of constants would clear ENABLE_PROCESSED_INPUT, at which point Ctrl+C stops raising
                // Console.CancelKeyPress and arrives as a key with character 0x03 instead - taking the host's only
                // quit path away while leaving mouse reporting switched on.
                //
                // ENABLE_EXTENDED_FLAGS has to be set in the SAME call that clears ENABLE_QUICK_EDIT_MODE or the
                // clear is silently ignored and SetConsoleMode still reports success. QuickEdit is on by default and
                // is what makes the console swallow left-button presses for its own text selection, which is exactly
                // the event we are here for.
                var wanted = (mode | EnableMouseInput | EnableExtendedFlags) & ~EnableQuickEditMode;
                if (!SetConsoleMode(handle, wanted))
                    return false;

                Active = new WindowsConsoleInput(handle, mode);
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>Puts the console mode back the way it was. Idempotent, and never throws.</summary>
        internal static void Disable()
        {
            var active = Active;
            if (active == null)
                return;

            Active = null;

            try
            {
                // The extended flag rides along on the way out too, or the saved QuickEdit bit is ignored exactly as
                // it was on the way in and the user's text selection never comes back.
                SetConsoleMode(active._handle, active._savedMode | EnableExtendedFlags);
            }
            catch (DllNotFoundException)
            {
                // Nothing to restore on a platform that never had it.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        /// <summary>
        ///     Hands every record waiting in the console queue to one of the two callbacks, and returns having read
        ///     nothing further.
        ///     <para>
        ///         Gated on <c>GetNumberOfConsoleInputEvents</c> because <c>ReadConsoleInput</c> <b>blocks</b> on an
        ///         empty queue, and a blocking read inside a system tick is a frozen application rather than a slow
        ///         one. A <c>while</c> loop rather than one batch per tick, for the same reason the code it replaces
        ///         is: a held key repeats about thirty times a second, and draining less than arrives means a
        ///         permanent and growing backlog.
        ///     </para>
        /// </summary>
        /// <param name="onKey">Called for each key press, already translated.</param>
        /// <param name="onMousePress">Called for each button-down.</param>
        internal void Drain(Action<ConsoleKeyInfo> onKey, Action<MouseEvent> onMousePress)
        {
            try
            {
                // Read once into a local: it is a syscall, and the value is only used to size this drain.
                var windowTop = SafeWindowTop();

                while (true)
                {
                    if (!GetNumberOfConsoleInputEvents(_handle, out var pending) || pending == 0)
                        return;

                    var wanted = (uint) Math.Min(pending, (uint) _records.Length);
                    if (!ReadConsoleInput(_handle, _records, wanted, out var read) || read == 0)
                        return;

                    for (var i = 0; i < read; i++)
                        Dispatch(_records[i], windowTop, onKey, onMousePress);
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
                // Stand down rather than throwing out of a tick: the next tick falls back to Console.ReadKey, which
                // means a degraded application instead of a dead one.
                Disable();
            }
        }

        /// <summary>Routes one record, dropping the kinds nothing here cares about.</summary>
        private void Dispatch(InputRecord record, int windowTop,
            Action<ConsoleKeyInfo> onKey, Action<MouseEvent> onMousePress)
        {
            switch (record.EventType)
            {
                case KeyEvent:
                    if (!TryTranslateKey(record.KeyDown != 0, record.VirtualKeyCode, (char) record.UnicodeChar,
                            record.KeyControlKeyState, out var key))
                        return;

                    // The console coalesces a held key into one record with a repeat count, so it is expanded back
                    // out here - the caller is counting presses, not records.
                    var repeats = record.RepeatCount < 1 ? 1 : record.RepeatCount;
                    for (var i = 0; i < repeats; i++)
                        onKey(key);

                    return;

                case MouseEvent:
                    var previous = _previousButtons;

                    // Masked to the five real buttons before anything else: a wheel record carries its signed notch
                    // count in the HIGH word of the same field, so an unmasked compare sees every scroll as a button
                    // transition.
                    _previousButtons = record.ButtonState & 0x1F;

                    if (TryTranslateMousePress(record.MousePositionX, record.MousePositionY, windowTop,
                            record.ButtonState, previous, record.MouseControlKeyState, record.EventFlags,
                            out var mouse))
                        onMousePress(mouse);

                    return;

                default:
                    // Window-resize, focus and menu records, dropped exactly as Console.KeyAvailable drops them.
                    return;
            }
        }

        /// <summary>
        ///     Turns a console key record into a <see cref="ConsoleKeyInfo" />, or answers false for the records that
        ///     are not key presses at all.
        ///     <para>
        ///         Reproduces what <c>Console.ReadKey</c> skips, because the rest of this library was written against
        ///         its behaviour: key-up records, the modifier keys pressed on their own, and the Alt+numpad
        ///         character-composition traffic that would otherwise arrive as a burst of arrow and Home presses.
        ///         <c>ConsoleKey</c>'s members <i>are</i> the Win32 virtual-key codes, so the key itself is a cast
        ///         and there is no mapping table here to get wrong.
        ///     </para>
        /// </summary>
        /// <param name="keyDown">Whether this is a press rather than a release.</param>
        /// <param name="virtualKeyCode">The Win32 virtual-key code.</param>
        /// <param name="unicodeChar">The character the console decoded, or nul.</param>
        /// <param name="controlKeyState">The modifier bits.</param>
        /// <param name="key">The translated key press.</param>
        /// <returns>TRUE when this record is a key press worth reporting.</returns>
        internal static bool TryTranslateKey(bool keyDown, ushort virtualKeyCode, char unicodeChar,
            uint controlKeyState, out ConsoleKeyInfo key)
        {
            key = default;

            if (!keyDown)
                return false;

            // A virtual-key code outside a byte cannot be a ConsoleKey, and handing one to the ConsoleKeyInfo
            // constructor throws out of the middle of a tick.
            if (virtualKeyCode > 0xFF)
                return false;

            var alt = (controlKeyState & (LeftAltPressed | RightAltPressed)) != 0;
            var control = (controlKeyState & (LeftCtrlPressed | RightCtrlPressed)) != 0;
            var shift = (controlKeyState & ShiftPressed) != 0;

            if (unicodeChar == '\0')
            {
                // Shift, Control, Alt, CapsLock, NumLock and ScrollLock pressed by themselves. They carry no
                // character and are not presses anybody handles; reporting them would fire every OnKeyPressed
                // override twice per real keystroke.
                switch (virtualKeyCode)
                {
                    case 0x10: // VK_SHIFT
                    case 0x11: // VK_CONTROL
                    case 0x12: // VK_MENU
                    case 0x14: // VK_CAPITAL
                    case 0x90: // VK_NUMLOCK
                    case 0x91: // VK_SCROLL
                        return false;
                }
            }

            // Alt held over the numpad and the navigation cluster is Windows composing a character out of digits.
            // The individual keys are not presses the application should see; the composed character arrives on its
            // own afterwards.
            if (alt && (virtualKeyCode is >= 0x21 and <= 0x28 or >= 0x60 and <= 0x69 or 0x0C or 0x2D or 0x91))
                return false;

            key = new ConsoleKeyInfo(unicodeChar, (ConsoleKey) virtualKeyCode, shift, alt, control);
            return true;
        }

        /// <summary>
        ///     Turns a console mouse record into a <see cref="MouseEvent" />, or answers false for everything that is
        ///     not a button going down.
        ///     <para>
        ///         Motion, drags and releases all fall out with no special case at all: the press set is
        ///         <c>now AND NOT previous</c>, which is empty for every one of them. The wheel is refused explicitly
        ///         instead, because a wheel record arrives with a button bit set and would otherwise read as a click.
        ///     </para>
        /// </summary>
        /// <param name="bufferX">Column in console screen-buffer space.</param>
        /// <param name="bufferY">Row in console screen-buffer space.</param>
        /// <param name="windowTop">The buffer row currently at the top of the window.</param>
        /// <param name="buttonState">Which buttons are down now.</param>
        /// <param name="previousButtonState">Which buttons were down at the previous record.</param>
        /// <param name="controlKeyState">The modifier bits.</param>
        /// <param name="eventFlags">The mouse event flags.</param>
        /// <param name="mouse">The translated press.</param>
        /// <returns>TRUE when this record is a button going down.</returns>
        internal static bool TryTranslateMousePress(short bufferX, short bufferY, int windowTop,
            uint buttonState, uint previousButtonState, uint controlKeyState, uint eventFlags,
            out MouseEvent mouse)
        {
            mouse = default;

            if ((eventFlags & (MouseWheeled | MouseHorizontalWheeled)) != 0)
                return false;

            var pressed = (buttonState & 0x1F) & ~(previousButtonState & 0x1F);
            if (pressed == 0)
                return false;

            var button = (pressed & 0x0001) != 0 ? MouseButtonEnum.Left
                : (pressed & 0x0002) != 0 ? MouseButtonEnum.Right
                : (pressed & 0x0004) != 0 ? MouseButtonEnum.Middle
                : MouseButtonEnum.None;

            if (button == MouseButtonEnum.None)
                return false;

            // Buffer coordinates, not window coordinates. On a console with scrollback the two differ by however far
            // the view has scrolled, and without this every click lands that many rows off with nothing appearing to
            // have failed.
            var row = bufferY - windowTop;
            if (row < 0 || bufferX < 0)
                return false;

            var modifiers = (ConsoleModifiers) 0;
            if ((controlKeyState & ShiftPressed) != 0)
                modifiers |= ConsoleModifiers.Shift;
            if ((controlKeyState & (LeftAltPressed | RightAltPressed)) != 0)
                modifiers |= ConsoleModifiers.Alt;
            if ((controlKeyState & (LeftCtrlPressed | RightCtrlPressed)) != 0)
                modifiers |= ConsoleModifiers.Control;

            mouse = new MouseEvent(bufferX, row, button, modifiers);
            return true;
        }

        /// <summary>Which buffer row is at the top of the window, or zero when that cannot be asked.</summary>
        private static int SafeWindowTop()
        {
            try
            {
                return Console.WindowTop;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        ///     One console input record. <see cref="StructLayout" /> is explicit because the payload is a union in C
        ///     and the two record kinds are read through the same sixteen bytes; laying it out sequentially would
        ///     read mouse coordinates out of a key record's flags and never crash while doing it.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 20)]
        private struct InputRecord
        {
            [FieldOffset(0)] public ushort EventType;

            // KEY_EVENT_RECORD
            [FieldOffset(4)] public int KeyDown;
            [FieldOffset(8)] public ushort RepeatCount;
            [FieldOffset(10)] public ushort VirtualKeyCode;

            // Deliberately a ushort and never a char: a char field marshals under the STRUCT's CharSet, which
            // defaults to Ansi - one byte - silently truncating every character above U+00FF. Marshal.SizeOf cannot
            // catch it either, because the mis-sized layout pads back to the same total.
            [FieldOffset(14)] public ushort UnicodeChar;

            [FieldOffset(16)] public uint KeyControlKeyState;

            // MOUSE_EVENT_RECORD, overlapping the above on purpose - that is what a union is.
            [FieldOffset(4)] public short MousePositionX;
            [FieldOffset(6)] public short MousePositionY;
            [FieldOffset(8)] public uint ButtonState;
            [FieldOffset(12)] public uint MouseControlKeyState;
            [FieldOffset(16)] public uint EventFlags;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNumberOfConsoleInputEvents(IntPtr hConsoleInput, out uint lpcNumberOfEvents);

        [DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", ExactSpelling = true, SetLastError = true)]
        private static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] InputRecord[] lpBuffer,
            uint nLength, out uint lpNumberOfEventsRead);
    }
}
