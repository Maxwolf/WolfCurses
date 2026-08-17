// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Text;
using WolfCurses.Games.Chess;
using WolfCurses.Games.Labyrinth;
using WolfCurses.Games.Minesweeper;
using WolfCurses.Games.MissileCommand;
using WolfCurses.Games.PacMan;
using WolfCurses.Games.Snake;
using WolfCurses.Games.Tetris;
using WolfCurses.Window;

namespace WolfCurses.Games
{
    /// <summary>
    ///     The arcade menu, and the only window this application defines. Every game is a
    ///     <see cref="Window.Form.Form{T}" /> attached to it rather than a window of its own, which is what makes the
    ///     single ESC handler below cover all of them at once.
    /// </summary>
    public sealed class GamesWindow : Window<GamesCommandsEnum, GamesWindowInfo>
    {
        /// <summary>Initializes a new instance of the <see cref="GamesWindow" /> class.</summary>
        /// <param name="simUnit">Core simulation which is controlling the form factory.</param>
        // ReSharper disable once UnusedMember.Global
        public GamesWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        /// <summary>Called after the window has been added to the list of modes and made active.</summary>
        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();

            AddCommand(PlaySnake, GamesCommandsEnum.Snake);
            AddCommand(PlayMinesweeper, GamesCommandsEnum.Minesweeper);
            AddCommand(PlayTetris, GamesCommandsEnum.Tetris);
            AddCommand(PlayChess, GamesCommandsEnum.Chess);
            AddCommand(PlayMissileCommand, GamesCommandsEnum.MissileCommand);
            AddCommand(PlayLabyrinth, GamesCommandsEnum.Labyrinth);
            AddCommand(PlayPacMan, GamesCommandsEnum.PacMan);
            AddCommand(Quit, GamesCommandsEnum.Quit);

            RefreshMenuHeader();
        }

        /// <summary>Restores the menu's own prompt and scores whenever a game hands control back.</summary>
        protected override void OnFormChange()
        {
            base.OnFormChange();

            if (CurrentForm != null)
                return;

            RefreshMenuHeader();
            PromptText = "Which game?";
        }

        /// <summary>
        ///     ESC backs out of whichever game is showing and returns to the arcade menu. Every game here is a form on
        ///     this one window, so intercepting the key before the base class forwards it down backs all of them out
        ///     from a single place — the same trick WolfCurses.Example uses, and the reason no game has to handle ESC.
        ///     <para>
        ///         The library's own modal controls are separate windows this override never sees, so a
        ///         <see cref="Controls.MessageBox" /> a game puts up is dismissed on its own terms, not by this.
        ///     </para>
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        public override void OnKeyPressed(ConsoleKey key)
        {
            if (key == ConsoleKey.Escape && CurrentForm != null)
            {
                ClearForm();
                return;
            }

            base.OnKeyPressed(key);
        }

        /// <summary>Rebuilds the header, whose scoreboard is the visible proof that window data outlives its forms.</summary>
        private void RefreshMenuHeader()
        {
            var header = new StringBuilder();
            header.AppendLine();
            header.AppendLine($"Snake best: {UserData.SnakeHighScore}   " +
                              $"Minefields cleared: {UserData.MinefieldsCleared}   " +
                              $"Tetris best: {UserData.TetrisBestLines} rows");
            header.AppendLine($"Chess wins: {UserData.ChessWins}   " +
                              $"Missile Command best: {UserData.MissileCommandBestScore:N0}   " +
                              $"Mazes escaped: {UserData.LabyrinthMazesEscaped}   " +
                              $"Pac-Man best: {UserData.PacManHighScore:N0}");
            header.AppendLine();
            header.Append("Choose one (arrow keys + ENTER, or type a number):");
            MenuHeader = header.ToString();
        }

        private void PlaySnake()
        {
            SetForm(typeof (SnakeDialog));
        }

        private void PlayMinesweeper()
        {
            SetForm(typeof (MinesweeperDialog));
        }

        private void PlayTetris()
        {
            SetForm(typeof (TetrisDialog));
        }

        private void PlayChess()
        {
            SetForm(typeof (ChessDialog));
        }

        private void PlayMissileCommand()
        {
            SetForm(typeof (MissileCommandDialog));
        }

        private void PlayLabyrinth()
        {
            SetForm(typeof (LabyrinthDialog));
        }

        private void PlayPacMan()
        {
            SetForm(typeof (PacManDialog));
        }

        private void Quit()
        {
            Program.Destroy();
        }
    }
}
