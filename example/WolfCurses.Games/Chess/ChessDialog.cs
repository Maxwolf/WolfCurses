// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     WolfChess 5000 — a joke about Battle Chess, and the game in this arcade that exercises the graphics
    ///     stack rather than avoiding it.
    ///     <para>
    ///         <b>The board is one picture, not sixty-four.</b> <see cref="ChessBoardArt" /> composites the squares
    ///         and the piece artwork into a single <c>PixelBuffer</c> and it goes through
    ///         <see cref="ImageRenderers.Default" /> once — sixel or kitty where the terminal has them, colored half
    ///         blocks where it does not. That is forced by the <see cref="AnsiGraphics" /> marker contract as much as
    ///         chosen: a true-pixel picture is one payload row that paints across many, recognised by the marker
    ///         being the row's <i>first</i> character, so <b>nothing may sit beside the board</b> — the status goes
    ///         above it and the move list below, never in a column to the right. TAB switches to
    ///         <see cref="ChessTextBoard" />, which is the honest answer on a terminal too small for a
    ///         forty-eight-pixel knight to survive being resampled into three rows.
    ///     </para>
    ///     <para>
    ///         <b>Both input styles at once</b>, which is the thing chess needs and neither Snake nor Minesweeper
    ///         demonstrates. The arrow keys steer a cursor and ENTER on an empty buffer picks a piece up and puts it
    ///         down — reusing the library's own "an empty submit acts for the highlight" rule. Typing works at the
    ///         same time and takes priority, because the buffer is left enabled: "e4", "Nf3" and "e2e4" all play,
    ///         and so do the words in <see cref="TryRunCommand" />. Nothing had to be added to make the two coexist;
    ///         an arrow key has no character so it never reaches the buffer, and that is the whole trick.
    ///     </para>
    ///     <para>
    ///         The bot is ticked rather than called. <see cref="IChessBot.Think" /> is handed a slice of each tick
    ///         and asked whether it is finished, so the screen keeps redrawing and the spinner keeps turning while
    ///         it searches — see that interface for why a chess search cannot simply be paused, and what iterative
    ///         deepening does about it.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class ChessDialog : Form<GamesWindowInfo>
    {
        /// <summary>
        ///     How much of each tick the bot may have. Small on purpose: the input manager reads no keys while a
        ///     <see cref="IChessBot.Think" /> call is running, so this is also how long ESC can go unanswered.
        /// </summary>
        private static readonly TimeSpan _thinkSlice = TimeSpan.FromMilliseconds(15);

        /// <summary>
        ///     How many plies the move list under the board shows. Twice what it showed while it was squeezed
        ///     onto the end of the status line, because it has the whole width to itself now.
        /// </summary>
        private const int RecentPlies = 8;

        private readonly ChessBoardArt _art = new();

        /// <summary>
        ///     Paces the "thinking…" readout.
        ///     <para>
        ///         <b>Without this the screen is rebuilt about a thousand times a second while the bot thinks</b>,
        ///         and rebuilding means recompositing 147,456 pixels and re-encoding them — for a readout whose
        ///         digits a human could not follow anyway. Worse, every rebuilt frame differs, so the presenter
        ///         counts it as changed, and its every-tenth-changed-frame full redraw re-emits the whole picture
        ///         payload — tens of megabytes a second at kitty's payload sizes. Four times a second is legible
        ///         and costs nothing.
        ///     </para>
        /// </summary>
        private readonly IntervalTimer _readout = new(TimeSpan.FromMilliseconds(250));

        private ChessGame _game;
        private IChessBot _bot;
        private IChessBot _adviser;
        private bool _dirty;
        private int _cursor = ChessBoard.SquareAt(4, 1);
        private int _selected = -1;
        private ChessMove _lastMove = ChessMove.None;
        private bool _flipped;
        private bool _useText;
        private bool _thinking;
        private int _level = 4;
        private string _message;
        private string _rendered;

        /// <summary>Initializes a new instance of the <see cref="ChessDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public ChessDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "Move (e4, Nf3, e2e4) or arrows+ENTER; \"help\" for commands, ESC for the menu";

            // The artwork missing is not fatal - it is what the text board is for - but it should be said once
            // rather than leaving the player wondering why a chess game with graphics has none.
            _useText = !_art.IsAvailable || !PictureIsLegible();
            RestartOnActivate(_readout);
            NewGame();
        }

        /// <summary>
        ///     Whether the picture board is worth showing at this terminal size, or whether letters would say more.
        ///     <para>
        ///         The threshold is much higher for half blocks than for real pixels, because half blocks get two
        ///         pixels per row: eight squares across the rows the board is given works out at under four pixels
        ///         a square on an eighty-by-twenty-four terminal, and a forty-eight pixel knight resampled to four
        ///         pixels is a smudge that is the same smudge as a bishop. Sixel and kitty draw a proper picture in
        ///         the same rows, so they only need enough of them to be worth looking at.
        ///     </para>
        /// </summary>
        private bool PictureIsLegible()
        {
            // Asked of the library rather than written out here: the colour mode matters because a picture is
            // nothing but colour, and virtual-terminal processing matters because without it the presenter writes a
            // payload row BLANK. This screen had only ever checked the first of the two, so on a console where VT
            // could not be enabled it drew an empty board and said nothing about why.
            if (!AnsiConsole.SupportsPictures())
                return false;

            // Asked of the renderer rather than type-tested against the built-in classes, which is what this line
            // used to do - and which gets the wrong answer for exactly the third-party renderers the seam exists
            // to allow.
            var rows = BoardRows();
            return ImageRenderers.Default.DrawsTruePixels ? rows >= 10 : rows >= 40;
        }

        /// <summary>How many rows the board may claim, after the chrome and the prompt have had theirs.</summary>
        private static int BoardRows() => Math.Max(6, AnsiConsole.SafeWindowHeight() - 9);

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (key == ConsoleKey.Tab)
            {
                _useText = !_useText;
                if (_useText || _art.IsAvailable)
                    Refresh();
                return;
            }

            var (dx, dy) = key switch
            {
                ConsoleKey.LeftArrow => (-1, 0),
                ConsoleKey.RightArrow => (1, 0),
                ConsoleKey.UpArrow => (0, 1),
                ConsoleKey.DownArrow => (0, -1),
                _ => (0, 0)
            };

            if (dx == 0 && dy == 0)
                return;

            // Turning the board around turns the arrow keys around with it, or "up" starts meaning "toward the
            // bottom of the screen", which no amount of explaining makes feel right.
            if (_flipped)
            {
                dx = -dx;
                dy = -dy;
            }

            var file = Math.Clamp(ChessBoard.FileOf(_cursor) + dx, 0, 7);
            var rank = Math.Clamp(ChessBoard.RankOf(_cursor) + dy, 0, 7);
            _cursor = ChessBoard.SquareAt(file, rank);

            // Marked rather than redrawn. The input manager spends every queued key in ONE tick, so a held arrow
            // arrives as a run of a dozen presses, and redrawing per press would recomposite and re-encode the
            // whole board a dozen times for twelve frames only the last of which is ever seen.
            _dirty = true;
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }

            AdviseIfAsked();

            if (!_thinking)
                return;

            if (!_bot.Think(_thinkSlice))
            {
                // Still searching. The readout is repainted a few times a second rather than every tick - see
                // _readout for why every tick is not merely wasteful but floods the console.
                if (_readout.TryConsume())
                    Refresh();

                return;
            }

            _thinking = false;
            var move = _bot.BestMove;

            if (!move.IsValid)
            {
                _message = "The bot could not find a move.";
                Refresh();
                return;
            }

            _lastMove = move;
            _message = $"{_bot.Name} played {_game.ToSan(move)}.";
            _game.Play(move);
            AnnounceIfOver();
            Refresh();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick, so it hands back a string that is already built. Composing and rendering
            // the board here instead would resample a 384x384 picture thousands of times a second.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            var typed = (input ?? string.Empty).Trim();

            if (typed.Length == 0)
            {
                PickUpOrPutDown();
                return;
            }

            if (TryRunCommand(typed))
                return;

            if (_game.IsOver)
            {
                _message = "The game is over. Type \"new\" to play again.";
                Refresh();
                return;
            }

            // Without this the player can type Black's reply while the bot is still working out Black's reply,
            // and then the search finishes and plays a second one from a position that has moved on. The arrow-key
            // path is already guarded inside PickUpOrPutDown; this is the typed half of the same rule.
            if (_thinking)
            {
                _message = "It is still thinking.";
                Refresh();
                return;
            }

            if (!_game.TryParseMove(typed, out var move))
            {
                _message = $"\"{typed}\" is not a legal move here.";
                Refresh();
                return;
            }

            PlayPlayerMove(move);
        }

        /// <summary>
        ///     ENTER with nothing typed: pick the piece under the cursor up, or put the held piece down here. The
        ///     second ENTER only plays if the square is a legal destination, so an accidental drop is a no-op rather
        ///     than an illegal move.
        /// </summary>
        private void PickUpOrPutDown()
        {
            if (_game.IsOver || _thinking)
                return;

            if (_selected < 0)
            {
                var piece = _game.Board[_cursor];
                if (!piece.Is(_game.Board.SideToMove))
                {
                    _message = piece.IsEmpty ? "Nothing there to move." : "That is not yours to move.";
                    Refresh();
                    return;
                }

                _selected = _cursor;
                _message = $"Holding the {piece.Kind.ToString().ToLowerInvariant()} on " +
                           $"{ChessBoard.SquareName(_cursor)}.";
                Refresh();
                return;
            }

            if (_cursor == _selected)
            {
                _selected = -1;
                _message = "Put it back.";
                Refresh();
                return;
            }

            foreach (var candidate in _game.LegalMoves)
            {
                if (candidate.From != _selected || candidate.To != _cursor)
                    continue;

                // Promotions arrive as four moves to the same square; the queen is what anyone steering with the
                // arrow keys means, and the other three stay reachable by typing "e8=N".
                if (candidate.Promotion != PieceKindEnum.None && candidate.Promotion != PieceKindEnum.Queen)
                    continue;

                PlayPlayerMove(candidate);
                return;
            }

            _message = $"{ChessBoard.SquareName(_selected)}-{ChessBoard.SquareName(_cursor)} is not legal.";
            Refresh();
        }

        /// <summary>Plays the player's move and sets the bot thinking about its reply.</summary>
        private void PlayPlayerMove(ChessMove move)
        {
            // A hint the player did not wait for is about a position that has just stopped existing. The
            // search runs sliced over more than a second of real time and nothing stops a move being played
            // during it, so without this the adviser finishes against a board two plies on and overwrites the
            // real message with a wrong one - visibly garbled rather than merely wrong, since ToSan reads the
            // piece off the FROM square and that square is empty when the player has just played the very move
            // being suggested. It reads "It would play .f3." NewGame already made this decision for the other
            // way of abandoning a hint.
            _adviser = null;
            _selected = -1;
            _lastMove = move;
            _message = $"You played {_game.ToSan(move)}.";
            _game.Play(move);

            if (AnnounceIfOver())
            {
                Refresh();
                return;
            }

            _bot.Begin(_game);
            _thinking = true;
            Refresh();
        }

        /// <summary>Sets the message and the scoreboard when the game has ended. Returns whether it has.</summary>
        private bool AnnounceIfOver()
        {
            if (!_game.IsOver)
                return false;

            _thinking = false;
            _message = _game.Result switch
            {
                ChessResultEnum.Checkmate => _game.Winner == PieceColorEnum.White
                    ? "Checkmate. You win - type \"new\" to play again."
                    : "Checkmate. WolfChess 5000 wins - type \"new\" to play again.",
                ChessResultEnum.Stalemate => "Stalemate: no legal move, and not in check. Drawn.",
                ChessResultEnum.FiftyMoveRule => "Fifty moves without a capture or a pawn move. Drawn.",
                ChessResultEnum.ThreefoldRepetition => "The same position three times. Drawn.",
                ChessResultEnum.InsufficientMaterial => "Neither side can force mate. Drawn.",
                ChessResultEnum.Resignation => "You resigned.",
                _ => _message
            };

            if (_game.Result == ChessResultEnum.Checkmate && _game.Winner == PieceColorEnum.White)
                UserData.ChessWins++;

            return true;
        }

        /// <summary>
        ///     The typed words that are not moves. Kept deliberately word-shaped rather than bound to keys, because
        ///     the input buffer is already open for moves and a letter key would have to be stolen from it.
        /// </summary>
        private bool TryRunCommand(string typed)
        {
            var parts = typed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0].ToLowerInvariant())
            {
                case "help":
                    _message = "new · flip · undo · level 2-5 · hint · text · resign";
                    break;
                case "new":
                    NewGame();
                    return true;
                case "flip":
                    _flipped = !_flipped;
                    _message = _flipped ? "Seen from Black's side." : "Seen from White's side.";
                    break;
                case "text":
                    _useText = !_useText;
                    _message = _useText ? "Text board." : "Picture board.";
                    break;
                case "level":
                    if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var level))
                    {
                        _level = Math.Clamp(level, 2, 5);

                        // Abandoning the search matters: replacing the bot mid-think leaves a fresh one that
                        // answers "finished" immediately with no move, and the game wedges with Black to move and
                        // nothing willing to move it. Whoever is to move gets asked again below.
                        var wasThinking = _thinking;
                        _thinking = false;
                        _bot = new WolfChessBot(_level);
                        _message = $"Difficulty {_level}. {_bot.Name}.";

                        if (wasThinking && !_game.IsOver)
                        {
                            _bot.Begin(_game);
                            _thinking = true;
                        }
                    }
                    else
                    {
                        _message = "Say which: \"level 4\".";
                    }

                    break;
                case "hint":
                    ShowHint();
                    break;
                case "resign":
                    _game.Resign(PieceColorEnum.White);
                    _message = "You resigned. Type \"new\" to play again.";
                    break;
                case "undo":
                    _message = "Undo needs a move history the board does not keep; start a new game instead.";
                    break;
                default:
                    return false;
            }

            Refresh();
            return true;
        }

        /// <summary>
        ///     Sets a second search going to suggest a move.
        ///     <para>
        ///         Sliced from <see cref="OnTick" /> exactly as the opponent is, and for the same reason. The first
        ///         version of this ran the whole search inside this method with
        ///         <c>while (!adviser.Think(200ms)) { }</c> — a full search inside an input handler, measured at
        ///         over a second in a Debug build, during which nothing is drawn and no key is read. It is the
        ///         precise anti-pattern the sliced interface exists to prevent, written by the same hand that built
        ///         the interface, which is worth leaving a note about.
        ///     </para>
        /// </summary>
        private void ShowHint()
        {
            if (_game.IsOver)
            {
                _message = "The game is over.";
                return;
            }

            if (_thinking)
            {
                _message = "Wait for it to finish its own move first.";
                return;
            }

            _adviser = new WolfChessBot(Math.Min(_level, 4));
            _adviser.Begin(_game);
            _message = "Thinking about what you should play…";
        }

        /// <summary>Advances the hint search, if one is running.</summary>
        private void AdviseIfAsked()
        {
            if (_adviser == null || !_adviser.Think(_thinkSlice))
                return;

            _message = _adviser.BestMove.IsValid
                ? $"It would play {_game.ToSan(_adviser.BestMove)}."
                : "It has no suggestion.";

            _adviser = null;
            Refresh();
        }

        /// <summary>Starts a fresh game with the player as White.</summary>
        private void NewGame()
        {
            _game = new ChessGame();
            _bot = new WolfChessBot(_level);
            _cursor = ChessBoard.SquareAt(4, 1);
            _selected = -1;
            _lastMove = ChessMove.None;
            _thinking = false;

            // A hint about the old game would arrive a few ticks into the new one and name a move that is not
            // legal in it.
            _adviser = null;
            _message = _art.IsAvailable
                ? "You are White. Move a piece to begin."
                : $"You are White. (No piece artwork found, so this is the text board: {_art.Error})";
            Refresh();
        }

        /// <summary>
        ///     Rebuilds the screen. Called when something changed, never from the render.
        ///     <para>
        ///         <b>Every row here is always present, including the empty ones.</b> The move list used to be a
        ///         second line of the status, conditional on there being any moves - so the whole board shifted
        ///         down exactly one row the moment White played. A player sees that as a jolt; on a sixel or
        ///         kitty terminal it is worse, because the payload row has moved, and the presenter then erases
        ///         the picture's area and repaints the whole payload rather than leaving an unchanged picture
        ///         alone. Putting the moves under the board also makes true what this class and the app notes
        ///         both already claimed: status above, move list below, never in a column beside it.
        ///     </para>
        /// </summary>
        private void Refresh()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(StatusLine());
            sb.AppendLine();
            sb.AppendLine(_useText || !_art.IsAvailable ? RenderTextBoard() : RenderPictureBoard());
            sb.AppendLine(RecentMoves());
            sb.Append(_message);
            _rendered = sb.ToString();
        }

        /// <summary>
        ///     The last few plies, drawn under the board on a row that is always there and is empty before the
        ///     first move.
        /// </summary>
        /// <returns>The moves, or an empty line.</returns>
        private string RecentMoves()
        {
            if (_game.Notation.Count == 0)
                return string.Empty;

            var recent = new StringBuilder("  ");
            var from = Math.Max(0, _game.Notation.Count - RecentPlies);

            for (var i = from; i < _game.Notation.Count; i++)
            {
                if (i % 2 == 0)
                    recent.Append(i / 2 + 1).Append('.');

                recent.Append(_game.Notation[i]).Append(' ');
            }

            return recent.ToString().TrimEnd();
        }

        /// <summary>The line above the board: whose turn, what the bot is doing, and the score. Always one line.</summary>
        private string StatusLine()
        {
            var turn = _game.IsOver
                ? _game.Result.ToString()
                : _thinking
                    ? $"thinking… depth {_bot.CompletedDepth}, {_bot.NodesSearched:N0} nodes"
                    : _game.Board.SideToMove == PieceColorEnum.White
                        ? "your move"
                        : "black to move";

            var check = !_game.IsOver && _game.IsInCheck ? "  CHECK" : string.Empty;
            return $"WolfChess 5000  ·  level {_level}  ·  {turn}{check}  ·  wins {UserData.ChessWins}";
        }

        /// <summary>Which squares to highlight, most important last so it wins the square.</summary>
        private Dictionary<int, ChessSquareMarkEnum> BuildMarks()
        {
            var marks = new Dictionary<int, ChessSquareMarkEnum>();

            void Mark(int square, ChessSquareMarkEnum mark)
            {
                if (square >= 0 && (!marks.TryGetValue(square, out var existing) || mark > existing))
                    marks[square] = mark;
            }

            if (_lastMove.IsValid)
            {
                Mark(_lastMove.From, ChessSquareMarkEnum.LastMove);
                Mark(_lastMove.To, ChessSquareMarkEnum.LastMove);
            }

            if (_selected >= 0)
            {
                foreach (var move in _game.LegalMoves)
                {
                    if (move.From == _selected)
                        Mark(move.To, ChessSquareMarkEnum.Target);
                }
            }

            if (!_game.IsOver && _game.IsInCheck)
                Mark(_game.Board.KingSquare(_game.Board.SideToMove), ChessSquareMarkEnum.Check);

            Mark(_selected, ChessSquareMarkEnum.Selected);
            Mark(_cursor, ChessSquareMarkEnum.Cursor);
            return marks;
        }

        /// <summary>Composites the board and renders it through whatever the terminal can do.</summary>
        private string RenderPictureBoard()
        {
            // Sized to whatever the terminal has left after the two status lines, the message and the prompt. Read
            // once into locals rather than per row - each of these is a syscall.
            var rows = Math.Max(8, AnsiConsole.SafeWindowHeight() - 9);
            var columns = Math.Max(16, AnsiConsole.SafeWindowWidth() - 2);

            var options = new AnsiImageOptions
            {
                MaxRows = rows,
                MaxColumns = columns,
                RowMargin = 0
            };

            var pixels = _art.Compose(_game.Board, _flipped, BuildMarks());
            return AnsiImage.FromPixels(pixels).ToAnsi(options);
        }

        /// <summary>Draws the board as characters.</summary>
        private string RenderTextBoard() => ChessTextBoard.Render(_game.Board, _flipped, BuildMarks());
    }
}
