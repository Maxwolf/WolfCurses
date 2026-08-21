// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Where a running program keeps everything: its variables, its arrays, its random numbers and the screen it
    ///     is talking to.
    /// </summary>
    public sealed class BasicRuntime
    {
        /// <summary>The dimensioned arrays.</summary>
        private readonly Dictionary<string, BasicArray> _arrays = new(StringComparer.Ordinal);

        /// <summary>The plain variables.</summary>
        private readonly Dictionary<string, BasicValue> _variables = new(StringComparer.Ordinal);

        /// <summary>Initializes a new instance of the <see cref="BasicRuntime" /> class.</summary>
        /// <param name="host">The screen the program talks to.</param>
        /// <param name="seed">A fixed seed, so a test can predict RND; null asks for an unpredictable one.</param>
        public BasicRuntime(IBasicHost host, int? seed = null)
        {
            Host = host;
            Random = seed.HasValue ? new Random(seed.Value) : new Random();
            StartedAt = DateTime.UtcNow;
        }

        /// <summary>The screen the program talks to.</summary>
        public IBasicHost Host { get; }

        /// <summary>
        ///     Where each GOSUB should come back to. A stack rather than one slot because subroutines nest, and a
        ///     BASIC that kept only the last return address would work for every example and fail on real code.
        /// </summary>
        public Stack<int> ReturnAddresses { get; } = new();

        /// <summary>
        ///     The values the open SELECT CASE constructs are testing, innermost on top. A stack for the same
        ///     reason the loops are one: a SELECT inside a SELECT has to test its own value.
        /// </summary>
        public Stack<BasicValue> SelectValues { get; } = new();

        /// <summary>
        ///     The FOR loops currently running, innermost on top. A stack is what lets a bare NEXT know which loop
        ///     it belongs to, which is the whole reason BASIC allows one to be written without a variable.
        /// </summary>
        public Stack<BasicLoopFrame> Loops { get; } = new();

        /// <summary>The random source, which RANDOMIZE replaces.</summary>
        public Random Random { get; private set; }

        /// <summary>The last number RND produced, which RND with a zero argument hands back again.</summary>
        public double LastRandom { get; set; }

        /// <summary>When the program started, which is what TIMER counts from.</summary>
        public DateTime StartedAt { get; }

        /// <summary>Whether a name means a string, which in BASIC is a fact about the name itself.</summary>
        /// <param name="name">The variable name.</param>
        /// <returns>TRUE when it is a string variable.</returns>
        public static bool IsStringName(string name)
        {
            return !string.IsNullOrEmpty(name) && name[name.Length - 1] == '$';
        }

        /// <summary>
        ///     Reads a variable. An unset one reads as zero or as the empty string rather than failing, which is
        ///     BASIC and which programs rely on.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>Its value.</returns>
        public BasicValue Read(string name)
        {
            if (_variables.TryGetValue(name, out var value))
                return value;

            return IsStringName(name) ? BasicValue.EmptyString : BasicValue.Zero;
        }

        /// <summary>Writes a variable, refusing a value of the wrong kind for the name.</summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The value.</param>
        /// <param name="line">The line to blame.</param>
        public void Write(string name, BasicValue value, int line)
        {
            if (IsStringName(name) != value.IsString)
                throw new BasicError("Type mismatch assigning to " + name, line);

            _variables[name] = value;
        }

        /// <summary>Whether a name has been dimensioned as an array.</summary>
        /// <param name="name">The name.</param>
        /// <returns>TRUE when it is an array.</returns>
        public bool IsArray(string name)
        {
            return _arrays.ContainsKey(name);
        }

        /// <summary>Dimensions an array, replacing any previous one of that name.</summary>
        /// <param name="name">The name.</param>
        /// <param name="upperBounds">The highest valid subscript on each dimension.</param>
        /// <param name="line">The line to blame.</param>
        public void Dimension(string name, int[] upperBounds, int line)
        {
            foreach (var bound in upperBounds)
            {
                if (bound < 0)
                    throw new BasicError("Subscript out of range", line);
            }

            _arrays[name] = new BasicArray(upperBounds, IsStringName(name));
        }

        /// <summary>Reads an array element.</summary>
        /// <param name="name">The array name.</param>
        /// <param name="subscripts">The subscripts.</param>
        /// <param name="line">The line to blame.</param>
        /// <returns>The element.</returns>
        public BasicValue ReadElement(string name, int[] subscripts, int line)
        {
            if (!_arrays.TryGetValue(name, out var array))
                throw new BasicError("Array " + name + " has not been dimensioned", line);

            return array.Read(subscripts, line);
        }

        /// <summary>Writes an array element.</summary>
        /// <param name="name">The array name.</param>
        /// <param name="subscripts">The subscripts.</param>
        /// <param name="value">The value.</param>
        /// <param name="line">The line to blame.</param>
        public void WriteElement(string name, int[] subscripts, BasicValue value, int line)
        {
            if (!_arrays.TryGetValue(name, out var array))
                throw new BasicError("Array " + name + " has not been dimensioned", line);

            if (IsStringName(name) != value.IsString)
                throw new BasicError("Type mismatch assigning to " + name, line);

            array.Write(subscripts, value, line);
        }

        /// <summary>Reseeds the random source, which is what RANDOMIZE does.</summary>
        /// <param name="seed">The seed.</param>
        public void Reseed(int seed)
        {
            Random = new Random(seed);
        }
    }
}
