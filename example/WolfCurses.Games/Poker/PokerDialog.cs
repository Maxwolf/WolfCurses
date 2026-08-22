// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Games.Cards;
using WolfCurses.Graphics;
using WolfCurses.Window;
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

        private readonly CardImages _images = new();

        private PokerGame _game;
        private CardTableArt _art;
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

            ParentWindow.PromptText = "1-5 hold, SPACE draw, ENTER deal again, TAB pictures, ESC to leave";

            _game = new PokerGame(SimUnit.Random);
            _art = new CardTableArt(_images);
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

        /// <summary>How many rows the table may claim, once the chrome and the paytable have had theirs.</summary>
        private static int TableRows()
        {
            return Math.Max(6, AnsiConsole.SafeWindowHeight() - 12);
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

            if (_useText || !_art.IsAvailable)
                body.AppendLine(CardTableText.Render(table));
            else
                body.AppendLine(CardView.Render(_art, TableRows(), table));

            body.AppendLine(HoldMarkers());
            body.AppendLine();
            body.Append(_game.Message);
            return body.ToString();
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
