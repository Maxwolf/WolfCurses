// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using WolfCurses.Window;

namespace WolfCurses.Games
{
    /// <summary>
    ///     State shared between the arcade menu and the games attached to it. A window's data object outlives its
    ///     forms, which is exactly what a high score wants to be: the game that set it is long gone by the time the
    ///     menu prints it.
    /// </summary>
    public sealed class GamesWindowInfo : WindowData
    {
        /// <summary>Longest snake the player has managed this session, in food eaten.</summary>
        public int SnakeHighScore { get; set; }

        /// <summary>How many minefields have been cleared without hitting anything this session.</summary>
        public int MinefieldsCleared { get; set; }

        /// <summary>Most rows cleared in a single well of tetris this session.</summary>
        public int TetrisBestLines { get; set; }

        /// <summary>How many games of chess the player has won this session.</summary>
        public int ChessWins { get; set; }

        /// <summary>Highest score reached in a single game of Missile Command this session.</summary>
        public int MissileCommandBestScore { get; set; }

        /// <summary>How many mazes the player has found their way out of this session.</summary>
        public int LabyrinthMazesEscaped { get; set; }

        /// <summary>Highest score reached in a single game of Pac-Man this session.</summary>
        public int PacManHighScore { get; set; }

        /// <summary>Biggest pile of chips the player has held at the blackjack table this session.</summary>
        public int BlackjackBestChips { get; set; }

        /// <summary>Biggest pile of chips the player has held at the poker table this session.</summary>
        public int PokerBestChips { get; set; }
    }
}
