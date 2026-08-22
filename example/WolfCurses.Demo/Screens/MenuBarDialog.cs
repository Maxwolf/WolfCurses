// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     The <see cref="MenuBar" />: the pull-down bar every text-mode application had, with a small styled document
    ///     underneath that the menus actually change, so a tick or a greyed entry means something rather than
    ///     demonstrating that the control renders.
    ///     <para>
    ///         <b>The reason this control exists is that it keeps its layout.</b> Its geometry is worked out once by
    ///         its own <c>Layout()</c> and read by both the drawing and the hit test, so the menu on screen and the
    ///         menu a click lands in cannot be different menus. The library's numbered menus deliberately cannot
    ///         promise that: <c>MenuLayout</c> may reflow items into columns it does not retain, which is exactly why
    ///         clicking one was never supported. Every hit test here goes through the same
    ///         <see cref="MenuBar.TitleAt" /> and <see cref="MenuBar.EntryAt" /> the panel was drawn from, and the
    ///         bottom line of the screen reports what they answered so it can be checked against what is drawn.
    ///     </para>
    ///     <para>
    ///         <b>F10 opens it and always works. ALT plus a letter also opens it and often does not</b>, because
    ///         terminals swallow ALT, send an escape prefix instead, or hand it to the window manager. Both are
    ///         advertised on this screen in that order for that reason, and a reader who finds ALT+V doing nothing has
    ///         learned the thing rather than hit a bug.
    ///     </para>
    ///     <para>
    ///         <b>ENTER is the trap.</b> <see cref="MenuBar.HandleKey" /> has an ENTER case that is unreachable
    ///         unless the screen asks for ENTER, since <c>InputManager</c> spends it closing the input buffer and it
    ///         never becomes a key press at all. The planner app shipped with exactly that bug: a bar that opened,
    ///         walked with the arrows and chose nothing. Of the two ways out this screen takes the second, calling
    ///         <see cref="MenuBar.Activate" /> from <see cref="OnInputBufferReturned" />, which is where ENTER really
    ///         arrives. See that method for why it is preferred here over <c>IForm.EditsText</c>.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class MenuBarDialog : Form<DemoWindowInfo>
    {
        /// <summary>
        ///     Which screen row the bar is drawn on: this form's second line.
        ///     <para>
        ///         <b>This is the whole of the coordinate problem, and it is why <see cref="MenuBar.BarRow" /> is told
        ///         rather than inferred.</b> A pointer reports the row of the window, but a form composes rows of its
        ///         own text, and the two only agree once whatever the host drew above is counted. Get it wrong by one
        ///         and every press lands on the entry above the pointer, which reads as a menu that chooses the wrong
        ///         thing rather than as arithmetic that is off.
        ///     </para>
        ///     <para>
        ///         The offset was a <c>const 1</c> here and five other things in five other screens before
        ///         <see cref="FrameLayout" /> existed; it is asked for now rather than written down, which is also
        ///         what lets it be right for a host whose <c>OnPreRender</c> does not end its line.
        ///     </para>
        /// </summary>
        private int BarRow => FrameLayout.RowsAboveForm(SimUnit) + 1;

        /// <summary>
        ///     Which screen row the body starts on, and therefore where an open panel's own top border goes.
        ///     <para>
        ///         <b>There is a row between the bar and the body on purpose</b>, so that this screen has to say where
        ///         the panel hangs instead of taking the default. <see cref="MenuBar.PanelRow" /> left alone means
        ///         "directly under the bar", which is right only while nothing sits between them. A screen with a
        ///         legend, a frame edge or a tab strip in the gap and no <c>PanelRow</c> set draws its panel in one
        ///         place and hit-tests it in another, off by exactly the chrome it forgot.
        ///     </para>
        /// </summary>
        private int BodyRow => BarRow + 2;

        /// <summary>
        ///     How many rows the body occupies, whatever is in it. Fixed rather than measured for the reason
        ///     <c>MonthGrid</c> always draws six weeks: a body that changed height as the ruler was switched on would
        ///     move the two status lines under it every time. It is also what guarantees the tallest panel has rows to
        ///     be drawn over.
        /// </summary>
        private const int BodyRows = 13;

        /// <summary>The narrowest console this screen lays itself out for.</summary>
        private const int MinScreenWidth = 46;

        /// <summary>
        ///     Narrowest and widest the document may be drawn, and the step the two size entries move it by. The floor
        ///     leaves room for the longest line plus the five columns a line number and its rule take, so switching
        ///     the numbers on at the narrowest setting does not quietly cut the text off.
        /// </summary>
        private const int MinDocWidth = 40;

        private const int MaxDocWidth = 60;
        private const int DocStep = 4;

        private const int PalettePlain = 0;
        private const int PaletteSilver = 1;
        private const int PaletteAmber = 2;
        private const int PaletteOcean = 3;

        /// <summary>What the settings are when the screen opens, which is also what File / Reset puts back.</summary>
        private const int DefaultPalette = PaletteSilver;

        private const BoxBorderEnum DefaultBorder = BoxBorderEnum.Single;
        private const int DefaultDocWidth = 48;

        /// <summary>The tick drawn beside a checked entry, and the ASCII stand-in the Options menu offers instead.</summary>
        private const char UnicodeCheck = '✓';

        private const char AsciiCheck = 'x';

        /// <summary>The screen's own heading. Bold plus a mid cyan, both of which survive either terminal polarity.</summary>
        private static readonly TextStyle _headingStyle =
            new TextStyle(new TextColor(ConsoleColor.DarkCyan), null, true);

        /// <summary>The legend and the two readouts, which should read as quieter than the thing they describe.</summary>
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        /// <summary>
        ///     The document, as fixed short lines rather than wrapped prose. Wrapping to the live width would make the
        ///     body's height a function of the width, and the point of the two size entries is to watch a pair of menu
        ///     entries switch themselves off at the ends of a range, not to exercise a word wrapper.
        /// </summary>
        private static readonly string[] _document =
        {
            "The bar keeps its layout.",
            "",
            "Layout() runs once and is read by",
            "both the drawing and the hit test,",
            "so the menu you see and the menu a",
            "click lands in cannot be two.",
            "",
            "A numbered menu cannot say that:",
            "it reflows into columns that",
            "nothing afterwards remembers."
        };

        /// <summary>The frame round the document. Reconfigured per compose, since the menus change how it is drawn.</summary>
        private readonly Box _box = new Box {Title = "keeps-its-layout.txt", Padding = 0};

        private MenuBar _bar;

        private BoxBorderEnum _border = DefaultBorder;
        private int _palette = DefaultPalette;
        private int _docWidth = DefaultDocWidth;
        private bool _lineNumbers;
        private bool _ruler;

        /// <summary>Whether an entry that cannot be chosen is drawn greyed, which is the Options menu's own demonstration.</summary>
        private bool _grey = true;

        private bool _asciiCheck;

        private TextStyle _docStyle;
        private TextStyle _frameStyle;
        private TextStyle _titleStyle;

        /// <summary>How wide the screen is, read once a second rather than once a frame.</summary>
        private int _width = 80;

        /// <summary>The whole frame, built whenever something changed and handed back unchanged by every render between.</summary>
        private string _rendered = string.Empty;

        /// <summary>What the hit test said about the last press, so the drawn menu and the tested menu can be compared.</summary>
        private string _lastPress;

        /// <summary>How many motion events arrived, against how many of them actually moved anything.</summary>
        private int _moves;

        private int _redraws;

        /// <summary>Initializes a new instance of the <see cref="MenuBarDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public MenuBarDialog(IWindow window) : base(window)
        {
        }

        /// <summary>Whether anything has been changed from where the screen opened, which is what File / Reset asks.</summary>
        private bool IsChanged =>
            _border != DefaultBorder || _palette != DefaultPalette || _docWidth != DefaultDocWidth ||
            _lineNumbers || _ruler || !_grey || _asciiCheck || !_bar.ShowAccessKeys;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "F10 opens the menus, ENTER returns to the demo menu";

            // Asked for by the screen that wants it and handed back in OnFormClosing. The hover below is the only
            // reason this screen wants motion at all, and motion is one event for every cell the pointer crosses, so
            // every other demo would otherwise pay for a flood none of them read.
            SimUnit.InputManager.ReportsMouseMotion = true;

            _width = ScreenWidth();

            BuildMenus();
            ApplyLook();
            Compose();
        }

        /// <summary>
        ///     Hands pointer reporting back. Being dropped is no signal at all, which is why <c>IForm.OnFormClosing</c>
        ///     exists, and the library fires it from every path a form is detached by, so ESC, ENTER, the window being
        ///     removed and quitting are all covered by this one override.
        /// </summary>
        public override void OnFormClosing()
        {
            base.OnFormClosing();

            SimUnit.InputManager.ReportsMouseMotion = false;
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // The console size is a syscall, so it is asked once a second rather than a thousand times a second, and
            // nothing else here changes on its own: this screen recomposes when a key or the pointer changed
            // something, and not otherwise.
            if (systemTick)
                return;

            var width = ScreenWidth();
            if (width == _width)
                return;

            _width = width;
            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // A pure read of what the last change built. The scene graph calls this on every system tick, about a
            // thousand times a second, and this screen changes only when somebody presses something.
            return _rendered;
        }

        /// <summary>
        ///     Offers every key to the bar first and handles whatever it did not want.
        ///     <para>
        ///         <b>The false return is the half that matters.</b> While a menu is open the bar swallows everything,
        ///         so none of the function keys below can fire at a document the user cannot even see; while it is
        ///         shut it takes only F10 and ALT plus a letter, so this screen's own shortcuts keep working. A screen
        ///         that ignored the answer and handled keys anyway would act twice on some of them and leak the rest
        ///         underneath an open panel.
        ///     </para>
        ///     <para>
        ///         <b>ESC never reaches the bar here</b>, and that is this app rather than the control:
        ///         <c>DemoWindow.OnKeyPressed</c> spends ESC backing out to the demo menu before the key is forwarded
        ///         to any form. <see cref="MenuBar.HandleKey" /> does answer ESC by shutting the open menu and
        ///         reporting the key as consumed, which is precisely the hook an application treating ESC as "leave"
        ///         needs so that dismissing a menu does not also drop the user out of the program. The office suite
        ///         uses it that way; here F10 shuts the menu instead.
        ///     </para>
        /// </summary>
        /// <param name="keyInfo">The key press exactly as the host saw it.</param>
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            if (_bar.HandleKey(keyInfo))
            {
                // The bar spent the key, so the echoed prompt must not also show it. A printable key lands in the
                // input buffer as well as arriving here, which is the two input paths working as intended, and a
                // letter typed at an open menu would otherwise pile up in the prompt for nobody.
                SimUnit.InputManager.ClearBuffer();
                Compose();
                return;
            }

            // Function keys rather than letters, because a letter is printable and would land in the input buffer as
            // well; these are the shortcuts the menus advertise, so the two have to do the same thing.
            switch (keyInfo.Key)
            {
                case ConsoleKey.F2:
                    _lineNumbers = !_lineNumbers;
                    break;
                case ConsoleKey.F3:
                    _ruler = !_ruler;
                    break;
                case ConsoleKey.F4:
                    Widen(1);
                    break;
                case ConsoleKey.F5:
                    Widen(-1);
                    break;
                case ConsoleKey.F8:
                    Reset();
                    break;
                default:
                    return;
            }

            Compose();
        }

        /// <summary>
        ///     Where a press lands, and the only place it can. <c>Window.OnMouseEvent</c> hands a press to
        ///     <c>OnMousePressed</c> and returns, so a press never reaches a form's <see cref="OnMouseEvent" /> at
        ///     all: a click handler written there compiles, runs never, and leaves a bar that cannot be clicked.
        ///     <para>
        ///         The hit test is asked <b>before</b> the press is handed over, because handling it opens or shuts a
        ///         menu and the answer would then describe a layout that no longer exists.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            _lastPress = Describe(mouse);

            // HandleMouse reports what it spent for the same reason HandleKey does. A press that merely dismisses an
            // open menu is consumed, so it does not also act on whatever was underneath the panel; a press the bar
            // did not want is left for the screen, which here has nothing clickable of its own.
            _bar.HandleMouse(mouse.Row, mouse.Column);

            Compose();
        }

        /// <summary>
        ///     Everything the pointer does except press. A press cannot arrive here (see
        ///     <see cref="OnMousePressed" />), and this screen has nothing with a duration to track, so a release and
        ///     a wheel notch are dropped and the hover is the whole of it.
        ///     <para>
        ///         <b><see cref="MenuBar.HandleMouseMove" /> does nothing at all while the bar is shut, deliberately.</b>
        ///         A menu that drops open because the pointer crossed it on the way somewhere else is a menu in the
        ///         way, and opening one costs a single press. Once one is open, sliding along the bar opens each in
        ///         turn, which is how somebody looks through them.
        ///     </para>
        ///     <para>
        ///         <b>It answers whether anything moved, not whether the pointer was over the menu</b>, which is what
        ///         lets this method skip a recompose for the pointer crossing a cell it was already in. On a screen
        ///         reporting one event per cell that is most of them, and the two counters on the bottom line are here
        ///         to show the gap: wiggle inside one entry and the moves climb while the redraws do not.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What the mouse did, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (mouse.Kind != MouseEventKindEnum.Move)
                return;

            _moves++;

            if (!_bar.HandleMouseMove(mouse.Row, mouse.Column))
                return;

            _redraws++;
            Compose();
        }

        /// <summary>
        ///     Where ENTER really arrives, and therefore where the menu bar's own ENTER handling has to be reached
        ///     from.
        ///     <para>
        ///         <b>This is the bug the planner shipped with.</b> <c>InputManager</c> consumes ENTER as buffer
        ///         control before any key press is queued, so <c>case ConsoleKey.Enter:</c> inside
        ///         <see cref="MenuBar.HandleKey" /> is dead code for every screen that has not asked for it. A reader
        ///         who never wires this up gets a bar that opens with F10, walks with the arrows and chooses nothing
        ///         at the end of it, with an entry that has no shortcut of its own reachable only by pointer.
        ///     </para>
        ///     <para>
        ///         The other way out is <c>IForm.EditsText</c>, which sends ENTER and BACKSPACE to
        ///         <see cref="OnKeyPressed" /> as ordinary key presses and never calls this method at all. That is the
        ///         right answer for an editor and the wrong one here: it also hands over BACKSPACE, it wants
        ///         <c>InputFillsBuffer</c> switched off so the prompt does not collect text nothing reads, and it takes
        ///         ENTER away from leaving the screen, which is how every other demo in this app is left. So this
        ///         screen takes ENTER as a submitted line and spends it on the open menu when there is one.
        ///     </para>
        /// </summary>
        /// <param name="input">Whatever was in the buffer, which this screen does not read.</param>
        public override void OnInputBufferReturned(string input)
        {
            if (_bar.IsOpen)
            {
                _bar.Activate();
                Compose();
                return;
            }

            ClearForm();
        }

        /// <summary>
        ///     Builds the bar. Read this beside the class documentation: everything the control is for is a property
        ///     set in here.
        /// </summary>
        private void BuildMenus()
        {
            // A menu's access key is the first letter of its title and nothing else, so two menus starting with the
            // same letter quietly share one and nothing anywhere complains. File, View, Border, Palette, Options and
            // Help were picked to differ, which is the only defence there is.
            _bar = new MenuBar(
                // EnabledWhen is a predicate rather than a flag because IsEnabled is read at four separate moments:
                // drawing the panel, walking it with the arrows, hovering it and activating an entry. An owner
                // keeping a flag fresh has to refresh it before all four, and missing one draws a live entry as dead
                // or runs a dead one. Reset is the classic shape: it means nothing until something has changed.
                new MenuBarMenu("File",
                    new MenuBarEntry("Reset", Reset, "F8") {EnabledWhen = () => IsChanged},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Close", () => ClearForm(), "Esc")),

                // The other two switching entries, and the pair worth watching: walk the document out to sixty
                // columns and Wider greys itself out under the cursor, which the arrow keys then refuse to land on.
                new MenuBarMenu("View",
                    new MenuBarEntry("Line Numbers", ToggleNumbers, "F2") {CheckedWhen = () => _lineNumbers},
                    new MenuBarEntry("Ruler", ToggleRuler, "F3") {CheckedWhen = () => _ruler},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Wider", () => Widen(1), "F4") {EnabledWhen = () => _docWidth < MaxDocWidth},
                    new MenuBarEntry("Narrower", () => Widen(-1), "F5") {EnabledWhen = () => _docWidth > MinDocWidth}),

                // CheckedWhen is a predicate too, and there is deliberately no settable IsChecked to go with it: a
                // mark that never moves says nothing. These two menus are radio groups written as predicates, which
                // is all a radio group is.
                new MenuBarMenu("Border",
                    BorderEntry("Single", BoxBorderEnum.Single),
                    BorderEntry("Double", BoxBorderEnum.Double),
                    BorderEntry("Rounded", BoxBorderEnum.Rounded),
                    BorderEntry("ASCII", BoxBorderEnum.Ascii),
                    BorderEntry("None", BoxBorderEnum.None)),

                new MenuBarMenu("Palette",
                    PaletteEntry("Plain", PalettePlain),
                    PaletteEntry("Silver", PaletteSilver),
                    PaletteEntry("Amber", PaletteAmber),
                    PaletteEntry("Ocean", PaletteOcean)),

                // The bar's own settings, so what they change is on screen while the menu that changed them is open.
                new MenuBarMenu("Options",
                    new MenuBarEntry("Access Keys", ToggleAccessKeys) {CheckedWhen = () => _bar.ShowAccessKeys},
                    new MenuBarEntry("Grey Disabled Entries", ToggleGrey) {CheckedWhen = () => _grey},
                    new MenuBarEntry("ASCII Check Mark", ToggleCheckMark) {CheckedWhen = () => _asciiCheck}),

                // Help is traditionally right-aligned and is deliberately not, here. MenuBarMenu.AlignRight lays the
                // title from the right edge, but a panel is drawn and hit-tested from its own title's column, so a
                // Help panel wider than the word Help hangs off the screen. Pushing it back on is what the office
                // suite does, and it moves the drawing without moving EntryAt: the entries then answer to columns
                // they are no longer drawn in. Left aligned, this screen never has to choose.
                new MenuBarMenu("Help",
                    new MenuBarEntry("About the menu bar", ShowAbout),
                    new MenuBarEntry("Keys and the pointer", ShowKeys)));

            // Told, not inferred. See BarRow and BodyRow for what each of these two is counting.
            _bar.BarRow = BarRow;
            _bar.PanelRow = BodyRow;
            _bar.Width = _width;
        }

        /// <summary>One of the border radio entries.</summary>
        /// <param name="label">What the line reads.</param>
        /// <param name="border">The border it selects.</param>
        /// <returns>The entry.</returns>
        private MenuBarEntry BorderEntry(string label, BoxBorderEnum border)
        {
            return new MenuBarEntry(label, () => SetBorder(border)) {CheckedWhen = () => _border == border};
        }

        /// <summary>One of the palette radio entries.</summary>
        /// <param name="label">What the line reads.</param>
        /// <param name="palette">The palette it selects.</param>
        /// <returns>The entry.</returns>
        private MenuBarEntry PaletteEntry(string label, int palette)
        {
            return new MenuBarEntry(label, () => SetPalette(palette)) {CheckedWhen = () => _palette == palette};
        }

        /// <summary>
        ///     Paints the bar, the panel and the document from the chosen palette.
        ///     <para>
        ///         <b><see cref="MenuBar.DisabledStyle" /> is the drawing half of <c>EnabledWhen</c>, and shipping the
        ///         predicate without it was a real bug.</b> Switch it off from the Options menu under any palette but
        ///         Plain and the greyed entries here draw exactly like live ones: they still refuse the pointer, still
        ///         refuse the arrows and still do nothing when clicked, with nothing on screen saying why. That is
        ///         what a menu with four dead entries out of six looked like, and it read as broken rather than as
        ///         greyed. It is nullable where its sibling styles are not, so null means "fall back to the panel"
        ///         rather than "paint disabled entries with no style", which would punch a hole in a coloured panel.
        ///     </para>
        ///     <para>
        ///         Plain leaves every style empty, and the whole screen then emits no escape sequences at all. That is
        ///         the library's standing rule rather than anything arranged here, and it is why the greying toggle
        ///         has nothing to show under it.
        ///     </para>
        /// </summary>
        private void ApplyLook()
        {
            switch (_palette)
            {
                case PaletteSilver:
                    SetLook(
                        new TextStyle(ConsoleColor.Black, ConsoleColor.Gray),
                        new TextStyle(ConsoleColor.White, ConsoleColor.Black),
                        new TextStyle(ConsoleColor.DarkGray, ConsoleColor.Gray),
                        new TextStyle(ConsoleColor.Gray, ConsoleColor.DarkBlue),
                        new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue),
                        new TextStyle(ConsoleColor.DarkBlue, ConsoleColor.Gray));
                    break;
                case PaletteAmber:
                    SetLook(
                        new TextStyle(ConsoleColor.Black, ConsoleColor.DarkYellow),
                        new TextStyle(ConsoleColor.White, ConsoleColor.Black),
                        new TextStyle(ConsoleColor.DarkGray, ConsoleColor.DarkYellow),
                        new TextStyle(ConsoleColor.DarkYellow, ConsoleColor.Black),
                        new TextStyle(ConsoleColor.Yellow, ConsoleColor.Black),
                        new TextStyle(ConsoleColor.Black, ConsoleColor.DarkYellow));
                    break;
                case PaletteOcean:
                    SetLook(
                        new TextStyle(ConsoleColor.Black, ConsoleColor.Cyan),
                        new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue),
                        new TextStyle(ConsoleColor.DarkGray, ConsoleColor.Cyan),
                        new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue),
                        new TextStyle(ConsoleColor.Cyan, ConsoleColor.DarkBlue),
                        new TextStyle(ConsoleColor.DarkBlue, ConsoleColor.Cyan));
                    break;
                default:
                    SetLook(TextStyle.None, TextStyle.None, TextStyle.None, TextStyle.None, TextStyle.None,
                        TextStyle.None);
                    break;
            }
        }

        /// <summary>Assigns one palette's worth of styles to the bar and to the document.</summary>
        /// <param name="bar">The bar row, and the titles sitting on it.</param>
        /// <param name="highlight">The open title, and the entry under the cursor.</param>
        /// <param name="disabled">An entry that cannot be chosen just now.</param>
        /// <param name="document">The document's own text.</param>
        /// <param name="frame">The frame drawn round it.</param>
        /// <param name="title">The name notched into the frame's top edge.</param>
        private void SetLook(TextStyle bar, TextStyle highlight, TextStyle disabled, TextStyle document,
            TextStyle frame, TextStyle title)
        {
            _bar.BarStyle = bar;
            _bar.HighlightStyle = highlight;

            // A dropped panel is painted in the colours of the bar it came from, which is what every menu bar since
            // the first one has done, so one style does for both.
            _bar.PanelStyle = bar;
            _bar.PanelHighlightStyle = highlight;

            // Null rather than an empty style when the greying is switched off, because null means "fall back to the
            // panel" while TextStyle.None means "paint this with nothing at all", which would punch an unstyled hole
            // through a coloured panel instead of removing the greying.
            _bar.DisabledStyle = _grey ? (TextStyle?) disabled : null;
            _bar.CheckMark = _asciiCheck ? AsciiCheck : UnicodeCheck;

            _docStyle = document;
            _frameStyle = frame;
            _titleStyle = title;
        }

        /// <summary>Builds the whole frame, which is the only place anything here is composed.</summary>
        private void Compose()
        {
            var sb = new StringBuilder();

            // Kept under seventy columns on purpose. Auto-wrap is off for the frame, so an over-wide row is
            // truncated rather than reflowed, and a heading that loses its last four words says nothing about why.
            sb.Append(_headingStyle.Apply("Menu bar. F10 opens it; ALT plus an underlined letter may too."))
                .Append(Environment.NewLine);

            sb.Append(_bar.RenderTitleBar(_width)).Append(Environment.NewLine);
            sb.Append(_dimStyle.Apply(Legend())).Append(Environment.NewLine);

            var body = Body();

            // The panel is drawn OVER the body rather than pushed into it, which is what a dropped menu does; a menu
            // that displaced the document would be a screen being stacked rather than composited. Panel row i lands
            // on body row i precisely because PanelRow was set to the body's first row, so the drawing and the hit
            // test are reading the same number.
            var panel = _bar.IsOpen ? _bar.DropdownRows() : (IReadOnlyList<string>) Array.Empty<string>();
            var column = Math.Clamp(_bar.DropdownColumn, 0, Math.Max(0, _width - 1));

            // Truncated at the right edge rather than shoved back on screen. Moving a panel that does not fit is what
            // desynchronises the hit test, since EntryAt still measures from the title's own column; a panel cut off
            // at the edge is at least still where its entries think it is.
            var panelWidth = Math.Max(0, Math.Min(_bar.DropdownWidth, _width - column));

            for (var i = 0; i < body.Count; i++)
                sb.Append(Overlay(body[i], panel, i, column, panelWidth)).Append(Environment.NewLine);

            sb.Append(_dimStyle.Apply(_lastPress ?? "no press yet. Click a title, then click an entry."))
                .Append(Environment.NewLine);

            sb.Append(_dimStyle.Apply(
                "pointer: " + _moves.ToString(CultureInfo.InvariantCulture) + " moves, " +
                _redraws.ToString(CultureInfo.InvariantCulture) + " of them moved something"));

            _rendered = sb.ToString();
        }

        /// <summary>The document in its frame, padded out to a fixed number of full width rows.</summary>
        /// <returns>The body's rows, each exactly as wide as the screen.</returns>
        private List<string> Body()
        {
            var docWidth = DocWidth();
            var doc = new StringBuilder();

            if (_ruler)
                doc.Append(_docStyle.Apply(Ruler(docWidth))).Append(Environment.NewLine);

            for (var i = 0; i < _document.Length; i++)
            {
                var text = _lineNumbers
                    ? (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(2) + " | " + _document[i]
                    : _document[i];

                if (i > 0)
                    doc.Append(Environment.NewLine);

                // Fitted to an exact column count and then painted, never the other way round: a style has length but
                // no width, so padding a styled row spends the budget on escape bytes and leaves the background
                // stopping short of the frame.
                doc.Append(_docStyle.Apply(AnsiText.Fit(text, docWidth)));
            }

            _box.Border = _border;
            _box.BorderStyle = _frameStyle;
            _box.TitleStyle = _titleStyle;
            _box.MinimumWidth = docWidth;

            var rows = new List<string>(BodyRows);

            foreach (var row in _box.Render(doc.ToString()).Replace("\r\n", "\n").Split('\n'))
            {
                // Every body row is exactly the width of the screen, because a slice of a short row hands back a short
                // row: the panel would then land at whatever column that row happened to end at rather than at the one
                // it was drawn from.
                rows.Add(AnsiText.Fit(row, _width));
            }

            while (rows.Count < BodyRows)
                rows.Add(new string(' ', _width));

            if (rows.Count > BodyRows)
                rows.RemoveRange(BodyRows, rows.Count - BodyRows);

            return rows;
        }

        /// <summary>Draws one body row with the open panel over the top of it, where the panel reaches that row.</summary>
        /// <param name="row">The finished row, already the width of the screen.</param>
        /// <param name="panel">The panel's rows.</param>
        /// <param name="index">Which of the panel's rows belongs on this one.</param>
        /// <param name="column">Which screen column the panel starts at.</param>
        /// <param name="width">How many columns of the panel there is room for.</param>
        /// <returns>The row to draw.</returns>
        private string Overlay(string row, IReadOnlyList<string> panel, int index, int column, int width)
        {
            if (index < 0 || index >= panel.Count || width <= 0)
                return row;

            // AnsiText.Slice rather than Substring, and this is the trap the type exists to remove: a styled row is
            // several hundred characters long for eighty columns of width, so cutting it by character index lands
            // inside an escape sequence and spills the remainder into the terminal as text.
            return AnsiText.Slice(row, 0, column) +
                   AnsiText.Slice(panel[index], 0, width) +
                   AnsiText.Slice(row, column + width, _width - column - width);
        }

        /// <summary>
        ///     How wide the document is really drawn: the chosen width, clamped to what this console has room for.
        ///     The frame and its two borders have to fit, so the chosen width is a request rather than a promise, and
        ///     the legend reads this rather than the request so it cannot say one thing while the screen shows another.
        /// </summary>
        /// <returns>The document's width in columns.</returns>
        private int DocWidth()
        {
            return Math.Clamp(_docWidth, MinDocWidth, Math.Max(MinDocWidth, _width - 4));
        }

        /// <summary>What the menus have done to the document, said out loud so a tick has something to point at.</summary>
        /// <returns>The legend row.</returns>
        private string Legend()
        {
            return "border " + _border +
                   " | palette " + PaletteName() +
                   " | " + DocWidth().ToString(CultureInfo.InvariantCulture) + " cols" +
                   " | numbers " + (_lineNumbers ? "on" : "off") +
                   " | ruler " + (_ruler ? "on" : "off");
        }

        /// <summary>The chosen palette's name.</summary>
        /// <returns>What to call it in the legend.</returns>
        private string PaletteName()
        {
            return _palette switch
            {
                PaletteSilver => "Silver",
                PaletteAmber => "Amber",
                PaletteOcean => "Ocean",

                // Named for what it demonstrates: with every style left empty this screen emits no escape sequence
                // anywhere, which is the library's standing rule rather than anything arranged here.
                _ => "Plain (no escapes)"
            };
        }

        /// <summary>
        ///     Asks the bar what is at a cell, using the same two hit tests the panel was drawn from. The point of
        ///     putting the answer on screen is that it can be checked against what is drawn there.
        /// </summary>
        /// <param name="mouse">The press to describe.</param>
        /// <returns>What the bar says is under it.</returns>
        private string Describe(MouseEvent mouse)
        {
            var where = "press r" + mouse.Row.ToString(CultureInfo.InvariantCulture) +
                        " c" + mouse.Column.ToString(CultureInfo.InvariantCulture) + ": ";

            if (mouse.Row == BarRow)
            {
                var title = _bar.TitleAt(mouse.Column);
                return where + (title >= 0
                    ? "the " + _bar.Menus[title].Title + " title"
                    : "the bar, past the last title");
            }

            if (!_bar.IsOpen)
                return where + "the document, nothing clickable here";

            var entry = _bar.EntryAt(mouse.Row, mouse.Column);

            // EntryAt answers -1 for the panel's border, for a separator, for a disabled entry AND for everywhere
            // outside the panel, so the panel's own rectangle is asked about separately to tell the two apart. The
            // first three are one rule with the arrow keys, which refuse exactly the same rows, so the greying and
            // the behaviour cannot drift apart; the fourth is a press that simply shuts the menu.
            if (entry >= 0)
                return where + _bar.Menus[_bar.OpenIndex].Title + " / " +
                       _bar.Menus[_bar.OpenIndex].Entries[entry].Label;

            var inPanel = mouse.Row >= BodyRow && mouse.Row < BodyRow + _bar.DropdownHeight &&
                          mouse.Column >= _bar.DropdownColumn &&
                          mouse.Column < _bar.DropdownColumn + _bar.DropdownWidth;

            return where + (inPanel
                ? "no choice there (a border, a separator or a greyed entry)"
                : "outside the panel, so the menu shuts");
        }

        /// <summary>A column ruler, which is the one thing on this screen whose width visibly follows the document.</summary>
        /// <param name="width">How many columns to draw.</param>
        /// <returns>The ruler row, exactly that wide.</returns>
        private static string Ruler(int width)
        {
            var sb = new StringBuilder(width);

            for (var i = 1; i <= width; i++)
            {
                if (i % 10 == 0)
                    sb.Append((char) ('0' + i / 10 % 10));
                else
                    sb.Append(i % 5 == 0 ? '+' : '-');
            }

            return sb.ToString();
        }

        /// <summary>How wide this screen lays itself out, one column short of the console as every screen here does.</summary>
        /// <returns>The width to compose against.</returns>
        private static int ScreenWidth()
        {
            return Math.Max(MinScreenWidth, AnsiConsole.SafeWindowWidth() - 1);
        }

        private void ToggleNumbers()
        {
            _lineNumbers = !_lineNumbers;
        }

        private void ToggleRuler()
        {
            _ruler = !_ruler;
        }

        private void ToggleAccessKeys()
        {
            _bar.ShowAccessKeys = !_bar.ShowAccessKeys;
        }

        private void ToggleGrey()
        {
            _grey = !_grey;
            ApplyLook();
        }

        private void ToggleCheckMark()
        {
            _asciiCheck = !_asciiCheck;
            ApplyLook();
        }

        private void SetBorder(BoxBorderEnum border)
        {
            _border = border;
        }

        private void SetPalette(int palette)
        {
            _palette = palette;
            ApplyLook();
        }

        /// <summary>Moves the document's width by one step, clamped, which is what the two switching entries watch.</summary>
        /// <param name="steps">How many steps, and which way.</param>
        private void Widen(int steps)
        {
            _docWidth = Math.Clamp(_docWidth + steps * DocStep, MinDocWidth, MaxDocWidth);
        }

        /// <summary>Puts every setting back where the screen opened, which is what File / Reset is enabled by.</summary>
        private void Reset()
        {
            _border = DefaultBorder;
            _palette = DefaultPalette;
            _docWidth = DefaultDocWidth;
            _lineNumbers = false;
            _ruler = false;
            _grey = true;
            _asciiCheck = false;
            _bar.ShowAccessKeys = true;

            ApplyLook();
        }

        /// <summary>
        ///     What the control is for, put up from a menu entry. Worth noticing that the menu shuts before this runs:
        ///     the bar closes itself first, so the dialog cannot come up behind an open panel, and an action that
        ///     rebuilt its own menu would not be undone by a close that happened afterwards.
        /// </summary>
        private void ShowAbout()
        {
            MessageBox.Show(ConsoleSimulationApp.Instance,
                "MenuBar keeps its layout: Layout() runs once and is read by" + Environment.NewLine +
                "both the drawing and the hit test, so the menu you see and the" + Environment.NewLine +
                "menu a click lands in cannot be different menus." + Environment.NewLine +
                Environment.NewLine +
                "That is what a numbered menu cannot promise, because MenuLayout" + Environment.NewLine +
                "reflows items into columns it does not keep, and it is why" + Environment.NewLine +
                "clicking one has never been supported.");
        }

        /// <summary>The keys, and what the pointer does, which is the other half a reader arrives wanting.</summary>
        private void ShowKeys()
        {
            MessageBox.Show(ConsoleSimulationApp.Instance,
                "F10 opens and shuts the bar and always works. ALT plus a" + Environment.NewLine +
                "title's underlined letter also opens it, on the terminals that" + Environment.NewLine +
                "deliver ALT at all; plenty swallow it, send an escape prefix" + Environment.NewLine +
                "or hand it to the window manager." + Environment.NewLine +
                Environment.NewLine +
                "Left and Right cross the menus, Up and Down walk the entries" + Environment.NewLine +
                "and skip the separators and anything greyed. ENTER chooses." +
                Environment.NewLine + Environment.NewLine +
                "The pointer opens a title with a press. Hovering does nothing" + Environment.NewLine +
                "until a menu is already open, and then sliding along the bar" + Environment.NewLine +
                "opens each in turn.");
        }
    }
}
