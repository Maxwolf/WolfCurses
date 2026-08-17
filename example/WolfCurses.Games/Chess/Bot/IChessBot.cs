// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Something that picks a move. Shaped for a single-threaded tick loop, which is the only thing about it
    ///     that is unusual.
    ///     <para>
    ///         <b>The interface is incremental because a chess search is not.</b> The obvious signature —
    ///         <c>ChessMove ChooseMove(game)</c> — blocks until it has an answer, and in this library that means the
    ///         scene graph does not run, so the screen freezes and the spinner stops for however long the bot
    ///         thinks. Handing the search a budget and calling it every tick keeps the interface alive and lets it
    ///         say "thinking, depth 4" while it works. The mechanism that makes this possible is <b>iterative
    ///         deepening</b>: the search is re-run from scratch at depth 1, then 2, then 3, and each run is short
    ///         enough to fit a tick. A recursive search cannot be paused in the middle and resumed — but it can be
    ///         thrown away and restarted one ply deeper, and that turns out to be what real engines do anyway,
    ///         because the shallow result orders the moves for the deeper one.
    ///     </para>
    ///     <para>
    ///         It is also the seam. The library's own <c>IImageDecoder</c> / <c>IImageRenderer</c> pattern is
    ///         "ship a working default, let anyone replace it", and this is the same: <see cref="WolfChessBot" /> is
    ///         the built-in, and anything that can answer these five members can take its place — including an
    ///         adapter that drives a real engine over UCI, which this repository cannot ship because it would mean
    ///         bundling somebody else's binary.
    ///     </para>
    /// </summary>
    public interface IChessBot
    {
        /// <summary>What to call it on screen.</summary>
        string Name { get; }

        /// <summary>The best move found so far. Valid once <see cref="CompletedDepth" /> is at least one.</summary>
        ChessMove BestMove { get; }

        /// <summary>How many plies deep the search has finished. Zero means it has not answered yet.</summary>
        int CompletedDepth { get; }

        /// <summary>How many positions have been looked at, for the readout.</summary>
        long NodesSearched { get; }

        /// <summary>Starts thinking about a position. Any previous thinking is abandoned.</summary>
        /// <param name="game">The game to move in. The bot must not modify it.</param>
        void Begin(ChessGame game);

        /// <summary>
        ///     Thinks for up to <paramref name="budget" /> and returns whether it is finished. A caller ticks this
        ///     until it returns true, then plays <see cref="BestMove" />.
        /// </summary>
        /// <param name="budget">How long this slice may take.</param>
        /// <returns>True when the bot has nothing more it wants to do.</returns>
        bool Think(TimeSpan budget);
    }
}
