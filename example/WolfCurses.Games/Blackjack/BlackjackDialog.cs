// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Games.Cards;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Blackjack
{
    /// <summary>
    ///     Blackjack against a dealer with no choices to make.
    ///     <para>
    ///         <b>The first of two games sharing one deck.</b> Everything about a card — what it is, what it is
    ///         called, which file its picture lives in, how a hand is drawn as a picture and as letters — is in
    ///         <see cref="Cards" />, and poker next door uses all of it unchanged. Only the rules are here, and they
    ///         are two small files.
    ///     </para>
    ///     <para>
    ///         <b>Picture or letters is decided the same way chess decides it</b>, and for a stronger reason: what
    ///         you need off a playing card is the rank and pip in its corner, which is the first thing to vanish when
    ///         the image is scaled. So a terminal that cannot draw real pixels gets <c>A♠</c> in a box, which loses
    ///         nothing at all. TAB switches, as it does in chess.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class BlackjackDialog : Form<GamesWindowInfo>
    {
        /// <summary>
        ///     Columns held for the chip count. Five, because the only things that ever move the pile are the 25
        ///     stake and the 37 a natural pays, so reaching five digits means winning several hundred rounds net at
        ///     one keypress a round. Note the field only ever <i>pads</i>: a pile that somehow got past it shoves the
        ///     rest of the line along exactly as it does today rather than being cut, which is the failure worth
        ///     avoiding of the two.
        /// </summary>
        private const int ChipColumns = 5;

        /// <summary>
        ///     Columns held for the round counter. One round is one keypress, so four digits outlives any sitting
        ///     somebody will actually have at this table.
        /// </summary>
        private const int RoundColumns = 4;

        private readonly CardImages _images = new();

        private BlackjackGame _game;
        private CardTableArt _art;
        private string _rendered;
        private bool _useText;

        /// <summary>Initializes a new instance of the <see cref="BlackjackDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public BlackjackDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Keeps typed characters out of the input buffer: the table is played with single keys, so an H would
        ///     otherwise pile up in the prompt while the player was hitting.
        /// </summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "H hit, S stand, ENTER deal again, TAB pictures, ESC to leave";

            _game = new BlackjackGame(SimUnit.Random);
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
            // Called every system tick, so it hands back a string that was built when something happened. Nothing
            // here moves on its own - the table changes only when a key is pressed.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            switch (key)
            {
                case ConsoleKey.H:
                    _game.Hit();
                    break;
                case ConsoleKey.S:
                    _game.Stand();
                    break;
                case ConsoleKey.D:
                    if (!_game.CanAct)
                        _game.Deal();

                    break;
                case ConsoleKey.Tab:
                    // TAB is not printable, so the input manager drops it before it can reach the prompt - the same
                    // reason the sprite demos bind their renderer switch to it.
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
        ///     ENTER, which at this table means "again" rather than "done".
        ///     <para>
        ///         <b>Leaving is ESC, and only ESC.</b> The obvious binding — ENTER quits, as it does in the snake and
        ///         the maze — is wrong here because a card table is played in rounds: the key you press most often is
        ///         the one that deals the next hand, and putting <i>quit</i> under the key a player hits by reflex
        ///         between rounds means one stray press ends the session. Every other game here ends when the game
        ///         does; this one ends when the player decides to stop.
        ///     </para>
        ///     <para>
        ///         Out of chips it deals a fresh stake instead, because a table with nothing left to bet has exactly
        ///         one useful thing ENTER could do and going quiet is not it. The session's best pile is already
        ///         recorded on the window's data, so starting over costs nothing that was worth keeping.
        ///     </para>
        /// </summary>
        /// <param name="input">The finished line, which is always empty — nothing here fills the buffer.</param>
        public override void OnInputBufferReturned(string input)
        {
            // Mid-hand ENTER does nothing at all. Dealing over a hand the player is still deciding would throw away
            // their cards for a keypress that meant nothing.
            if (_game.CanAct)
                return;

            if (_game.IsBroke)
                _game = new BlackjackGame(SimUnit.Random);
            else
                _game.Deal();

            Record();
            _rendered = Compose();
        }

        /// <summary>Keeps the session's best pile, which outlives this form because the window's data does.</summary>
        private void Record()
        {
            if (_game.BestChips > UserData.BlackjackBestChips)
                UserData.BlackjackBestChips = _game.BestChips;
        }

        /// <summary>How many rows the table may claim, once the chrome and the prompt have had theirs.</summary>
        private static int TableRows()
        {
            return Math.Max(6, AnsiConsole.SafeWindowHeight() - 10);
        }

        /// <summary>Draws the heading, the table, and whatever the last round had to say.</summary>
        private string Compose()
        {
            var body = new StringBuilder();
            body.AppendLine();
            // Fixed-width fields, and the chip count is the one that earns them: it moves on every single round, up
            // and down across the three- and four-digit boundaries as a stake is won or lost, and this line sits
            // directly on top of a table whose alignment is the whole point of the screen. Unpadded, "Bet", "Rounds"
            // and "Best" all step sideways with every deal. "Best" itself is deliberately left ragged - it is last on
            // the line and has nothing to shove.
            //
            // A plain composite-format width rather than AnsiText.Fit, because nothing on this line is styled: Fit
            // measures visible columns and is what to reach for when the text carries escapes, where PadRight would
            // count the SGR bytes as cells and under-pad a coloured run by hundreds of characters. Here there are no
            // escapes to miscount, and a plain width is both simpler and incapable of trimming a real value.
            body.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"Chips {_game.Chips,-ChipColumns}    Bet {BlackjackGame.BetSize}    " +
                $"Rounds {_game.RoundsPlayed,-RoundColumns}    Best {UserData.BlackjackBestChips}"));
            body.AppendLine();

            var dealer = _game.DealerTable();
            var player = _game.Player.ToTable();

            if (_useText || !_art.IsAvailable)
            {
                body.AppendLine($"Dealer  {(_game.CanAct ? _game.DealerShowing + " showing" : _game.Dealer.Value.ToString())}");
                body.AppendLine(CardTableText.Render(dealer));
                body.AppendLine();
                body.AppendLine($"You     {_game.Player.Value}{(_game.Player.IsSoft ? " (soft)" : string.Empty)}");
                body.AppendLine(CardTableText.Render(player));
            }
            else
            {
                body.AppendLine($"Dealer {(_game.CanAct ? _game.DealerShowing + " showing" : _game.Dealer.Value.ToString())}" +
                                $"   You {_game.Player.Value}{(_game.Player.IsSoft ? " (soft)" : string.Empty)}");
                body.AppendLine(CardView.Render(_art, TableRows(), dealer, player));
            }

            body.AppendLine();
            body.Append(_game.Message);
            return body.ToString();
        }
    }
}
