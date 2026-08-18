// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@2:38 PM

using System;
using System.Collections.Generic;

namespace WolfCurses.Core
{
    /// <summary>
    ///     Used for rolling the virtual dice in the simulation to determine the outcome of various events.
    /// </summary>
    public sealed class Randomizer : Module.Module
    {
        /// <summary>
        ///     Game logic objects.
        /// </summary>
        private Random _random;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Randomizer" /> class, seeding from the current system tick.
        ///     The auto-generated seed is exposed via <see cref="RandomSeed" /> so a session can be recorded and later
        ///     replayed by passing it back to <see cref="Randomizer(int)" />.
        /// </summary>
        public Randomizer()
        {
            // Create a unique random seed based on current system tick.
            RandomSeed = (int) DateTime.Now.Ticks & 0x0000FFF;
            _random = new Random(RandomSeed);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Randomizer" /> class with an explicit seed, making the
        ///     sequence deterministic and reproducible across runs.
        /// </summary>
        /// <param name="seed">Seed for the underlying random number generator.</param>
        public Randomizer(int seed)
        {
            RandomSeed = seed;
            _random = new Random(seed);
        }

        /// <summary>
        ///     Number used to seed the random number generator. Record it to reproduce a session later via
        ///     <see cref="Randomizer(int)" />.
        /// </summary>
        public int RandomSeed { get; }

        /// <summary>
        ///     Fired when the simulation is closing and needs to clear out any data structures that it created so the program can
        ///     exit cleanly.
        /// </summary>
        public override void Destroy()
        {
            _random = null;
        }

        /// <summary>
        ///     C64 style RND with 0 would return clock timer 0 - 60 number so we do the same here for simulation.
        /// </summary>
        /// <returns>
        ///     The <see cref="int" />.
        /// </returns>
        public int Next()
        {
            return _random.Next(60);
        }

        /// <summary>Returns a random number within a specified range.</summary>
        /// <returns>
        ///     A 32-bit signed integer greater than or equal to <paramref name="minValue" /> and less than
        ///     <paramref name="maxValue" />; that is, the range of return values includes <paramref name="minValue" /> but not
        ///     <paramref name="maxValue" />. If <paramref name="minValue" /> equals <paramref name="maxValue" />,
        ///     <paramref name="minValue" /> is returned.
        /// </returns>
        /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
        /// <param name="maxValue">
        ///     The exclusive upper bound of the random number returned. <paramref name="maxValue" /> must be
        ///     greater than or equal to <paramref name="minValue" />.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     <paramref name="minValue" /> is greater than
        ///     <paramref name="maxValue" />.
        /// </exception>
        public int Next(int minValue, int maxValue)
        {
            return _random.Next(minValue, maxValue);
        }

        /// <summary>Returns a nonnegative random number less than the specified maximum.</summary>
        /// <returns>
        ///     A 32-bit signed integer greater than or equal to zero, and less than <paramref name="maxValue" />; that is, the
        ///     range of return values ordinarily includes zero but not <paramref name="maxValue" />. However, if
        ///     <paramref name="maxValue" /> equals zero, <paramref name="maxValue" /> is returned.
        /// </returns>
        /// <param name="maxValue">
        ///     The exclusive upper bound of the random number to be generated. <paramref name="maxValue" />
        ///     must be greater than or equal to zero.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="maxValue" /> is less than zero.</exception>
        public int Next(int maxValue)
        {
            return _random.Next(maxValue);
        }

        /// <summary>
        ///     Returns a random number between 0.0 and 1.0.
        /// </summary>
        /// <returns>
        ///     A double-precision floating point number greater than or equal to 0.0, and less than 1.0.
        /// </returns>
        public double NextDouble()
        {
            return _random.NextDouble();
        }

        /// <summary>
        ///     Returns a random Boolean value.
        /// </summary>
        /// <returns>
        ///     The <see cref="bool" />.
        /// </returns>
        public bool NextBool()
        {
            return _random.Next(100)%2 == 0;
        }

        /// <summary>
        ///     Shuffles a list into a random order, in place, so that every ordering is equally likely.
        ///     <para>
        ///         <b>This is a Fisher-Yates shuffle, and the reason it is here rather than in each caller is that
        ///         the obvious hand-written version is subtly wrong.</b> Walking the list and swapping each item with
        ///         a random index anywhere in the whole list — <c>Next(count)</c> rather than <c>Next(i + 1)</c> — is
        ///         the mistake almost everybody makes, and it does not produce a uniform shuffle: it can reach
        ///         n<sup>n</sup> equally-likely outcomes for n! orderings, which do not divide, so some orders come
        ///         up more often than others. It still <i>looks</i> shuffled, which is why it survives review.
        ///     </para>
        ///     <para>
        ///         The loop below only ever picks from the part of the list it has not settled yet, which is the
        ///         whole difference. A list of nothing, or of one item, is left alone rather than refused.
        ///     </para>
        /// </summary>
        /// <typeparam name="T">What the list holds.</typeparam>
        /// <param name="items">The list to shuffle. Modified in place; null is ignored rather than thrown at.</param>
        public void Shuffle<T>(IList<T> items)
        {
            if (items == null)
                return;

            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}