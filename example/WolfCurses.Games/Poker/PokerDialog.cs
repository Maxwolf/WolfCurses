// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Games.Cards;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Poker
{
    /// <summary>
    ///     Five-card draw against a paytable.
    ///     <para>
    ///         <b>The second of two games sharing one deck</b>, and the point of the pair: nothing about a card, a
    ///         deck, a shuffle or how a hand is drawn appears in this folder or in blackjack's. All of it is in
    ///         <see cref="Cards" />, and the two games differ only in their rules — which is what you would want if
    ///         you added a third.
    ///     </para>
    ///     <para>
    ///         Held cards are marked <i>under</i> the hand rather than by changing the card, because a card's own
    ///         appearance is a fact about the card and holding is a fact about this game. It also means the marker
    ///         works identically over a picture and over letters, which is the only way one line of code can label
    ///         both.
    ///     </para>
    ///     <para>
    ///         <b>The paytable is drawn with the library's <see cref="FieldList" /></b>, which is what a record of
    ///         labelled fields is for and is exactly the shape of a paytable: a column of hand names with what each
    ///         is worth beside them, and the one the player just made picked out. Three of that control's rules are
    ///         load-bearing here. Its rows are a reservation rather than a maximum, so the block is the same height
    ///         every hand and nothing under it moves as the game is played. Its label column is measured once for
    ///         the whole list, so the numbers line up in a single column instead of one per hand name. And its
    ///         selection starts at -1 because a list only being read has nothing to point at, which is precisely
    ///         the state a hand still being chosen is in.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class PokerDialog : Form<GamesWindowInfo>
    {
        /// <summary>
        ///     Columns held for the chip count, matching the blackjack table's field so the two screens read the
        ///     same way. Five digits is far past any sitting somebody will actually have at one keypress a hand.
        /// </summary>
        private const int ChipColumns = 5;

        /// <summary>Columns held for the hand counter; one hand is one deal, so four digits outlives the session.</summary>
        private const int HandColumns = 4;

        /// <summary>
        ///     Columns held for a paytable label, which is the width of the two longest names there are ("Three of
        ///     a kind" and "Jacks or better"). Pinned rather than measured so that rewording a row cannot shunt the
        ///     whole column of numbers sideways, the same fixed-field discipline the chip line above it follows.
        /// </summary>
        private const int PaytableLabelColumns = 15;

        /// <summary>
        ///     Columns held for a payout. Four, because the best hand in the game pays 250 times a 25 stake and
        ///     6250 is as wide as this column ever gets.
        /// </summary>
        private const int PayoutColumns = 4;

        /// <summary>
        ///     Rows the screen spends on everything that is neither the hand nor the paytable: the scene graph's
        ///     status line, the chip line, the blank under it, the hold markers, the blank above the message, the
        ///     message, and the blank the scene graph leaves after it.
        ///     <para>
        ///         The echoed prompt is <b>not</b> among them, and that is worth knowing before anybody counts
        ///         these by eye against a screenshot: a form whose <see cref="InputFillsBuffer" /> is false leaves
        ///         <c>WindowManager.AcceptingInput</c> false, so the scene graph draws no prompt line at all and
        ///         this screen has one row more than the arithmetic of a typed screen would suggest.
        ///     </para>
        /// </summary>
        private const int ChromeRows = 7;

        /// <summary>
        ///     The paying hands, best first, which is the order every paytable ever printed runs in and also the
        ///     order <see cref="PokerCategoryEnum" /> counts down in.
        /// </summary>
        private static readonly PokerCategoryEnum[] _paytableOrder =
        {
            PokerCategoryEnum.RoyalFlush,
            PokerCategoryEnum.StraightFlush,
            PokerCategoryEnum.FourOfAKind,
            PokerCategoryEnum.FullHouse,
            PokerCategoryEnum.Flush,
            PokerCategoryEnum.Straight,
            PokerCategoryEnum.ThreeOfAKind,
            PokerCategoryEnum.TwoPair,
            PokerCategoryEnum.Pair
        };

        /// <summary>
        ///     What each of those hands is worth, as a multiple of the stake, in the same order.
        ///     <para>
        ///         <b>A second copy of the numbers in <c>PokerGame.Payout</c>, which is private.</b> Nothing would
        ///         stop compiling if the two drifted; the screen would simply promise chips the table does not pay,
        ///         which is the one lie a paytable must not tell. What holds them together is a test playing every
        ///         one of these hands through the real game and comparing what it paid against what this says.
        ///     </para>
        /// </summary>
        private static readonly int[] _paytableMultiples = {250, 50, 25, 9, 6, 4, 3, 2, 1};

        /// <summary>
        ///     Rows the paytable block claims: one per paying hand plus its caption. Read off the list rather than
        ///     written down again, so the rows the screen reserves and the rows it draws cannot disagree.
        /// </summary>
        private static readonly int _paytableRows = _paytableOrder.Length + 1;

        private readonly CardImages _images = new();

        private PokerGame _game;
        private CardTableArt _art;
        private FieldList _paytable;
        private string _rendered;
        private bool _useText;

        /// <summary>Initializes a new instance of the <see cref="PokerDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public PokerDialog(IWindow window) : base(window)
        {
        }

        /// <summary>Keeps the digits used to hold cards out of the echoed prompt.</summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            _game = new PokerGame(SimUnit.Random);
            _art = new CardTableArt(_images);

            // Built once and kept, not rebuilt per frame: the label column is measured off the entries, so a list
            // thrown away and remade every hand is a column that could quietly change width mid-session.
            _paytable = BuildPaytable();

            _useText = !_art.IsAvailable || !CardView.PictureIsLegible(TableRows());

            // Before the first draw, so the session's best reads as the pile the player starts with
            // rather than as zero until they touch a key.
            Record();
            _rendered = Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            switch (key)
            {
                case >= ConsoleKey.D1 and <= ConsoleKey.D5:
                    _game.ToggleHold(key - ConsoleKey.D1);
                    break;
                case >= ConsoleKey.NumPad1 and <= ConsoleKey.NumPad5:
                    _game.ToggleHold(key - ConsoleKey.NumPad1);
                    break;
                case ConsoleKey.Spacebar:
                    _game.Draw();
                    break;
                case ConsoleKey.D:
                    if (!_game.IsChoosing)
                        _game.Deal();

                    break;
                case ConsoleKey.Tab:
                    if (_useText || _art.IsAvailable)
                        _useText = !_useText;

                    break;
                default:
                    return;
            }

            Record();
            _rendered = Compose();
        }

        /// <summary>
        ///     ENTER, which at this table means "again" rather than "done" — the same binding blackjack next door
        ///     uses, and for the same reason: play happens in hands, so the key hit by reflex between them must not
        ///     be the one that ends the session. Leaving is ESC, and only ESC.
        /// </summary>
        /// <param name="input">The finished line, which is always empty — nothing here fills the buffer.</param>
        public override void OnInputBufferReturned(string input)
        {
            // Mid-hand ENTER does nothing at all. Dealing over a hand the player is still choosing holds on would
            // throw those cards away for a keypress that meant nothing - and would take another stake for it.
            if (_game.IsChoosing)
                return;

            if (_game.IsBroke)
                _game = new PokerGame(SimUnit.Random);
            else
                _game.Deal();

            Record();
            _rendered = Compose();
        }

        /// <summary>Keeps the session's best pile, which outlives this form because the window's data does.</summary>
        private void Record()
        {
            if (_game.BestChips > UserData.PokerBestChips)
                UserData.PokerBestChips = _game.BestChips;
        }

        /// <summary>
        ///     How many rows the table may claim, once the chrome and the paytable have had theirs.
        ///     <para>
        ///         The subtraction is seven rows of chrome, ten of paytable and caption, one for the row the
        ///         presenter clips off the bottom, and four of slack, which are the same four the bare twelve here
        ///         used to carry. Giving the paytable its ten is what stops a picture and a paytable both being
        ///         drawn onto a screen with room for one of them, and it moves the size a half-block terminal needs
        ///         before the picture is worth showing from about fifty-six rows to about sixty-six. That is not a
        ///         regression to tune away: fewer rows for the table really does mean a smaller picture, and a
        ///         corner index that has stopped being readable is the whole thing
        ///         <see cref="CardView.PictureIsLegible" /> exists to refuse.
        ///     </para>
        /// </summary>
        private static int TableRows()
        {
            return Math.Max(6, AnsiConsole.SafeWindowHeight() - (ChromeRows + _paytableRows + 5));
        }

        /// <summary>
        ///     Whether the terminal has the rows for the paytable once the hand has taken its own.
        ///     <para>
        ///         Asked in this direction, hand first and paytable second, because the other way round is circular:
        ///         how tall the picture is drawn decides whether the paytable fits, and letting the paytable's
        ///         presence decide how tall the picture is drawn closes the loop. So the hand keeps what
        ///         <see cref="TableRows" /> gave it and the paytable is dropped when what is left will not hold it.
        ///     </para>
        ///     <para>
        ///         As the numbers stand that is a terminal under twenty-one rows and nothing else, because
        ///         <see cref="TableRows" /> already took the paytable's rows off the top: a window too short to
        ///         hold both is also too short for the picture to have been offered enough rows to be legible, so
        ///         the hand falls back to letters and its three rows leave the block room. That is worth stating
        ///         rather than leaving to be rediscovered, since the branch is otherwise unreachable and reads like
        ///         dead code.
        ///     </para>
        /// </summary>
        /// <param name="tableRows">How many rows the drawn hand occupies.</param>
        /// <returns>Whether to draw the paytable at all.</returns>
        private static bool HasRoomForThePaytable(int tableRows)
        {
            // Height less one, because ConsolePresenter clips every frame to height-1 and keeps that row for the
            // input prompt whether or not the screen in front of it draws one.
            return AnsiConsole.SafeWindowHeight() - 1 >= tableRows + ChromeRows + _paytableRows;
        }

        /// <summary>
        ///     The jacks-or-better paytable as a list of labelled fields.
        /// </summary>
        /// <returns>The list, with nothing selected.</returns>
        internal static FieldList BuildPaytable()
        {
            var entries = new FieldListEntry[_paytableOrder.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                // Padded left by hand, because FieldList fits a value to the LEFT of its column and a column of
                // prices that does not line up on its units is one you have to read twice. Padding is the wrong
                // tool wherever the text is already styled, since it counts escape bytes as columns, and the right
                // one here: this text is plain and the control puts the colour on afterwards.
                entries[i] = new FieldListEntry(PaytableLabel(_paytableOrder[i]),
                    (_paytableMultiples[i]*PokerGame.BetSize).ToString(CultureInfo.InvariantCulture)
                    .PadLeft(PayoutColumns));
            }

            return new FieldList(entries)
            {
                Width = PaytableLabelColumns + 1 + PayoutColumns,
                MinimumLabelWidth = PaytableLabelColumns,
                LabelStyle = new TextStyle(ConsoleColor.DarkGray),
                ValueStyle = new TextStyle(ConsoleColor.Gray),

                // The same yellow the HELD markers wear, so the two things on this screen that are true of the
                // hand in front of the player right now are told apart from the furniture by the same colour.
                SelectedStyle = new TextStyle(ConsoleColor.Yellow, bold: true)
            };
        }

        /// <summary>
        ///     Which paytable row a finished hand made, or -1 for none.
        ///     <para>
        ///         Keyed off what the hand actually <b>paid</b> rather than off its category, and that is the whole
        ///         of the rule: a pair of fives is a <see cref="PokerCategoryEnum.Pair" /> and pays nothing, so
        ///         pointing at the "Jacks or better" row for it would be the screen claiming chips the player never
        ///         got. A hand still being chosen has paid nothing either, so it lands on the same -1, which is
        ///         where <see cref="FieldList.Selected" /> starts for exactly that reason.
        ///     </para>
        /// </summary>
        /// <param name="category">What the hand made.</param>
        /// <param name="payout">What it paid, which is zero for everything that did not.</param>
        /// <returns>The row index, or -1.</returns>
        internal static int PaytableRowFor(PokerCategoryEnum category, int payout)
        {
            return payout > 0 ? Array.IndexOf(_paytableOrder, category) : -1;
        }

        /// <summary>
        ///     What a paytable row is called, which is the category's own name for every row but one.
        ///     <para>
        ///         A pair pays only from jacks up, so a row labelled "Pair" would promise chips for a pair of
        ///         fives. That rule is the one the game is named after, and this is the only place a player is
        ///         actually told it.
        ///     </para>
        /// </summary>
        /// <param name="category">The paying hand.</param>
        /// <returns>Its name on the table.</returns>
        private static string PaytableLabel(PokerCategoryEnum category)
        {
            return category == PokerCategoryEnum.Pair ? "Jacks or better" : PokerHand.Describe(category);
        }

        /// <summary>Draws the heading, the hand with its hold markers, and the paytable.</summary>
        private string Compose()
        {
            var body = new StringBuilder();
            body.AppendLine();
            // Fixed-width fields, the same fix and the same reasoning as the blackjack table next door: the chip
            // count moves on every hand, across the three- and four-digit boundaries as a stake is won or lost,
            // and this line sits directly above five cards whose alignment is the point of the screen. Unpadded,
            // "Bet" and "Hands" step sideways with every deal. "Best" is left ragged deliberately: it is last on
            // the line and has nothing to shove.
            //
            // A plain composite-format width rather than AnsiText.Fit, because nothing on this line is styled.
            // Fit is what to reach for when the text carries escapes; here there are none to miscount, and a
            // plain width is simpler and can only ever pad, so a value that outgrew its field would degrade to
            // today's shuffle rather than losing a digit.
            body.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"Chips {_game.Chips,-ChipColumns}    Bet {PokerGame.BetSize}    " +
                $"Hands {_game.HandsPlayed,-HandColumns}    Best {UserData.PokerBestChips}"));
            body.AppendLine();

            var table = _game.Table();
            var tableRows = CardTableText.CardRows;

            if (_useText || !_art.IsAvailable)
            {
                body.AppendLine(CardTableText.Render(table));
            }
            else
            {
                // The picture claims every row TableRows offered whether or not the fit used them all, which is
                // the safe direction to be wrong in when the answer decides whether anything else gets drawn.
                tableRows = TableRows();
                body.AppendLine(CardView.Render(_art, tableRows, table));
            }

            body.AppendLine(HoldMarkers());
            AppendPaytable(body, tableRows);
            body.AppendLine();
            body.Append(_game.Message);

            // Drawn here rather than set as the window's prompt, which is the row this screen suppressed. See
            // GameControls for why that row never reaches the screen and why this one is free.
            GameControls.Append(body, "1-5 hold, SPACE draw, ENTER deal again, TAB pictures, ESC to leave");
            return body.ToString();
        }

        /// <summary>
        ///     Adds the paytable under the hand, when there are rows for it.
        ///     <para>
        ///         Under the hand and above the message rather than in a column beside the cards, and that is
        ///         forced rather than chosen: a true-pixel picture is one payload row that
        ///         <see cref="AnsiGraphics.IsPayloadRow" /> identifies by a marker at index zero, so nothing may
        ///         ever sit beside one. A layout that only worked while the terminal drew letters would be a screen
        ///         that comes apart on the terminals the pictures are for.
        ///     </para>
        /// </summary>
        /// <param name="body">The frame being built.</param>
        /// <param name="tableRows">How many rows the drawn hand occupies.</param>
        private void AppendPaytable(StringBuilder body, int tableRows)
        {
            if (!HasRoomForThePaytable(tableRows))
                return;

            // The caption takes the place of the blank line that used to separate the hand from the message, so
            // the block costs its nine rows and one more rather than eleven. It also says what the column of
            // numbers is, which "6250" beside "Royal flush" does not on its own.
            body.AppendLine(new TextStyle(ConsoleColor.DarkGray).Apply(
                string.Create(CultureInfo.InvariantCulture, $"Pays at {PokerGame.BetSize} a hand:")));

            _paytable.Selected = PaytableRowFor(_game.Category, _game.LastPayout);

            foreach (var row in _paytable.Render())
                body.AppendLine(row);
        }

        /// <summary>
        ///     The row of HELD labels under the hand.
        ///     <para>
        ///         Spaced to the text layout's card width whichever view is up. Under the picture the columns will
        ///         not line up exactly — the picture is scaled to fit the terminal and this is not — but the labels
        ///         are numbered, and a number the player can count is worth more than an alignment that only holds
        ///         at one window size.
        ///     </para>
        /// </summary>
        private string HoldMarkers()
        {
            var row = new StringBuilder();

            for (var i = 0; i < PokerHand.HandSize; i++)
            {
                if (i > 0)
                    row.Append(' ');

                var label = _game.IsHeld(i) ? "HELD" : (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var style = _game.IsHeld(i)
                    ? new TextStyle(ConsoleColor.Yellow, bold: true)
                    : new TextStyle(ConsoleColor.DarkGray);

                row.Append(style.Apply(label.PadRight(CardTableText.CardColumns)));
            }

            return row.ToString();
        }
    }
}
