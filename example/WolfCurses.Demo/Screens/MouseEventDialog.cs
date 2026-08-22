// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     Every <see cref="MouseEvent" /> the host reports, written down as it arrives: the
    ///     <see cref="MouseEvent.Kind" />, the <see cref="MouseEvent.Button" />, the cell, the
    ///     <see cref="MouseEvent.Modifiers" /> and the <see cref="MouseEvent.WheelDelta" /> in notches. It is the only
    ///     screen in this app where the three kinds added after presses can be watched arriving, and the only place
    ///     the wheel and the modifiers can be seen at all.
    ///     <para>
    ///         <b>The last few events are kept as well as the current one, because the interesting part is the
    ///         sequence.</b> A press, then a run of moves each carrying that same button, then a release: that is a
    ///         drag, and it is a shape rather than an event. No single line of the readout says "drag" and none could;
    ///         it is only visible over time, which is exactly the argument for shipping the other three kinds at all.
    ///     </para>
    ///     <para>
    ///         <b>A move carries whichever buttons are still held</b>, so a drag is a move with a button on it and a
    ///         bare hover is a move carrying <see cref="MouseButtonEnum.None" />. That one fact is the whole of what
    ///         tells the two apart without inventing a further kind, and it is why the release above is belt and
    ///         braces rather than the thing that ends a drag.
    ///     </para>
    ///     <para>
    ///         <b>The wheel is its own kind and not a button</b>, and <see cref="MouseButtonEnum" /> still has no
    ///         wheel member. Every protocol in use encodes a notch as a button <i>press</i>, so the obvious handler
    ///         ("on a press, do the thing") empties a fire-on-click game's magazine the moment somebody scrolls.
    ///         Giving the wheel a kind of its own removes that at the root: nothing which handles a press can be
    ///         reached by a notch. The sideways wheel is refused outright and reaches this screen as nothing at all,
    ///         because through one delta field a horizontal flick would scroll a document vertically.
    ///     </para>
    ///     <para>
    ///         <b>A press arrives at <see cref="OnMousePressed" /> and never at <see cref="OnMouseEvent" />.</b>
    ///         <c>Window.OnMouseEvent</c> hands a press to the older method and returns, so this screen needs both
    ///         overrides to see all four kinds; writing the press handling in <see cref="OnMouseEvent" /> compiles and
    ///         runs never. The sprite collision demo records the same trap, where it cost a red test.
    ///     </para>
    ///     <para>
    ///         <b><see cref="MouseEvent.Row" /> is window-relative, not buffer-relative</b>, which is what makes the
    ///         trail in the pad land under the pointer: the pad is drawn at known window rows, so the reported row is
    ///         an index into it with no correction at all. A buffer row would put every mark a screenful away on a
    ///         scrolled console, silently, since nothing about it looks like a failure.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class MouseEventDialog : Form<DemoWindowInfo>
    {
        /// <summary>
        ///     Rows spent on everything that is not the history table or the pad, counted so the two variable blocks
        ///     can be sized against what is left. Written down once because both of them read it; two spellings of
        ///     one budget is how a screen ends up one row too tall and loses its prompt off the bottom.
        /// </summary>
        private const int FixedRows = 14;

        /// <summary>
        ///     How many events are remembered. Both the history table and the pad's trail read this one list, which is
        ///     the point rather than a saving: the marks in the pad are the rows of the table, drawn where they
        ///     happened instead of when.
        /// </summary>
        private const int Remembered = 32;

        /// <summary>Narrowest pad worth drawing at all.</summary>
        private const int MinimumPadWidth = 20;

        private const char PressGlyph = '#';
        private const char DragGlyph = 'x';
        private const char HoverGlyph = '.';
        private const char ReleaseGlyph = 'o';
        private const char WheelUpGlyph = '^';
        private const char WheelDownGlyph = 'v';

        /// <summary>
        ///     What the pad's title says the glyphs mean, when the pad is wide enough to carry it. It opens by saying
        ///     why the pad is there at all: once reporting is on, a terminal stops drawing a pointer of its own, so
        ///     the only one on the screen is the one an application draws for itself.
        /// </summary>
        private const string TrailLegend = "no terminal draws a pointer:  # press  x drag  . hover  o release  ^v wheel";

        /// <summary>What the pad's title falls back to on a console too narrow for the legend.</summary>
        private const string ShortLegend = "trail";

        private static readonly TextStyle _headingStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null,
            true);

        private static readonly TextStyle _labelStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan));
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));
        private static readonly TextStyle _alarmStyle = new TextStyle(new TextColor(ConsoleColor.Red), null, true);

        // One colour per kind, shared by the readout's Kind field and by that event's mark in the pad, so the two are
        // obviously the same event rather than two coincidences. Named colours on purpose: a terminal theme gets to
        // have an opinion about them, and at a resolved mode of None every one of these emits nothing.
        private static readonly TextStyle _pressStyle = new TextStyle(new TextColor(ConsoleColor.Green));
        private static readonly TextStyle _releaseStyle = new TextStyle(new TextColor(ConsoleColor.DarkGreen));
        private static readonly TextStyle _dragStyle = new TextStyle(new TextColor(ConsoleColor.Magenta));
        private static readonly TextStyle _wheelStyle = new TextStyle(new TextColor(ConsoleColor.Cyan));

        /// <summary>A hover is drawn as quietly as the flood it is: hundreds of these arrive and none of them matter.</summary>
        private static readonly TextStyle _hoverStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        /// <summary>The events, oldest first. Newest last, so the trail can be drawn in arrival order.</summary>
        private readonly List<MouseEvent> _events = new List<MouseEvent>(Remembered);

        /// <summary>The frame, built when an event changed something and handed back unchanged by every render between.</summary>
        private string _rendered = string.Empty;

        /// <summary>
        ///     Whether an event has arrived since the frame was last built.
        ///     <para>
        ///         The flood is the reason this is a flag rather than a rebuild inside the handlers.
        ///         <c>InputManager</c> drains the console's queue with a <c>while</c>, so a fast sweep of the pointer
        ///         delivers a burst of moves inside one tick; composing per event would build a frame for each of
        ///         them and throw all but the last away unseen.
        ///     </para>
        /// </summary>
        private bool _dirty;

        private int _historyRows;
        private int _moves;
        private int _notches;

        /// <summary>
        ///     The window row the pad's top border sits on: this form's own rows, counted out of the builder while
        ///     the frame is being composed, plus the ones the scene graph writes above the form. Counted rather than
        ///     written down as a constant, which is right until a line is added above it, at which point every mark
        ///     lands a row out and nothing anywhere reports a problem.
        /// </summary>
        private int _padTop;

        private int _padRows;
        private int _padWidth;
        private int _presses;
        private int _releases;
        private int _width;
        private int _wheels;

        private Box _pad;

        /// <summary>Initializes a new instance of the <see cref="MouseEventDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public MouseEventDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = AnsiConsole.MouseEnabled
                ? "Move, click, drag and scroll anywhere; ENTER or ESC returns to the menu"
                : "This terminal reports no mouse; ENTER or ESC returns to the menu";

            // Asked for by the screen that wants it and handed back in OnFormClosing. Motion is one event for every
            // cell the pointer crosses, so it is opt-in per screen and every other demo goes on paying nothing for
            // it. This screen is the one place in the app where seeing that flood arrive is the entire point.
            SimUnit.InputManager.ReportsMouseMotion = true;

            // Each of these is a live syscall, so both are read once here and the layout is derived from them once.
            // The pad then keeps its layout: the drawing and the hit test read the same three numbers, which is the
            // only thing that stops the mark being drawn somewhere other than where the pointer was reported.
            _width = Math.Max(MinimumPadWidth + 2, AnsiConsole.SafeWindowWidth());
            _padWidth = _width - 2;

            // What is really available is four rows fewer than the console, and every one of the four was found by
            // counting a frame rather than by guessing: the presenter draws one row short of the height (nothing may
            // be printed into the cell that scrolls a terminal), the scene graph puts a blank line and then the
            // prompt under this form, and RowsAboveForm is written above it. Get this wrong upwards and the prompt
            // is the row that falls off, which is the one row the user is typing into.
            var budget = Math.Max(6, AnsiConsole.SafeWindowHeight() - 3 - RowsAboveForm() - FixedRows);

            // The table is capped and the pad takes everything left over, because the pad is the only part of this
            // screen a pointer can be over: a row it does not cover is a row where the mark cannot be drawn at all.
            _historyRows = Math.Clamp(budget / 3, 3, 8);
            _padRows = Math.Max(3, budget - _historyRows);

            _pad = new Box
            {
                BorderStyle = ConsoleColor.DarkGray,
                TitleStyle = ConsoleColor.DarkCyan,

                // A title longer than the interior grows the box, which would push it past the last column and have
                // the right-hand border truncated away rather than reflowed, since auto-wrap is off for the frame.
                Title = AnsiText.VisibleLength(TrailLegend) + 2 <= _padWidth ? TrailLegend : ShortLegend
            };

            Compose();
        }

        /// <summary>
        ///     Hands pointer reporting back, which is the half a form has nowhere else to put.
        ///     <para>
        ///         Being dropped is no signal at all, which is why <c>IForm.OnFormClosing</c> exists. Without this
        ///         every later demo and the menu itself would go on receiving one event per cell the pointer crossed
        ///         for the rest of the session, read by nobody. The library fires it from every path a form is
        ///         detached by, so ESC, ENTER, the window being removed and quitting are all covered by the one
        ///         override.
        ///     </para>
        /// </summary>
        public override void OnFormClosing()
        {
            base.OnFormClosing();

            SimUnit.InputManager.ReportsMouseMotion = false;
        }

        /// <summary>
        ///     Where a press lands, and where it has to be handled.
        ///     <para>
        ///         <b>A press never reaches <see cref="OnMouseEvent" />.</b> <c>Window.OnMouseEvent</c> hands it to
        ///         this method and returns, so a screen that wants all four kinds needs both overrides. Written the
        ///         other way it compiles, runs never, and leaves a screen that is deaf to exactly the kind everything
        ///         else was built on.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            Record(mouse);
        }

        /// <summary>
        ///     Where the other three land: a move, a release, and a wheel notch.
        ///     <para>
        ///         Moves arrive only because <c>InputManager.ReportsMouseMotion</c> was asked for above; the wheel
        ///         needs no opt-in, a notch not being a firehose. Nothing here filters by kind, which is unusual and
        ///         deliberate: every other screen wants one shape out of the stream and this one is the stream.
        ///     </para>
        ///     <para>
        ///         Deliberately no <c>base</c> call. The default implementation exists to forward a press to
        ///         <see cref="OnMousePressed" /> for forms written before motion existed, and a press cannot arrive
        ///         here, so calling it would only invite the reader to believe one can.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What the mouse did, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            Record(mouse);
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (!_dirty)
                return;

            _dirty = false;
            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // A pure read of what the tick built. This runs about a thousand times a second.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>Remembers one event and its arithmetic, and asks for a new frame on the next tick.</summary>
        /// <param name="mouse">The event exactly as the host reported it.</param>
        private void Record(MouseEvent mouse)
        {
            switch (mouse.Kind)
            {
                case MouseEventKindEnum.Press:
                    _presses++;
                    break;
                case MouseEventKindEnum.Release:
                    _releases++;
                    break;
                case MouseEventKindEnum.Wheel:
                    _wheels++;

                    // Counted in notches because that is what the library reports: Windows counts in 120ths of one
                    // and every caller would otherwise divide by the same constant to get back to the number a
                    // person's hand actually turned.
                    _notches += mouse.WheelDelta;
                    break;
                default:
                    _moves++;
                    break;
            }

            _events.Add(mouse);
            if (_events.Count > Remembered)
                _events.RemoveAt(0);

            _dirty = true;
        }

        /// <summary>Builds the whole frame, and notes where the pad landed while it is building it.</summary>
        private void Compose()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(_headingStyle.Apply(Clip("Mouse events, live - every MouseEvent as it arrives", _width)));
            sb.AppendLine(StatusLine());
            sb.AppendLine(WhatHappened());
            sb.AppendLine(WhereItLanded());
            sb.AppendLine(_labelStyle.Apply(Clip(Reading(), _width)));
            sb.AppendLine();
            sb.AppendLine(_labelStyle.Apply(Clip(
                TableRow(" ", "kind", "button", "col", "row", "modifiers", "wheel"), _width)));

            AppendHistory(sb);

            sb.AppendLine();
            sb.AppendLine(_dimStyle.Apply(Clip(
                "Row counts from the top of the window, not the console's screen buffer.", _width)));
            sb.AppendLine(_dimStyle.Apply(Clip(
                "Wheel is its own Kind, never a button: as a press it would empty a magazine.", _width)));
            sb.AppendLine(_dimStyle.Apply(Clip(
                "A sideways flick is refused outright - one delta field cannot mean two axes.", _width)));

            // Counted out of the builder immediately before the pad is drawn into it, so the row the marks are
            // placed at and the row they are drawn on are the same number by construction.
            _padTop = RowsAboveForm() + CountLineBreaks(sb);
            sb.Append(_pad.Render(PadContent()));

            _rendered = sb.ToString();
        }

        /// <summary>
        ///     How many window rows the scene graph writes above this form's own first line.
        ///     <para>
        ///         <b>A form's line zero is not the window's row zero</b>, and a screen whose marks are the whole
        ///         point cannot be a row out about it. Every frame opens with the spinner, the focused window's name
        ///         and whatever <c>SimulationApp.OnPreRender</c> adds, and the window's text is appended to that same
        ///         line. The spinner and the names cannot carry a line break, so the offset is however many that one
        ///         method wrote: one here, because this app's <c>OnPreRender</c> ends its line, and zero for an app
        ///         whose version does not.
        ///     </para>
        ///     <para>
        ///         Asked rather than written down as a one, for the reason the pad's own top row is counted rather
        ///         than declared: the day that method grows a second line every mark lands a row above the pointer,
        ///         which looks exactly like the coordinate arithmetic being wrong and is nowhere near it.
        ///     </para>
        /// </summary>
        /// <returns>The number of rows between the top of the window and this form's first line.</returns>
        private int RowsAboveForm()
        {
            return CountLineBreaks(SimUnit?.OnPreRender());
        }

        /// <summary>How many line breaks are already in the builder, which is the row the next line will occupy.</summary>
        /// <param name="builder">The frame being composed.</param>
        /// <returns>The count.</returns>
        private static int CountLineBreaks(StringBuilder builder)
        {
            var breaks = 0;
            for (var i = 0; i < builder.Length; i++)
            {
                if (builder[i] == '\n')
                    breaks++;
            }

            return breaks;
        }

        /// <summary>How many line breaks a piece of text contains. Null counts as none.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The count.</returns>
        private static int CountLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var breaks = 0;
            foreach (var glyph in text)
            {
                if (glyph == '\n')
                    breaks++;
            }

            return breaks;
        }

        /// <summary>
        ///     Whether the mouse is on at all, and how much of the flood has arrived so far.
        ///     <para>
        ///         The no-mouse case is said out loud rather than left to be inferred from an empty table, because
        ///         "this terminal has no mouse" and "this screen is broken" look identical from the other side of the
        ///         glass. <c>AnsiConsole.EnableMouse</c> is Windows-only today and answers false having written
        ///         nothing at all anywhere else.
        ///     </para>
        /// </summary>
        /// <returns>The status line, styled.</returns>
        private string StatusLine()
        {
            if (!AnsiConsole.MouseEnabled)
            {
                return _alarmStyle.Apply(Clip(
                    "MOUSE OFF - AnsiConsole.EnableMouse() answered false on this terminal.", _width));
            }

            // The moves counter is the honest measure of what motion reporting costs: one event for every cell the
            // pointer crosses, whether anything is being dragged or not. Sweep the pointer once and watch it jump by
            // a hundred, which is the argument for it being opt-in stated as a number rather than as a claim.
            var line = string.Format(CultureInfo.InvariantCulture,
                "mouse on | motion {0} | {1} moves | {2} press | {3} release | {4} wheel ({5:+0;-0;0} notches)",
                SimUnit.InputManager.ReportsMouseMotion ? "on" : "off",
                _moves, _presses, _releases, _wheels, _notches);

            return _dimStyle.Apply(Clip(line, _width));
        }

        /// <summary>
        ///     What the newest event was: its kind, its button and the modifiers held with it.
        ///     <para>
        ///         Every member of <see cref="MouseEvent" /> is named rather than abbreviated, because this screen is
        ///         read by somebody learning which properties exist and a column headed "btn" teaches nothing. The
        ///         Kind takes the same colour its mark has down in the pad, so the two read as one event.
        ///     </para>
        ///     <para>
        ///         <b>Two lines rather than one, and that is arithmetic rather than taste.</b> A wheel notch turned
        ///         with all three modifiers held at a three-digit column runs to ninety columns with the members
        ///         spelled out, so on an eighty-column console the one field a reader came to see would be the one
        ///         cut off the end. Splitting it puts the worst case at fifty-six.
        ///     </para>
        /// </summary>
        /// <returns>The line, styled.</returns>
        private string WhatHappened()
        {
            if (_events.Count == 0)
                return _dimStyle.Apply(Clip("Kind=  Button=  Modifiers=", _width));

            var mouse = _events[_events.Count - 1];
            var row = new TextRow();

            AppendField(row, "Kind", mouse.Kind.ToString(), StyleFor(mouse));
            AppendField(row, "Button", mouse.Button.ToString(), default);
            AppendField(row, "Modifiers", Modifiers(mouse), default);

            return Clip(row.Render(), _width);
        }

        /// <summary>
        ///     Where the newest event landed and how far the wheel turned, which are the two questions with a number
        ///     for an answer. The notch count carries its unit here because it is the one figure on the screen whose
        ///     unit is not obvious, and because it is not the platform's: Windows counts in 120ths of a notch.
        /// </summary>
        /// <returns>The line, styled.</returns>
        private string WhereItLanded()
        {
            if (_events.Count == 0)
                return _dimStyle.Apply(Clip("Column=  Row=  WheelDelta=", _width));

            var mouse = _events[_events.Count - 1];
            var row = new TextRow();

            AppendField(row, "Column", mouse.Column.ToString(CultureInfo.InvariantCulture), default);
            AppendField(row, "Row", mouse.Row.ToString(CultureInfo.InvariantCulture), default);
            AppendField(row, "WheelDelta", Notches(mouse), mouse.WheelDelta == 0 ? default : _wheelStyle);

            return Clip(row.Render(), _width);
        }

        /// <summary>Appends one <c>Name=value</c> field, with the member name quieter than what it is holding.</summary>
        /// <param name="row">The row being built.</param>
        /// <param name="label">The member's name, spelled exactly as the property is.</param>
        /// <param name="value">What it holds.</param>
        /// <param name="style">How to draw the value.</param>
        private static void AppendField(TextRow row, string label, string value, TextStyle style)
        {
            if (row.Width > 0)
                row.Append("  ");

            row.Append(label + "=", _dimStyle);
            row.Append(value, style);
        }

        /// <summary>
        ///     What is worth saying about the newest event, said as it arrives rather than in a legend somebody reads
        ///     once. Every one of these is a thing the type's own documentation exists to record.
        /// </summary>
        /// <returns>One line of prose about the newest event.</returns>
        private string Reading()
        {
            // The event decides what this says, and the mouse being off is only an explanation for there being
            // none. Written the other way round it would go on insisting nothing can arrive while the table above
            // filled up, which is what a host feeding events in through InputManager.SendMouseEvent really does.
            if (_events.Count == 0)
            {
                return AnsiConsole.MouseEnabled
                    ? "no events yet. Move the pointer over this window, click, drag, scroll."
                    : "nothing can arrive: this terminal reports no mouse, and only Windows does.";
            }

            var mouse = _events[_events.Count - 1];
            switch (mouse.Kind)
            {
                case MouseEventKindEnum.Press:
                    return "press: it arrived at OnMousePressed, and never at OnMouseEvent.";
                case MouseEventKindEnum.Release:
                    return "release: the next move already carries what is still held, so this is spare.";
                case MouseEventKindEnum.Wheel:
                    return "wheel: its own Kind, never a button, and no release ever follows one.";
                default:
                    return mouse.Button == MouseButtonEnum.None
                        ? "hover: a move carrying None. One of these per cell the pointer crosses."
                        : "drag: a move still carrying " + mouse.Button + ". Same Kind as a hover.";
            }
        }

        /// <summary>
        ///     The last few events in the table's shorthand, newest first, with the newest one marked.
        ///     <para>
        ///         <b>Nothing is folded, and that is what makes the flood a measurement rather than a claim.</b> A
        ///         run of thirty moves really is thirty rows and really does push a press off the top in a fifth of
        ///         a second. Events only arrive while the pointer is moving, though, so letting go of the mouse
        ///         leaves the sequence standing still to be read: a release, a stretch of moves each carrying the
        ///         same button, and the press that started them, which is a drag written down.
        ///     </para>
        /// </summary>
        /// <param name="builder">The frame being composed.</param>
        private void AppendHistory(StringBuilder builder)
        {
            for (var drawn = 0; drawn < _historyRows; drawn++)
            {
                var index = _events.Count - 1 - drawn;
                if (index < 0)
                {
                    builder.AppendLine();
                    continue;
                }

                var mouse = _events[index];
                var line = TableRow(
                    drawn == 0 ? ">" : " ",
                    mouse.Kind.ToString(),
                    mouse.Button.ToString(),
                    mouse.Column.ToString(CultureInfo.InvariantCulture),
                    mouse.Row.ToString(CultureInfo.InvariantCulture),
                    Modifiers(mouse),
                    Notches(mouse));

                builder.AppendLine(StyleFor(mouse).Apply(Clip(line, _width)));
            }
        }

        /// <summary>
        ///     One row of the history table, and the header that names its columns, laid out by the same method so
        ///     the two cannot drift apart the day a column is widened.
        /// </summary>
        /// <param name="marker">What sits in the left margin; the newest row is the one that gets a mark.</param>
        /// <param name="kind">The kind column.</param>
        /// <param name="button">The button column.</param>
        /// <param name="column">The column column.</param>
        /// <param name="row">The row column.</param>
        /// <param name="modifiers">The modifiers column.</param>
        /// <param name="wheel">The wheel column.</param>
        /// <returns>The plain, unstyled row.</returns>
        private static string TableRow(string marker, string kind, string button, string column, string row,
            string modifiers, string wheel)
        {
            // Eighteen columns for the modifiers because "Alt+Shift+Control" is seventeen: a column sized for the
            // usual answer is one that jolts sideways on the rare row, which is exactly the row being looked at.
            return marker + " " + kind.PadRight(8) + " " + button.PadRight(7) + " " +
                   column.PadLeft(4) + " " + row.PadLeft(4) + "  " + modifiers.PadRight(18) + " " + wheel;
        }

        /// <summary>
        ///     Which of shift, alt and control were held, drawn as one token. The BCL's own
        ///     <see cref="ConsoleModifiers" /> rather than a type of the library's own, so a shifted click compares
        ///     against a shifted key press without a translation in between.
        /// </summary>
        /// <param name="mouse">The event.</param>
        /// <returns>The modifiers, or a dash for none.</returns>
        private static string Modifiers(MouseEvent mouse)
        {
            return mouse.Modifiers == 0
                ? "-"
                : mouse.Modifiers.ToString().Replace(", ", "+", StringComparison.Ordinal);
        }

        /// <summary>
        ///     How far the wheel turned, in notches, which is the unit the library reports and the unit a hand turns
        ///     in. Anything that is not a wheel event carries zero here, by contract rather than by accident.
        /// </summary>
        /// <param name="mouse">The event.</param>
        /// <returns>The signed notch count, with its unit while there is one.</returns>
        private static string Notches(MouseEvent mouse)
        {
            return mouse.WheelDelta == 0
                ? "0"
                : mouse.WheelDelta.ToString("+0;-0;0", CultureInfo.InvariantCulture) + " notches";
        }

        /// <summary>
        ///     The remembered events plotted where they happened, one cell each, newest drawn last so it wins the
        ///     cell it shares with an older mark.
        ///     <para>
        ///         <b>This is the pointer the terminal stops drawing.</b> Once reporting is on there is no cursor
        ///         following the mouse, which is a fact worth meeting rather than reading: the newest mark is where
        ///         the pointer is, and the marks behind it are the cells it crossed on the way, one event apiece.
        ///     </para>
        ///     <para>
        ///         The arithmetic is the whole demonstration of <see cref="MouseEvent.Row" /> being window-relative.
        ///         The pad's top border is at window row <c>_padTop</c> (this form's own rows plus the ones the scene
        ///         graph writes above it, see <see cref="RowsAboveForm" />) and its left border is at window column
        ///         zero, so an event's own row and column index straight into the interior with one row and one
        ///         column of border taken off. Nothing is scaled and nothing is guessed, which is why the mark
        ///         appears under the pointer rather than near it.
        ///     </para>
        /// </summary>
        /// <returns>The pad's interior, one line per row, each exactly the interior width.</returns>
        private string PadContent()
        {
            var lines = new string[_padRows];
            var glyphs = new char[_padWidth];
            var styles = new TextStyle[_padWidth];

            for (var r = 0; r < _padRows; r++)
            {
                Array.Fill(glyphs, ' ');
                Array.Fill(styles, default);

                for (var i = 0; i < _events.Count; i++)
                {
                    var mouse = _events[i];

                    // One row and one column of border, and nothing else. A pointer over the border itself, or
                    // anywhere above the pad, simply has no cell here to be drawn in.
                    var cellRow = mouse.Row - _padTop - 1;
                    var cellColumn = mouse.Column - 1;

                    if (cellRow != r || cellColumn < 0 || cellColumn >= _padWidth)
                        continue;

                    glyphs[cellColumn] = GlyphFor(mouse);

                    // The newest mark is where the pointer is now, so it is the one drawn bold. Older marks keep
                    // their kind's colour, which is what makes the stretch of a drag visibly different from the
                    // hover either side of it.
                    styles[cellColumn] = StyleFor(mouse).WithBold(i == _events.Count - 1);
                }

                lines[r] = RenderPadRow(glyphs, styles);
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        ///     Draws one row of the pad, breaking a run only where the style changes.
        ///     <para>
        ///         A run per cell would be correct and would also emit an open and a reset around every space on the
        ///         row, which on a screen redrawn on every event is most of the bytes. <see cref="TextRow" /> breaks
        ///         its runs on the resolved escape rather than on the <see cref="TextStyle" />, so the blanks between
        ///         two marks collapse into one run and a row nobody coloured emits no escape at all.
        ///     </para>
        /// </summary>
        /// <param name="glyphs">What is in each cell.</param>
        /// <param name="styles">How each cell is drawn.</param>
        /// <returns>The row, styled, exactly the interior width.</returns>
        private string RenderPadRow(char[] glyphs, TextStyle[] styles)
        {
            var row = new TextRow();
            var start = 0;

            for (var c = 1; c <= _padWidth; c++)
            {
                if (c < _padWidth && styles[c] == styles[start])
                    continue;

                row.Append(new string(glyphs, start, c - start), styles[start]);
                start = c;
            }

            return row.Render();
        }

        /// <summary>
        ///     Which glyph stands for an event in the pad. The wheel gets a direction rather than a mark, since which
        ///     way it turned is the only thing a notch has to say.
        /// </summary>
        /// <param name="mouse">The event.</param>
        /// <returns>The glyph.</returns>
        private static char GlyphFor(MouseEvent mouse)
        {
            switch (mouse.Kind)
            {
                case MouseEventKindEnum.Press:
                    return PressGlyph;
                case MouseEventKindEnum.Release:
                    return ReleaseGlyph;
                case MouseEventKindEnum.Wheel:
                    return mouse.WheelDelta >= 0 ? WheelUpGlyph : WheelDownGlyph;
                default:

                    // The one test that separates a drag from a hover, and the only one there is: a move carries
                    // whichever buttons are still held, so the button on a move is what a fourth kind would have
                    // been for.
                    return mouse.Button == MouseButtonEnum.None ? HoverGlyph : DragGlyph;
            }
        }

        /// <summary>How an event is coloured, in the readout, in the table and in the pad alike.</summary>
        /// <param name="mouse">The event.</param>
        /// <returns>The style.</returns>
        private static TextStyle StyleFor(MouseEvent mouse)
        {
            switch (mouse.Kind)
            {
                case MouseEventKindEnum.Press:
                    return _pressStyle;
                case MouseEventKindEnum.Release:
                    return _releaseStyle;
                case MouseEventKindEnum.Wheel:
                    return _wheelStyle;
                default:
                    return mouse.Button == MouseButtonEnum.None ? _hoverStyle : _dragStyle;
            }
        }

        /// <summary>
        ///     Trims a line to the console, and leaves it alone when it already fits.
        ///     <para>
        ///         <see cref="AnsiText.Fit" /> would also pad, which is right for a cell in a table and wrong for a
        ///         sentence: every line would end in a run of spaces nobody asked for. Trimming is the half that is
        ///         needed, because <c>ConsolePresenter</c> turns auto-wrap off for the frame, so an over-wide row is
        ///         truncated by the terminal rather than reflowed onto the row beneath.
        ///     </para>
        /// </summary>
        /// <param name="text">The line.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The line, no wider than the console.</returns>
        private static string Clip(string text, int width)
        {
            return AnsiText.VisibleLength(text) <= width ? text : AnsiText.Fit(text, width);
        }
    }
}
