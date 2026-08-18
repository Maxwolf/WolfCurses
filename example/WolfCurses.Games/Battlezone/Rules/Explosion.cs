// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     A tank coming apart: a burst of fragments flying outward, drawn as lines because everything here is
    ///     drawn as lines.
    ///     <para>
    ///         <b>It carries a seed rather than a list of fragments</b>, which is the division of labour this game
    ///         keeps everywhere: the rules know that something blew up at a place and how long ago, the renderer
    ///         knows what an explosion looks like. Any number of fragments can then be drawn from one integer, the
    ///         same ones every frame, with nothing allocated and nothing about the picture leaking into a class that
    ///         is supposed to be testable without a console.
    ///     </para>
    /// </summary>
    public sealed class Explosion
    {
        /// <summary>How long the fragments fly before they are gone.</summary>
        public const double Life = 0.85;

        /// <summary>Initializes a new instance of the <see cref="Explosion" /> class.</summary>
        /// <param name="x">Where it happened, east.</param>
        /// <param name="z">Where it happened, north.</param>
        /// <param name="y">How far off the ground.</param>
        /// <param name="size">How big the thing that blew up was.</param>
        /// <param name="seed">Which explosion this is, so its fragments can be recomputed rather than stored.</param>
        public Explosion(double x, double z, double y, double size, int seed)
        {
            X = x;
            Z = z;
            Y = y;
            Size = size;
            Seed = seed;
        }

        /// <summary>Where it happened, east.</summary>
        public double X { get; }

        /// <summary>Where it happened, north.</summary>
        public double Z { get; }

        /// <summary>How far off the ground it happened.</summary>
        public double Y { get; }

        /// <summary>How big the thing that blew up was.</summary>
        public double Size { get; }

        /// <summary>Which explosion this is.</summary>
        public int Seed { get; }

        /// <summary>How long ago it happened.</summary>
        public double Age { get; internal set; }

        /// <summary>Whether there is anything left to draw.</summary>
        public bool Alive => Age < Life;

        /// <summary>How far through its life it is, from zero to one.</summary>
        public double Progress => Age/Life;
    }
}
