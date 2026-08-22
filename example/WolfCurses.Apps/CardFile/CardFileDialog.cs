// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.IO;
using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     A card index: one card at a time with its fields laid out, or all of them at once as a table, with a row
    ///     of letter tabs across the top to flip to.
    ///     <para>
    ///         <b>This is the screen that has to distrust a file it wrote itself.</b> Everything else in the suite
    ///         reads a file somebody else made, or reads its own and gets away with assuming the shape. What this
    ///         one writes, the user can open in the word processor three menu items away, move a column in, delete
    ///         one, and hand-type a row that is a field short - and then open it here again. So nothing is read by
    ///         position: the header row says what the columns are and every value is fetched by name, which is
    ///         what <see cref="Documents.DelimitedColumns" /> is for.
    ///     </para>
    ///     <para>
    ///         <b>It is also where the library's four modal controls appear as a workflow rather than one at a
    ///         time.</b> Opening another file with unsaved changes asks whether to save first
    ///         (<see cref="MessageBox" />), which may ask for a folder (<see cref="FileDialog" />) and then a name
    ///         (<see cref="TextInputDialog" />) before finally asking which file to open; and which fields the list
    ///         shows is a <see cref="SelectList" /> of them all with the current ones already ticked.
    ///     </para>
    ///     <para>
    ///         The card itself has no drawing arithmetic anywhere in this application, because
    ///         <see cref="FieldList" /> keeps its own layout. That is the whole reason it went into the library:
    ///         each of the planner's four views needed a hit test of its own, and this needs none.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (OfficeWindow))]
    public sealed class CardFileDialog : Form<OfficeWindowInfo>, IHandlesEscape
    {
        /// <summary>How many cards a page key moves through the deck.</summary>
        private const int PageCards = 10;

        /// <summary>How much of a name the status strip will spare, so the key hints always fit beside it.</summary>
        private const int StatusName = 20;

        /// <summary>Where the table has been scrolled to, sideways as well as down.</summary>
        private readonly TableViewport _viewport = new();

        /// <summary>Which fields the list shows as columns, in field order.</summary>
        private List<int> _shown = new() {0, 1, 2};

        /// <summary>The pull-down menus across the top.</summary>
        private MenuBar _menuBar;

        /// <summary>The letter tabs, which are a keypad because a keypad is a grid of clickable labels.</summary>
        private Keypad _tabs;

        /// <summary>The chosen card's fields, which keep their own layout and so need no hit test here.</summary>
        private FieldList _fields;

        /// <summary>The cards.</summary>
        private CardDeck _deck = new();

        /// <summary>How wide each shown column is, worked out when the deck changes rather than while drawing.</summary>
        private int[] _widths = Array.Empty<int>();

        /// <summary>Which way the card file is being looked at.</summary>
        private CardViewEnum _view = CardViewEnum.Card;

        /// <summary>Which card the cursor is on, which is the one thing both views agree about.</summary>
        private int _selected;

        /// <summary>The console width, sampled on the tick rather than read while drawing.</summary>
        private int _width = 78;

        /// <summary>The file the deck came from, or null for one that has never been on disk.</summary>
        private string _path;

        /// <summary>What was last searched for, offered again so Find Next is F9 and ENTER.</summary>
        private string _search = string.Empty;

        /// <summary>What the status strip has to say, when it is not listing the keys.</summary>
        private string _message;

        /// <summary>Initializes a new instance of the <see cref="CardFileDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public CardFileDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     ENTER arrives as a key press rather than being spent on the input buffer. Without it the menu bar's
        ///     own ENTER handling is unreachable, so a menu opens, walks with the arrows and does nothing at the
        ///     end of it, and an entry with no shortcut of its own can only be chosen with the pointer.
        /// </summary>
        public override bool EditsText => true;

        /// <summary>Typed characters do not go into the prompt underneath: a letter here flips to a tab.</summary>
        public override bool InputFillsBuffer => false;

        /// <summary>The card the cursor is on, or null when the deck is empty.</summary>
        private Card Current => _selected >= 0 && _selected < _deck.Count ? _deck.Cards[_selected] : null;

        /// <summary>The file's name for the title tab, or something honest for a deck that has never been saved.</summary>
        private string FileName => string.IsNullOrEmpty(_path) ? "Untitled" : Path.GetFileName(_path);

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (_menuBar == null || !_menuBar.IsOpen)
                return false;

            _menuBar.Close();
            return true;
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            _width = Math.Max(40, AnsiConsole.SafeWindowWidth() - 1);
            _fields = CardView.Build(_width);

            BuildTabs();
            BuildMenus();

            LoadCards(CardFileLibrary.DefaultCardsPath);

            // Through ShowView rather than by assigning the view, because that is what puts the highlight on a
            // field. The field list starts pointing at nothing, which is right for one only being read.
            ShowView(CardViewEnum.Card);
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // The console size is a syscall, so it is asked once a second rather than a thousand times, exactly as
            // the spreadsheet does it.
            if (systemTick)
                return;

            var width = Math.Max(40, AnsiConsole.SafeWindowWidth() - 1);

            if (width == _width)
                return;

            _width = width;
            _fields.Width = _width - 2;

            RefreshColumns();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "F10 opens the menus, ESC returns to the suite:";

            CardView.Fill(_fields, Current);

            return Environment.NewLine +
                   CardChrome.Compose(_menuBar, _tabs, _view, _deck, _fields, _shown, _widths, _viewport,
                       _selected, Heading(), StatusText(), _width);
        }

        /// <summary>
        ///     Never called: <see cref="EditsText" /> is precisely the declaration that ENTER should arrive as a
        ///     key press instead, which is where both the menu bar and <see cref="Choose" /> hear it.
        /// </summary>
        /// <param name="input">Unused.</param>
        public override void OnInputBufferReturned(string input)
        {
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            if (_menuBar != null && _menuBar.HandleKey(keyInfo))
                return;

            switch (keyInfo.Key)
            {
                case ConsoleKey.Enter:
                    Choose();
                    return;

                case ConsoleKey.Tab:
                    ShowView(_view == CardViewEnum.Card ? CardViewEnum.List : CardViewEnum.Card);
                    return;

                case ConsoleKey.UpArrow:
                    StepBack();
                    return;

                case ConsoleKey.DownArrow:
                    StepOn();
                    return;

                case ConsoleKey.LeftArrow:
                    StepSideways(-1);
                    return;

                case ConsoleKey.RightArrow:
                    StepSideways(1);
                    return;

                case ConsoleKey.PageUp:
                    MoveCards(_view == CardViewEnum.List
                        ? -CardListView.VisibleRows(CardChrome.BodyRows)
                        : -PageCards);
                    return;

                case ConsoleKey.PageDown:
                    MoveCards(_view == CardViewEnum.List
                        ? CardListView.VisibleRows(CardChrome.BodyRows)
                        : PageCards);
                    return;

                case ConsoleKey.Home:
                    GoTo(0);
                    _message = null;
                    return;

                case ConsoleKey.End:
                    GoTo(_deck.Count - 1);
                    _message = null;
                    return;

                case ConsoleKey.F2:
                    EditField();
                    return;

                case ConsoleKey.F3:
                    OpenCards();
                    return;

                case ConsoleKey.F4:
                    SaveCards();
                    return;

                case ConsoleKey.F5:
                    ShowView(CardViewEnum.Card);
                    return;

                case ConsoleKey.F6:
                    ShowView(CardViewEnum.List);
                    return;

                case ConsoleKey.F7:
                    NewCard();
                    return;

                case ConsoleKey.F9:
                    FindCard();
                    return;

                case ConsoleKey.Delete:
                    DeleteCard();
                    return;
            }

            // A letter flips to that tab, which is what a card index is for and what its tabs are. Left until last
            // so nothing above it has to check whether the key also happens to carry a character.
            if (char.IsLetter(keyInfo.KeyChar))
                JumpTo(char.ToUpperInvariant(keyInfo.KeyChar));
            else if (char.IsDigit(keyInfo.KeyChar))
                JumpTo(Card.OtherLetter);
        }

        /// <inheritdoc />
        public override void OnMouseEvent(MouseEvent mouse)
        {
            switch (mouse.Kind)
            {
                case MouseEventKindEnum.Press:
                    OnMousePressed(mouse);
                    return;

                case MouseEventKindEnum.Wheel:
                    Wheel(-mouse.WheelDelta);
                    return;

                case MouseEventKindEnum.Move:
                    if (_menuBar != null && _menuBar.HandleMouseMove(mouse.Row, mouse.Column))
                        return;

                    // The tabs only light up while no menu is down, or a pointer crossing the panel on its way
                    // somewhere would light a tab behind it.
                    if (_tabs != null && (_menuBar == null || !_menuBar.IsOpen))
                        _tabs.Hover(mouse.Row, mouse.Column);

                    return;
            }
        }

        /// <inheritdoc />
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (_menuBar != null && _menuBar.HandleMouse(mouse.Row, mouse.Column))
                return;

            if (mouse.Button != MouseButtonEnum.Left)
                return;

            // The tabs answer for themselves, since a keypad remembers where it drew every key.
            if (_tabs != null && _tabs.Press(mouse.Row, mouse.Column))
                return;

            if (_view == CardViewEnum.List)
            {
                var card = CardListView.CardAt(_deck, _viewport, CardChrome.BodyRows, mouse.Row);

                if (card >= 0)
                {
                    GoTo(card);
                    _message = null;
                }

                return;
            }

            var field = _fields.FieldAt(mouse.Row, mouse.Column);

            if (field >= 0)
            {
                _fields.Selected = field;
                _message = null;
            }
        }

        /// <summary>
        ///     What ENTER means, which is "act on what is picked out": open the highlighted card from the list,
        ///     and edit the highlighted field on the card.
        /// </summary>
        private void Choose()
        {
            if (_view == CardViewEnum.List)
            {
                ShowView(CardViewEnum.Card);
                return;
            }

            EditField();
        }

        /// <summary>What UP means, which is a field on a card and a card in the list.</summary>
        private void StepBack()
        {
            if (_view == CardViewEnum.List)
                MoveCards(-1);
            else
                MoveField(-1);
        }

        /// <summary>What DOWN means, the mirror of <see cref="StepBack" />.</summary>
        private void StepOn()
        {
            if (_view == CardViewEnum.List)
                MoveCards(1);
            else
                MoveField(1);
        }

        /// <summary>
        ///     What LEFT and RIGHT mean: flipping through the cards on the card, and scrolling the table sideways
        ///     in the list, where the cards are already what up and down move through.
        /// </summary>
        /// <param name="step">Which way; negative goes back.</param>
        private void StepSideways(int step)
        {
            if (_view != CardViewEnum.List)
            {
                MoveCards(step);
                return;
            }

            _viewport.ScrollBy(0, step);
            _viewport.ClampToTable(_deck.Count, _widths);
            _message = null;
        }

        /// <summary>Moves the cursor through the deck.</summary>
        /// <param name="step">How many cards; negative goes back.</param>
        private void MoveCards(int step)
        {
            if (_deck.Count == 0)
                return;

            GoTo(_selected + step);
            _message = null;
        }

        /// <summary>Moves the highlight through the card's fields.</summary>
        /// <param name="step">How many fields; negative goes back.</param>
        private void MoveField(int step)
        {
            _fields.Selected = Math.Clamp(_fields.Selected + step, 0, _fields.Entries.Count - 1);
            _message = null;
        }

        /// <summary>Puts the cursor on a card, clamped to the deck, and scrolls it into sight.</summary>
        /// <param name="index">Which card.</param>
        private void GoTo(int index)
        {
            _selected = _deck.Count == 0 ? 0 : Math.Clamp(index, 0, _deck.Count - 1);

            Reveal();
        }

        /// <summary>Flips to the first card behind a letter tab.</summary>
        /// <param name="letter">The tab.</param>
        private void JumpTo(char letter)
        {
            var at = _deck.FirstBehind(letter);

            if (at < 0)
            {
                _message = "Nothing is filed under " + letter + ".";
                return;
            }

            GoTo(at);
            _message = null;
        }

        /// <summary>Switches which way the card file is being looked at.</summary>
        /// <param name="view">The view to show.</param>
        private void ShowView(CardViewEnum view)
        {
            _view = view;
            _message = null;

            // The card is for choosing a field, so its highlight starts on one rather than hidden, which is what
            // the field list's own default is for a list only being read.
            if (view == CardViewEnum.Card && _fields.Selected < 0)
                _fields.Selected = 0;

            Reveal();
        }

        /// <summary>Scrolls the list so the chosen card is on screen, and does nothing to the card view.</summary>
        private void Reveal()
        {
            if (_view != CardViewEnum.List || _deck.Count == 0)
                return;

            // The current first column is passed back in, which asks the viewport to keep the row visible without
            // dragging the sideways scroll back to where it started.
            _viewport.EnsureVisible(_selected, _viewport.FirstColumn, _widths);
        }

        /// <summary>Turns the wheel: through the cards on the card, down the table in the list.</summary>
        /// <param name="lines">How far; negative goes up.</param>
        private void Wheel(int lines)
        {
            if (_view != CardViewEnum.List)
            {
                MoveCards(lines);
                return;
            }

            _viewport.ScrollBy(lines * 3, 0);
            _viewport.ClampToTable(_deck.Count, _widths);
        }

        /// <summary>
        ///     Works out how wide each shown column has to be, and keeps the viewport inside the table.
        ///     <para>
        ///         Done when the deck or the chosen columns change rather than while drawing, because it walks
        ///         every card once per column and a render happens about a thousand times a second. Same discipline
        ///         as the planner working out its list on a key press rather than on a frame.
        ///     </para>
        /// </summary>
        private void RefreshColumns()
        {
            _widths = CardListView.ColumnWidths(_deck, _shown);

            _viewport.Resize(CardListView.TableWidth(_width), CardListView.VisibleRows(CardChrome.BodyRows));
            _viewport.ClampToTable(_deck.Count, _widths);

            Reveal();
        }

        /// <summary>Asks for a new value for the field the cursor is on, and files the card again if it was the name.</summary>
        private void EditField()
        {
            var card = Current;

            if (card == null)
            {
                _message = "There are no cards. F7 starts one.";
                return;
            }

            if (_view == CardViewEnum.List)
                ShowView(CardViewEnum.Card);

            var field = Math.Clamp(_fields.Selected, 0, card.Fields - 1);

            TextInputDialog.Prompt(
                SimUnit,
                Card.FieldNames[field] + " for " + card.Name + "?",
                value => Apply(card, field, value),
                () => _message = "Left as it was.",

                // Offered flat, because a prompt is one line. A note holding several comes back as one; the word
                // processor three menu items away is where a longer one is written.
                CardListView.Flatten(card[field]));
        }

        /// <summary>Writes a value into a field.</summary>
        /// <param name="card">The card.</param>
        /// <param name="field">Which field.</param>
        /// <param name="value">The new value.</param>
        private void Apply(Card card, int field, string value)
        {
            card[field] = value;

            // Renaming a card moves it, since the deck is an index rather than a list, so the cursor follows the
            // card rather than staying on the position it used to be at.
            if (field == Card.NameField)
                _selected = _deck.Resort(_selected);
            else
                _deck.Touch();

            RefreshColumns();
            _message = Card.FieldNames[field] + " changed.";
        }

        /// <summary>Empties the field the cursor is on, which the prompt cannot do since a blank line cancels it.</summary>
        private void ClearField()
        {
            var card = Current;

            if (card == null || _view != CardViewEnum.Card)
                return;

            var field = Math.Clamp(_fields.Selected, 0, card.Fields - 1);

            if (field == Card.NameField)
            {
                _message = "A card has to keep its name; the index is by name.";
                return;
            }

            card[field] = string.Empty;
            _deck.Touch();

            RefreshColumns();
            _message = Card.FieldNames[field] + " cleared.";
        }

        /// <summary>Whether there is a field the cursor is on that could be emptied.</summary>
        /// <returns>TRUE when Clear Field would do something.</returns>
        private bool CanClear()
        {
            var card = Current;

            if (card == null || _view != CardViewEnum.Card || _fields.Selected == Card.NameField)
                return false;

            return _fields.Selected >= 0 && card[_fields.Selected].Length > 0;
        }

        /// <summary>Asks for a name and starts a card with it.</summary>
        private void NewCard()
        {
            TextInputDialog.Prompt(
                SimUnit,
                "What is the new card's name?",
                name =>
                {
                    var at = _deck.Add(new Card(name));

                    if (at < 0)
                    {
                        _message = "A card needs a name.";
                        return;
                    }

                    RefreshColumns();
                    ShowView(CardViewEnum.Card);
                    GoTo(at);

                    // The name is already filled in, so the cursor starts on the next thing to be asked.
                    _fields.Selected = Card.NameField + 1;
                    _message = "New card. F2 fills in a field.";
                },
                () => _message = "No card added.");
        }

        /// <summary>Confirms and then throws a card away, since there is no undo anywhere in this suite.</summary>
        private void DeleteCard()
        {
            var card = Current;

            if (card == null)
            {
                _message = "There are no cards to remove.";
                return;
            }

            MessageBox.Confirm(
                SimUnit,
                "Throw away the card for " + card.Name + "?",
                () =>
                {
                    _deck.RemoveAt(_selected);

                    RefreshColumns();
                    GoTo(_selected);

                    _message = "Card thrown away.";
                },
                () => _message = "Card kept.");
        }

        /// <summary>Looks for text in any field of any card, starting after the one the cursor is on.</summary>
        private void FindCard()
        {
            TextInputDialog.Prompt(
                SimUnit,
                "Find which text?",
                needle =>
                {
                    _search = needle;

                    var from = _selected;
                    var at = _deck.Find(needle, from);

                    if (at < 0)
                    {
                        _message = "Nothing holds \"" + needle + "\".";
                        return;
                    }

                    GoTo(at);
                    _message = at <= from ? "Wrapped round to the start." : "Found in " + _deck.Cards[at].Name + ".";
                },
                () => _message = "Find cancelled.",

                // Offered back, so F9 and ENTER is Find Next and the search starting after the current card is
                // what makes that walk the matches rather than stand still.
                _search);
        }

        /// <summary>Asks which fields the list shows, with the current ones already ticked.</summary>
        private void ChooseColumns()
        {
            SelectList.ChooseMany(
                SimUnit,
                "Which fields does the list show?",
                Card.FieldNames,
                chosen =>
                {
                    if (chosen.Count == 0)
                    {
                        _message = "The list needs at least one column.";
                        return;
                    }

                    // The indexes come back in ascending order, so the columns are always in field order however
                    // they were ticked. One less thing for the table to remember.
                    _shown = new List<int>(chosen);

                    RefreshColumns();
                    _viewport.ScrollTo(_viewport.FirstRow, 0);

                    _message = "The list now shows " + chosen.Count + " of " + Card.FieldNames.Count + " fields.";
                },
                () => _message = "Columns left as they were.",
                _shown);
        }

        /// <summary>
        ///     Opens another card file, offering to save first when there is anything to lose. The whole of what
        ///     the README means by the modal controls arriving as a workflow: a question, then possibly a folder
        ///     and a name, and only then the file to open.
        /// </summary>
        private void OpenCards()
        {
            if (!_deck.IsModified)
            {
                Browse();
                return;
            }

            MessageBox.Show(
                SimUnit,
                "Save the changes to " + FileName + " first?",
                MessageBoxButtonsEnum.YesNoCancel,
                result =>
                {
                    switch (result)
                    {
                        case MessageBoxResultEnum.Yes:
                            SaveThen(Browse);
                            return;

                        case MessageBoxResultEnum.No:
                            Browse();
                            return;

                        default:
                            _message = "Open cancelled.";
                            return;
                    }
                });
        }

        /// <summary>Asks which file to open.</summary>
        private void Browse()
        {
            FileDialog.OpenFile(
                SimUnit,
                CardFileLibrary.BrowseFolder,
                CardFileLibrary.Extensions,
                LoadCards,
                () => _message = "Open cancelled.");
        }

        /// <summary>Writes the deck back where it came from, or asks where to put it.</summary>
        private void SaveCards()
        {
            SaveThen(null);
        }

        /// <summary>Saves, and then does something else once it has, which is what makes the Open chain work.</summary>
        /// <param name="next">What to do afterwards; null for nothing.</param>
        private void SaveThen(Action next)
        {
            if (string.IsNullOrEmpty(_path))
            {
                SaveCardsAs(next);
                return;
            }

            if (Write(_path))
                next?.Invoke();
        }

        /// <summary>Asks for a folder and then a name, since the library still has no Save As of its own.</summary>
        /// <param name="next">What to do once it is written; null for nothing.</param>
        private void SaveCardsAs(Action next = null)
        {
            FileDialog.SelectFolder(
                SimUnit,
                CardFileLibrary.BrowseFolder,
                folder => TextInputDialog.Prompt(
                    SimUnit,
                    "Save as which file name?",
                    name =>
                    {
                        if (Write(Path.Combine(folder, name)))
                            next?.Invoke();
                    },
                    () => _message = "Save cancelled.",
                    _path == null ? CardFileLibrary.DefaultCardsName : Path.GetFileName(_path)),
                () => _message = "Save cancelled.");
        }

        /// <summary>Writes the deck to a path and says how it went.</summary>
        /// <param name="path">Where to write.</param>
        /// <returns>TRUE when it was written.</returns>
        private bool Write(string path)
        {
            if (!CardFileLibrary.TrySave(_deck, path, out var error))
            {
                _message = "Could not save " + Path.GetFileName(path) + ": " + error;
                return false;
            }

            _path = path;
            _deck.MarkSaved();
            _message = "Saved " + Path.GetFileName(path) + ".";

            return true;
        }

        /// <summary>Reads a card file, or leaves the deck alone and says why it could not.</summary>
        /// <param name="path">The file to read.</param>
        private void LoadCards(string path)
        {
            var loaded = CardFileLibrary.TryLoad(path, out var error);

            if (loaded == null)
            {
                _message = "Could not open " + Path.GetFileName(path) + ": " + error;
                return;
            }

            _deck = loaded;
            _path = path;
            _message = null;

            RefreshColumns();
            GoTo(0);
        }

        /// <summary>Builds the letter tabs.</summary>
        private void BuildTabs()
        {
            var buttons = new List<KeypadButton>();

            for (var letter = 'A'; letter <= 'Z'; letter++)
            {
                // Captured into a local, or every tab would close over the loop's own variable and jump to '['.
                var tab = letter;

                buttons.Add(new KeypadButton(tab.ToString()) {Action = () => JumpTo(tab), EnabledWhen = () => _deck.HasBehind(tab)});
            }

            buttons.Add(new KeypadButton(Card.OtherLetter.ToString())
            {
                Action = () => JumpTo(Card.OtherLetter),
                EnabledWhen = () => _deck.HasBehind(Card.OtherLetter)
            });

            _tabs = new Keypad(new KeypadRow(buttons.ToArray()))
            {
                // One column per letter, which is what makes twenty-seven tabs fit a terminal at all.
                ButtonWidth = 1,
                Row = CardChrome.TabRow,
                Column = CardChrome.TabColumn,
                BorderStyle = DosTheme.Frame,
                ButtonStyle = DosTheme.Header,
                HoverStyle = DosTheme.MenuHighlight,

                // A tab with nothing behind it is greyed rather than hidden, which is what a real card index does:
                // the alphabet stays put and the empty letters simply do not open.
                DisabledStyle = DosTheme.MenuDisabled
            };
        }

        /// <summary>Builds the pull-downs.</summary>
        private void BuildMenus()
        {
            _menuBar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("Open...", OpenCards, "F3"),
                    new MenuBarEntry("Save", SaveCards, "F4") {EnabledWhen = () => _deck.IsModified},
                    new MenuBarEntry("Save As...", () => SaveCardsAs()),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Exit", () => ParentWindow.ClearForm(), "Esc")),
                new MenuBarMenu("Edit",
                    new MenuBarEntry("Edit Field...", EditField, "F2") {EnabledWhen = () => _deck.Count > 0},
                    new MenuBarEntry("Clear Field", ClearField) {EnabledWhen = CanClear},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("New Card...", NewCard, "F7"),
                    new MenuBarEntry("Delete Card...", DeleteCard, "Del") {EnabledWhen = () => _deck.Count > 0},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Find...", FindCard, "F9") {EnabledWhen = () => _deck.Count > 0}),
                new MenuBarMenu("View",
                    new MenuBarEntry("Card", () => ShowView(CardViewEnum.Card), "F5")
                        {CheckedWhen = () => _view == CardViewEnum.Card},
                    new MenuBarEntry("List", () => ShowView(CardViewEnum.List), "F6")
                        {CheckedWhen = () => _view == CardViewEnum.List},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Columns...", ChooseColumns),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("First Card", () => GoTo(0), "Home"),
                    new MenuBarEntry("Last Card", () => GoTo(_deck.Count - 1), "End")),
                new MenuBarMenu("Help",
                    new MenuBarEntry("About", ShowAbout)) {AlignRight = true})
            {
                BarStyle = DosTheme.MenuBar,
                HighlightStyle = DosTheme.MenuHighlight,
                PanelStyle = DosTheme.MenuPanel,
                PanelHighlightStyle = DosTheme.MenuHighlight,
                DisabledStyle = DosTheme.MenuDisabled,
                CheckMark = '√',
                BarRow = CardChrome.BarRow,
                PanelRow = CardChrome.TabRow
            };
        }

        /// <summary>Says what this is.</summary>
        private void ShowAbout()
        {
            _message = "WolfCurses card file - it reads its own file by column name, never by position.";
        }

        /// <summary>What is notched into the left of the box's top edge.</summary>
        /// <returns>The file's name, marked when there is anything unsaved.</returns>
        private string Heading()
        {
            return _deck.IsModified ? FileName + " *" : FileName;
        }

        /// <summary>
        ///     The key-hint strip, or whatever the last action had to say.
        ///     <para>
        ///         The name is shortened rather than the hints, which is the opposite of what the spreadsheet does
        ///         with its cell reference and right for the opposite reason: whose card this is, is written in
        ///         full two rows above, so the strip losing the end of a long name costs nothing, where losing the
        ///         end of the hints costs the only mention F10 gets.
        ///     </para>
        /// </summary>
        /// <returns>The status text.</returns>
        private string StatusText()
        {
            var where = _deck.Count == 0 ? "No cards" : Short(_deck.Cards[_selected].Name);

            if (!string.IsNullOrEmpty(_message))
                return "  " + where + "   " + _message;

            return "  " + where + "   TAB=View  F2=Edit  F7=New  F9=Find  F10=Menu";
        }

        /// <summary>A name cut to what the status strip can spare for it.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The name, at most <see cref="StatusName" /> columns of it.</returns>
        private static string Short(string name)
        {
            return name.Length <= StatusName ? name : name.Substring(0, StatusName - 1) + '…';
        }
    }
}
