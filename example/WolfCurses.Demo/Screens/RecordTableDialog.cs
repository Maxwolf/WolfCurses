// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     Twenty-six ships read out of a CSV that lives in this file, drawn as a table you can walk in both
    ///     directions with a bar beside it, and whichever record the cursor is on shown underneath as a card. Four
    ///     library types that only mean anything together: <see cref="DelimitedText" /> turns the text into rows,
    ///     <see cref="DelimitedColumns" /> answers what a row holds in a named column, <see cref="TableViewport" />
    ///     decides what is on screen, and <see cref="ScrollBar" /> says where in the file that is.
    ///     <para>
    ///         <b>The quoted line break is what settles the whole design of the reader, and WLF-02 has one.</b> Its
    ///         note is two lines long and one row of the file, so a record and a line stopped being the same thing
    ///         the moment somebody typed it - which is why <c>DelimitedText.Read</c> takes the whole text rather
    ///         than a line at a time, and why no line-by-line splitter can be made to work however carefully it is
    ///         written. The two halves of this screen then treat that break in deliberately opposite ways: a table
    ///         row is a row, so the cell draws it as a picture through <see cref="ControlPictures" /> (written raw
    ///         it would put everything after it on the line below, over whatever was drawn there), while the card
    ///         underneath has rows to spare and wraps it.
    ///     </para>
    ///     <para>
    ///         <b>Reading is lenient and never throws, which is what the rest of the register is here to show.</b>
    ///         WLF-01's home port is quoted because it holds a comma; WLF-03 measures a beam in a bare inch mark,
    ///         which is a quote in the middle of an unquoted field and means itself; WLF-04 doubles its quotes to
    ///         get one of them and leaves its home port empty; WLF-05 simply stops after Tons. Real exports contain
    ///         all of those, and refusing to open the file is worse than opening it slightly oddly. Nothing is
    ///         trimmed either, because a leading space may be data.
    ///     </para>
    ///     <para>
    ///         <b>Every cell on screen is <c>Value(record, name)</c> and never <c>record[3]</c>.</b> The names this
    ///         screen asks with are spelled badly on purpose (<c>"NAME"</c>, <c>" home port "</c>) and are not in
    ///         the file's order, because a name is matched without case and with the ends trimmed where the data is
    ///         neither: a header cell is a name and a data cell is a value. The card's labels are the header
    ///         exactly as it was read, which is why one of them is lowercase - the file really does say
    ///         <c>" name "</c>. <c>Master</c> is a column the file has not got, and it sits on the card answering
    ///         empty beside WLF-05's missing Notes, because those are the two ragged shapes and <c>Value</c>
    ///         answers empty for both and throws for neither.
    ///     </para>
    ///     <para>
    ///         <b>The columns are deliberately uneven, which is the entire reason the scrolling is a
    ///         <see cref="TableViewport" /> and not a <c>TextViewport</c>.</b> Tons is six screen columns wide and
    ///         Home Port is twenty-two, so how much ground a step to the right gives up depends on which column is
    ///         being scrolled off and cannot be worked out by subtraction. Only whole columns are drawn, so the one
    ///         at the edge is never half shown; the furthest right is the last screenful rather than the last
    ///         column, which is what <c>ClampToTable</c> is called for whenever the console is measured again; and
    ///         clicking the empty ground past the last drawn column says so, because <c>ColumnAt</c> answers -1 out
    ///         there rather than rounding to the nearest column and moving the cursor somewhere nobody pointed at.
    ///     </para>
    ///     <para>
    ///         <b>The bar's thumb says two things and both have an off-by-one</b>, so the status line reads both of
    ///         them out. Its length is one cell here rather than none: twenty-six records over a four-cell track
    ///         rounds to zero, and the widget refuses to let it, or a long file would have an empty bar. Its
    ///         position is scaled against the furthest the window can start rather than against the record count,
    ///         so the readout's <c>at N of N</c> arrives exactly when WLF-26 comes on screen and not a cell before.
    ///     </para>
    ///     <para>
    ///         The card is a <see cref="FieldList" />, and its Notes field keeps three rows whether the record has
    ///         a note or not: <c>Lines</c> is a reservation rather than a maximum, so nothing below it moves as you
    ///         walk the register. WLF-08's note wants a fourth row and says so with an ellipsis rather than
    ///         stopping mid-word, which is the one thing a control that quietly drops the end of a value can never
    ///         tell you.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class RecordTableDialog : Form<DemoWindowInfo>
    {
        /// <summary>
        ///     The register, as a file would hold it.
        ///     <para>
        ///         It is a constant rather than an asset because the demo copies only <c>*.jpg</c>, <c>*.png</c>
        ///         and <c>*.gif</c> from the shared media folder, per extension: a <c>.csv</c> dropped in there
        ///         would silently never arrive beside the executable and this screen would open empty on every
        ///         machine but the one it was written on.
        ///     </para>
        ///     <para>
        ///         Rows are separated with a bare <c>\n</c> written out here rather than left to whatever this
        ///         source file happens to be saved with, so the record that carries a line break inside a quoted
        ///         field carries the same one on every machine. The header is spelled sloppily on purpose, spaces
        ///         and all, and the file ends with a line break, which must not invent a twenty-seventh row.
        ///     </para>
        /// </summary>
        private const string Register =
            "Call Sign, name ,Home Port,Tons,Notes\n" +
            "WLF-01,Aurora Borealis,\"Halifax, Nova Scotia\",480,Steel hull; refitted twice.\n" +
            "WLF-02,Grey Sister,Lunenburg,262,\"Laid up all winter.\n" +
            "Afloat again in April.\"\n" +
            "WLF-03,Katabatic,Port Hardy,1150,Ice class. Beam 24\" at the waist.\n" +
            "WLF-04,Marlinspike,,95,\"Known to her crew as \"\"Old Smoky\"\" and never anything else.\"\n" +
            "WLF-05,Sable Light,Shelburne,318\n" +
            "WLF-06,Northwest Passage,Tuktoyaktuk,2040,Widest name in the register.\n" +
            "WLF-07,Quill,Digby,86,\n" +
            "WLF-08,Cormorant,Saint John,540,\"Three rows were reserved for this note and it wants a " +
            "fourth, so the last of them ends in an ellipsis rather than stopping mid-word with nothing at all " +
            "to say that the rest of it is anywhere.\"\n" +
            "WLF-09,Bell Rock,Yarmouth,204,Bell cast in 1911.\n" +
            "WLF-10,Fogbound,Canso,151,\n" +
            "WLF-11,Ironbark,Pictou,762,Hull plating replaced in 1998.\n" +
            "WLF-12,Sou'wester,Peggys Cove,133,An apostrophe is not a quote and needs none.\n" +
            "WLF-13,Winter Harbour,Sydney,918,\n" +
            "WLF-14,Thrum Cap,Chester,177,\"Aground twice, refloated twice.\"\n" +
            "WLF-15,Longliner,Souris,244,\n" +
            "WLF-16,Petrel,Pubnico,109,\n" +
            "WLF-17,Gannet Rock,Grand Manan,331,\n" +
            "WLF-18,Sea Smoke,Trepassey,405,\n" +
            "WLF-19,Trawlerman,Bonavista,688,\n" +
            "WLF-20,Nor'easter,Twillingate,297,\n" +
            "WLF-21,Cape Split,Parrsboro,158,\n" +
            "WLF-22,Minas Basin,Windsor,222,\n" +
            "WLF-23,Bay of Fundy,Digby,1330,Deepest draught in the register.\n" +
            "WLF-24,Slack Water,Truro,64,\n" +
            "WLF-25,Spring Tide,Advocate,88,\n" +
            "WLF-26,Last Entry,Louisbourg,470,The file ends with a line break and that invents no row.\n";

        /// <summary>A column asked for that the file has not got, which is half of what <c>Value</c> is for.</summary>
        private const string AbsentColumn = "Master";

        /// <summary>The screen column the table's own first column starts in, leaving room for the row marker.</summary>
        private const int BodyColumn = 1;

        /// <summary>
        ///     The screen row this form's own text starts on.
        ///     <para>
        ///         The scene graph writes its spinner and the focused window's name first, and this app's
        ///         <c>OnPreRender</c> ends that line with the module readout, so everything composed here is one
        ///         row further down than the builder alone would say. It is a fact about the host rather than about
        ///         this screen, which is why it is named once here: a pointer test that leaves it out lands on the
        ///         row above the cell that was clicked, and nothing on screen says so.
        ///     </para>
        /// </summary>
        private const int FormTopRow = 1;

        /// <summary>Blank columns padding a cell, one before its text and one after.</summary>
        private const int Gutter = 2;

        /// <summary>The widest a cell may make its column, however long the value in it runs.</summary>
        private const int MaxCellWidth = 22;

        /// <summary>Rows the card keeps for a note, which is a reservation and not a maximum.</summary>
        private const int NoteLines = 3;

        /// <summary>
        ///     Every row of the frame that is neither a table row nor a card row: the scene graph's own line, the
        ///     blank under it, the title, the status, the column headings, the one row that is either the blank
        ///     above the card or the line saying there was no room for one, the message, the key hints, the input
        ///     prompt, and one more for the row the presenter clips away to keep that prompt on screen.
        ///     <para>
        ///         Counted rather than guessed. A budget one row out costs the bottom table row on every console
        ///         there is, or - the way this one first came out - pushes the prompt off a short one, where what
        ///         the user is typing simply cannot be seen.
        ///     </para>
        /// </summary>
        private const int ChromeRows = 10;

        /// <summary>Table rows shown on a console with nothing to spare.</summary>
        private const int MinBodyRows = 3;

        /// <summary>
        ///     Table rows shown on a console with plenty. Capped rather than let grow, because a table taller than
        ///     the register would show every record at once, and a screen about scrolling with nothing left to
        ///     scroll demonstrates nothing at all.
        /// </summary>
        private const int MaxBodyRows = 14;

        /// <summary>The narrowest the table body is allowed to get before it is simply clipped.</summary>
        private const int MinTableWidth = 20;

        /// <summary>The narrowest and widest the card may be drawn.</summary>
        private const int MinCardWidth = 28;

        private const int MaxCardWidth = 64;

        /// <summary>
        ///     How wide the card's label column is. Pinned rather than measured so the values do not shuffle
        ///     sideways when a longer label arrives; the list clamps it against its own width in any case.
        /// </summary>
        private const int LabelColumnWidth = 10;

        /// <summary>
        ///     The marker on the cursor's row.
        ///     <para>
        ///         Always drawn, exactly as <c>ListNavigator</c> draws its own: the inverse video below it is gated
        ///         on the terminal having colour at all, and on one that has none the highlighted row would
        ///         otherwise be indistinguishable from every other row.
        ///     </para>
        /// </summary>
        private const char Cursor = '>';

        /// <summary>What the key hints say, kept short enough to survive a narrow console uncut.</summary>
        private const string Hint =
            "arrows move  PGUP/PGDN page  click a cell or the bar  ENTER or ESC returns";

        /// <summary>What the message line says when the cursor is on a record with nothing in particular to teach.</summary>
        private const string DefaultMessage =
            "\"Master\" is no column of this file: Value answers empty and never throws.";

        /// <summary>What stands in for the card on a console with no rows to spare for it.</summary>
        private const string NoCard =
            "No room for the card on a console this short, so the table has the rows instead.";

        /// <summary>
        ///     The columns this screen draws, in the order it draws them, spelled deliberately badly and
        ///     deliberately not in the file's order.
        ///     <para>
        ///         Reordering the header in <see cref="Register" /> changes nothing on screen, which is the whole
        ///         difference between this and the <c>record[3]</c> everybody writes first: that one is right until
        ///         somebody moves a column, and then it is quietly wrong rather than broken.
        ///     </para>
        /// </summary>
        private static readonly string[] _shown = {"NAME", "call sign", " home port ", "Tons", "notes"};

        /// <summary>
        ///     What the message line says while the cursor is on a particular record, keyed by call sign rather
        ///     than by row number so that editing the register above cannot silently re-point them.
        /// </summary>
        private static readonly Dictionary<string, string> _lessons = new(StringComparer.Ordinal)
        {
            ["WLF-01"] = "Home port is quoted: the comma inside it is not a separator.",
            ["WLF-02"] = "This note holds a line break, so this record is two lines of the file.",
            ["WLF-03"] = "An inch mark: a bare quote mid-field is read as a quote, not as structure.",
            ["WLF-04"] = "Doubled quotes inside a quoted field mean one quote. Home port is blank.",
            ["WLF-05"] = "This row stops after Tons. Value answers empty for Notes rather than throwing.",
            ["WLF-08"] = "The note wants a fourth row, so the third one ends in an ellipsis.",
            ["WLF-26"] = "The file ends with a line break, which must not invent a 27th row."
        };

        /// <summary>The column headings, and the one column the cursor is in.</summary>
        private static readonly TextStyle _headingStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null,
            true);

        /// <summary>The cursor's row and its marker, and the heading of the column the cursor is in.</summary>
        private static readonly TextStyle _cursorStyle = new TextStyle(new TextColor(ConsoleColor.Black),
            new TextColor(ConsoleColor.DarkCyan));

        /// <summary>The title, the status readout and the hints, all of which should read as quieter than the data.</summary>
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        /// <summary>
        ///     The bar down the side of the table. Its length, total and position are set per frame; the glyphs and
        ///     the styles never change, so it is configured once here.
        /// </summary>
        private readonly ScrollBar _bar = new ScrollBar
        {
            ArrowStyle = _dimStyle,
            TrackStyle = _dimStyle,
            ThumbStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan))
        };

        /// <summary>Which slice of the register is on screen, and which columns of it.</summary>
        private readonly TableViewport _viewport = new TableViewport();

        /// <summary>The screen column the bar's cells are drawn in, which is where a press on it is looked for.</summary>
        private int _barColumn;

        /// <summary>The screen row the first table row is drawn on, counted from the builder that draws it.</summary>
        private int _bodyTopRow;

        /// <summary>The card under the table, showing whichever record the cursor is on.</summary>
        private FieldList _card;

        /// <summary>What the last press landed on, which replaces the message until the next key.</summary>
        private string _click;

        /// <summary>Which column the cursor is in, as an index into <see cref="_shown" />.</summary>
        private int _column;

        /// <summary>The header, and the only thing on this screen that knows where a named column lives.</summary>
        private DelimitedColumns _columns;

        /// <summary>Every row of the file but the header.</summary>
        private IReadOnlyList<IReadOnlyList<string>> _records;

        /// <summary>The whole frame, composed on the simulation tick and handed back unchanged by every render.</summary>
        private string _rendered = string.Empty;

        /// <summary>Which record the cursor is on.</summary>
        private int _row;

        /// <summary>Whether there are rows enough for the card as well as the table.</summary>
        private bool _showCard;

        /// <summary>How wide each shown column is drawn, gutters included. Uneven on purpose.</summary>
        private int[] _widths;

        /// <summary>Initializes a new instance of the <see cref="RecordTableDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public RecordTableDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "Arrows move, PGUP/PGDN page, HOME/END jump, click a cell, ENTER returns";

            Parse();
            Layout();
            Fill();
            Compose();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second, not a thousand times: measuring the console is two syscalls and composing the frame
            // copies a couple of kilobytes. A resize is the only thing that can move anything without a key press,
            // and a second's delay in noticing one is nothing beside paying for it on every system tick.
            if (systemTick)
                return;

            Layout();
            Compose();
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (_records == null)
                return;

            // Arrows, PAGE and HOME/END carry no character, so they never reach the input buffer and can only
            // arrive here. ENTER cannot arrive here at all - the input manager spends it closing the buffer - which
            // is why leaving is handled in OnInputBufferReturned and there is no case for it below.
            switch (key)
            {
                case ConsoleKey.UpArrow:
                    MoveTo(_row - 1, _column);
                    break;
                case ConsoleKey.DownArrow:
                    MoveTo(_row + 1, _column);
                    break;
                case ConsoleKey.LeftArrow:
                    MoveTo(_row, _column - 1);
                    break;
                case ConsoleKey.RightArrow:
                    MoveTo(_row, _column + 1);
                    break;
                case ConsoleKey.PageUp:
                    MoveTo(_row - _viewport.Rows, _column);
                    break;
                case ConsoleKey.PageDown:
                    MoveTo(_row + _viewport.Rows, _column);
                    break;
                case ConsoleKey.Home:
                    MoveTo(0, 0);
                    break;
                case ConsoleKey.End:
                    MoveTo(_records.Count - 1, _widths.Length - 1);
                    break;
                default:
                    return;
            }

            Compose();
        }

        /// <summary>
        ///     Picks a cell, or works the bar.
        ///     <para>
        ///         <b>This has to be its own override and cannot live in <c>OnMouseEvent</c>.</b>
        ///         <c>Window.OnMouseEvent</c> hands a press to <c>OnMousePressed</c> and returns, so a press never
        ///         reaches a form's <c>OnMouseEvent</c> at all and a hit test written there compiles and then runs
        ///         never. The collision demo records the same trap, which cost it a red test.
        ///     </para>
        ///     <para>
        ///         The two answers of -1 in here are both the widgets refusing to guess, and both are worth
        ///         meeting: <c>ColumnAt</c> gives it for the empty ground past the last drawn column, because
        ///         rounding a press to the nearest column moves the cursor somewhere nobody pointed at, and
        ///         <c>PositionForPress</c> gives it for a press on the thumb itself, because that is the start of a
        ///         drag rather than a jump.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (_records == null || mouse.Button != MouseButtonEnum.Left)
                return;

            var line = mouse.Row - _bodyTopRow;

            if (line < 0 || line >= _viewport.Rows)
                return;

            if (mouse.Column == _barColumn)
                PressBar(line);
            else
                PressCell(line, mouse.Column);

            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // A pure read of what the tick composed. The scene graph asks for this on every system tick, about a
            // thousand times a second, where the frame changes only when something is pressed.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Reads the register and works out how wide each column has to be.
        ///     <para>
        ///         The widths come out uneven because the values are: Tons never needs more than four characters
        ///         and a home port may need twenty. That is the case <see cref="TableViewport" /> exists for, and
        ///         the case a viewport written as multiplication gets wrong the first time a column is widened.
        ///     </para>
        /// </summary>
        private void Parse()
        {
            var rows = DelimitedText.Read(Register);

            _columns = new DelimitedColumns(rows[0]);

            var records = new List<IReadOnlyList<string>>(rows.Count - 1);

            for (var i = 1; i < rows.Count; i++)
                records.Add(rows[i]);

            _records = records;

            _widths = new int[_shown.Length];

            for (var i = 0; i < _shown.Length; i++)
            {
                // The heading counts too, or a column of short values draws its own name cut in half.
                var width = Heading(i).Length;

                foreach (var record in _records)
                    width = Math.Max(width, Flatten(_columns.Value(record, _shown[i])).Length);

                _widths[i] = Math.Min(width, MaxCellWidth) + Gutter;
            }

            var entries = new List<FieldListEntry>(_shown.Length + 1);

            // Labelled with the header as it was read rather than with the sloppy name asked for, which is why one
            // of them comes out lowercase: the file says " name " and this screen says "NAME".
            for (var i = 0; i < _shown.Length; i++)
                entries.Add(new FieldListEntry(Heading(i), string.Empty,
                    string.Equals(_shown[i].Trim(), "notes", StringComparison.OrdinalIgnoreCase) ? NoteLines : 1));

            entries.Add(new FieldListEntry(AbsentColumn));

            _card = new FieldList(entries.ToArray())
            {
                Gap = 1,
                MinimumLabelWidth = LabelColumnWidth,
                LabelStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan)),
                SelectedStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null, true)
            };
        }

        /// <summary>
        ///     Measures the console and hands the numbers to the viewport.
        ///     <para>
        ///         <c>ClampToTable</c> is called here and not only after a key press, because the thing that moves
        ///         the origin without anybody asking is a resize: widen the window and the furthest right becomes a
        ///         column further left, and a table left parked past it draws one narrow column beside an empty
        ///         window. The furthest right is the last screenful, never the last column.
        ///     </para>
        ///     <para>
        ///         <b>The card is what gives way when there is not room for both</b>, and being the one that gives
        ///         way is most of what it is for: a table with no rows shows no records at all, where a table with
        ///         no card still shows every field the console is wide enough to hold. The alternative is a frame
        ///         one row too tall, and what the presenter clips off the bottom of one of those is the input
        ///         prompt, which leaves the user typing at something they cannot see.
        ///     </para>
        /// </summary>
        private void Layout()
        {
            // Each of these is a live syscall, so they are read once into a local rather than per row.
            var width = AnsiConsole.SafeWindowWidth();
            var height = AnsiConsole.SafeWindowHeight();

            _card.Width = Math.Clamp(width - BodyColumn - 1, MinCardWidth, MaxCardWidth);

            var withCard = height - ChromeRows - _card.Height;
            _showCard = withCard >= MinBodyRows;

            _viewport.Resize(Math.Max(MinTableWidth, width - BodyColumn - 1),
                Math.Clamp(_showCard ? withCard : height - ChromeRows, MinBodyRows, MaxBodyRows));

            _viewport.ClampToTable(_records.Count, _widths);
            _viewport.EnsureVisible(_row, _column, _widths);
        }

        /// <summary>Moves the cursor and scrolls the least amount that brings it back into view.</summary>
        /// <param name="row">The record wanted.</param>
        /// <param name="column">The column wanted.</param>
        private void MoveTo(int row, int column)
        {
            _row = Math.Clamp(row, 0, _records.Count - 1);
            _column = Math.Clamp(column, 0, _widths.Length - 1);
            _click = null;

            // Sideways this gives up one column at a time rather than subtracting a width, because how much ground
            // a step costs depends on which columns are being scrolled off.
            _viewport.EnsureVisible(_row, _column, _widths);

            Fill();
        }

        /// <summary>Puts the cursor's record on the card.</summary>
        private void Fill()
        {
            var record = _records[_row];

            for (var i = 0; i < _shown.Length; i++)
                _card.Entries[i].Value = _columns.Value(record, _shown[i]);

            // The two ragged cases, side by side and neither of them an exception: a column the file has not got,
            // and (on WLF-05) a row that stops before reaching one it has.
            _card.Entries[_shown.Length].Value = _columns.Value(record, AbsentColumn);

            _card.Selected = _column;
        }

        /// <summary>Acts on a press somewhere on the bar, and says which of its three parts was hit.</summary>
        /// <param name="cell">Which cell of the bar, counting from the arrow cap at the top.</param>
        private void PressBar(int cell)
        {
            if (_bar.IsOnThumb(cell))
            {
                _click = "PositionForPress answers -1 on the thumb: a press there begins a drag.";
                return;
            }

            var position = _bar.PositionForPress(cell);

            if (position < 0)
                return;

            _viewport.ScrollTo(position, _viewport.FirstColumn);
            _viewport.ClampToTable(_records.Count, _widths);

            // The view moved and the cursor did not, which is what scrolling is: the card still shows the record
            // that was chosen, whether or not the table is still drawing it.
            _click = cell == 0 || cell == _bar.Length - 1
                ? "An arrow cap steps the view one record."
                : "The track beyond the thumb jumps a windowful.";
        }

        /// <summary>Acts on a press somewhere over the table body.</summary>
        /// <param name="line">The line within the body, zero at the top.</param>
        /// <param name="screenColumn">The screen column pressed.</param>
        private void PressCell(int line, int screenColumn)
        {
            var column = _viewport.ColumnAt(screenColumn - BodyColumn, _widths);

            if (column < 0)
            {
                _click = "ColumnAt answered -1: that is the ground past the last drawn column.";
                return;
            }

            var at = _viewport.RowAt(line);

            if (at < 0 || at >= _records.Count)
            {
                _click = "No record on that row.";
                return;
            }

            _row = at;
            _column = column;

            Fill();

            _click = string.Format(CultureInfo.InvariantCulture, "Picked {0}, column \"{1}\".",
                _columns.Value(_records[_row], "call sign"), Heading(_column));
        }

        /// <summary>Builds the whole frame, which is what every render between now and the next one hands back.</summary>
        private void Compose()
        {
            var width = AnsiConsole.SafeWindowWidth();

            // Before the status line and not inside the table, which is where it started out: the readout below
            // quotes the bar's own numbers, so a bar told them afterwards spends the whole first frame describing
            // the widget's defaults and every frame after that describing the one before it.
            _bar.Length = _viewport.Rows;
            _bar.Visible = _viewport.Rows;
            _bar.Total = Math.Max(_records.Count, _viewport.Rows);
            _bar.Position = _viewport.FirstRow;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(_headingStyle.Apply(Clip(
                "Record table: DelimitedText, DelimitedColumns, TableViewport, ScrollBar", width)));
            sb.AppendLine(_dimStyle.Apply(Clip(Status(), width)));

            AppendTable(sb);

            // One row either way, so the frame is the same height whichever of the two it turns out to be.
            if (_showCard)
            {
                sb.AppendLine();

                var margin = new string(' ', BodyColumn);

                foreach (var row in _card.Render())
                    sb.AppendLine(margin + row);
            }
            else
            {
                sb.AppendLine(_dimStyle.Apply(Clip(NoCard, width)));
            }

            sb.AppendLine(_dimStyle.Apply(Clip(Message(), width)));
            sb.Append(_dimStyle.Apply(Clip(Hint, width)));

            _rendered = sb.ToString();
        }

        /// <summary>
        ///     Draws the column headings and the table body, and notes where the body landed while it is doing it.
        ///     <para>
        ///         <b>The body's first row is counted from the builder rather than written down as a constant.</b>
        ///         A constant is right until somebody adds a line to the header, at which point every press lands
        ///         one row out and nothing anywhere reports a problem.
        ///     </para>
        /// </summary>
        /// <param name="sb">The frame being composed.</param>
        private void AppendTable(StringBuilder sb)
        {
            // Whole columns only, and never fewer than one: a column wider than the window is drawn clipped rather
            // than not at all, or there would be no cell to click on to get anywhere else.
            var drawn = _viewport.VisibleColumns(_widths);

            var heading = new TextRow().Append(' ', BodyColumn);
            var used = 0;

            for (var i = 0; i < drawn; i++)
            {
                var column = _viewport.FirstColumn + i;
                var room = Room(column, used);

                heading.Append(AnsiText.Fit(" " + Heading(column), room),
                    column == _column ? _cursorStyle : _headingStyle);

                used += room;
            }

            // One cell wider than the body, since the heading row sits above the bar rather than beside it and the
            // rectangle has to stay square either way.
            sb.AppendLine(heading.Append(' ', _viewport.Width - used + 1, _headingStyle).Render());

            _bodyTopRow = FrameLayout.RowsAboveForm(SimUnit) + FrameLayout.CountLineBreaks(sb);
            _barColumn = BodyColumn + _viewport.Width;

            var cells = _bar.Cells();

            for (var line = 0; line < _viewport.Rows; line++)
            {
                var at = _viewport.RowAt(line);
                var record = at >= 0 && at < _records.Count ? _records[at] : null;
                var chosen = at == _row;
                var style = chosen ? _cursorStyle : TextStyle.None;

                var body = new TextRow().Append(chosen ? Cursor : ' ', BodyColumn, style);
                used = 0;

                for (var i = 0; i < drawn; i++)
                {
                    var column = _viewport.FirstColumn + i;
                    var room = Room(column, used);

                    body.Append(record == null
                        ? new string(' ', room)
                        : Cell(_columns.Value(record, _shown[column]), room), style);

                    used += room;
                }

                sb.AppendLine(body.Append(' ', _viewport.Width - used, style).Render() + cells[line]);
            }
        }

        /// <summary>
        ///     How much of a column's width there is still room to draw.
        ///     <para>
        ///         Only the last drawn column can ever be short of room, and only when it is wider than the whole
        ///         window. Clipping it here keeps the body exactly <c>Width</c> columns, which is what puts the bar
        ///         where the hit test looks for it; the viewport goes on measuring that column at its full width,
        ///         so a press inside the part that was drawn still answers with it.
        ///     </para>
        /// </summary>
        /// <param name="column">Which column.</param>
        /// <param name="used">How much of the body has been drawn already.</param>
        /// <returns>The columns to draw it in, never below zero.</returns>
        private int Room(int column, int used)
        {
            return Math.Min(_widths[column], Math.Max(0, _viewport.Width - used));
        }

        /// <summary>
        ///     The readout that makes the two scrollbar off-by-ones watchable: the thumb's length in track cells,
        ///     and how far down the track it has got of how far it can go. The second pair reaching its end is what
        ///     "the last record is on screen" looks like, and a bar scaled against the record count instead would
        ///     never quite arrive.
        /// </summary>
        /// <returns>The status line.</returns>
        private string Status()
        {
            var slack = _bar.TrackLength - _bar.ThumbLength;

            return string.Format(CultureInfo.InvariantCulture,
                "row {0}/{1}  col {2}/{3} \"{4}\"  {5} drawn  thumb {6} of {7} cells {8}",
                _row + 1, _records.Count, _column + 1, _widths.Length, Heading(_column),
                _viewport.VisibleColumns(_widths), _bar.ThumbLength, _bar.TrackLength,
                slack > 0
                    ? string.Format(CultureInfo.InvariantCulture, "at {0} of {1}", _bar.ThumbStart, slack)
                    : "filling the track");
        }

        /// <summary>What the message line says: the last press if there was one, else whatever this record teaches.</summary>
        /// <returns>The message.</returns>
        private string Message()
        {
            if (_click != null)
                return _click;

            return _lessons.TryGetValue(_columns.Value(_records[_row], "call sign"), out var lesson)
                ? lesson
                : DefaultMessage;
        }

        /// <summary>The column's name as the file spells it, falling back to the name asked for when it has none.</summary>
        /// <param name="column">Which shown column.</param>
        /// <returns>The heading text.</returns>
        private string Heading(int column)
        {
            var at = _columns.IndexOf(_shown[column]);

            return at >= 0 ? _columns.Names[at] : _shown[column].Trim();
        }

        /// <summary>
        ///     One cell, padded to its column and marked when it had to be cut.
        ///     <para>
        ///         The mark is the same courtesy <see cref="FieldList.Overflow" /> pays on the card below, and it
        ///         is needed here for the same reason: a cell that simply stops mid-word says nothing about whether
        ///         there was more of it, and in the Notes column - the one column capped rather than measured -
        ///         there usually is. The card is where the whole of the value lives.
        ///     </para>
        /// </summary>
        /// <param name="value">The value as the file holds it.</param>
        /// <param name="room">The columns this cell has, gutters included.</param>
        /// <returns>The cell.</returns>
        private static string Cell(string value, int room)
        {
            var text = Flatten(value);

            if (room > Gutter && text.Length > room - Gutter)
                text = text.Substring(0, room - Gutter - 1) + '…';

            return AnsiText.Fit(" " + text, room);
        }

        /// <summary>
        ///     A value as a table row can carry it.
        ///     <para>
        ///         <b>A terminal does not draw a control character, it obeys it.</b> WLF-02's note holds a line
        ///         break, and a row of a table is a row: written raw it would move the cursor down part way through
        ///         the row, so everything after it would land on the line below over whatever was drawn there, and
        ///         nothing would look wrong until something was drawn under the table. <see cref="ControlPictures" />
        ///         is one character in and one character out, which is what keeps every column measurement made
        ///         above still true.
        ///     </para>
        /// </summary>
        /// <param name="value">The value as the file holds it.</param>
        /// <returns>The value as a row can draw it.</returns>
        private static string Flatten(string value)
        {
            return ControlPictures.Replace(value);
        }

        /// <summary>Cuts a plain line to the console's width, which is a truncation and never a wrap.</summary>
        /// <param name="text">The line, before any style is applied to it.</param>
        /// <param name="width">The console's width.</param>
        /// <returns>The line, no wider than the console.</returns>
        private static string Clip(string text, int width)
        {
            // The presenter turns auto-wrap off for the frame, so an over-wide row is truncated by the terminal
            // rather than reflowed onto the next one - which would push everything below it down a row. Cutting the
            // text before it is styled keeps this a plain substring instead of a walk through escape sequences.
            return width > 0 && text.Length > width ? text.Substring(0, width) : text;
        }
    }
}
