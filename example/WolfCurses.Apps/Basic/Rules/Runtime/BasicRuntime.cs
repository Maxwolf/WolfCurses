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

        /// <summary>The plain variables belonging to the program itself.</summary>
        private readonly Dictionary<string, BasicValue> _variables = new(StringComparer.Ordinal);

        /// <summary>The procedures currently running, innermost on top.</summary>
        private readonly Stack<BasicScope> _scopes = new();

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

        /// <summary>
        ///     What PLAY remembers between one string and the next. On the runtime rather than in the parser
        ///     because programs set the tempo on one line and play the tune on another.
        /// </summary>
        public BasicMusicState Music { get; } = new();

        /// <summary>The random source, which RANDOMIZE replaces.</summary>
        public Random Random { get; private set; }

        /// <summary>The last number RND produced, which RND with a zero argument hands back again.</summary>
        public double LastRandom { get; set; }

        /// <summary>When the program started, which is what TIMER counts from.</summary>
        public DateTime StartedAt { get; }

        /// <summary>The procedure currently running, or null when the program itself is.</summary>
        public BasicScope CurrentScope => _scopes.Count > 0 ? _scopes.Peek() : null;

        /// <summary>The declared procedures, which the program hands over before it runs.</summary>
        public IReadOnlyDictionary<string, BasicProcedure> Procedures { get; set; }

        /// <summary>
        ///     The constants the program wrote in its DATA statements, in the order they appear in the source. Also
        ///     handed over by the program: they are gathered when it is parsed, because a READ near the top is
        ///     entitled to read DATA written at the bottom and every listing that uses DATA does exactly that.
        /// </summary>
        public IReadOnlyList<BasicValue> Data { get; set; } = new List<BasicValue>();

        /// <summary>How much of the DATA has been read.</summary>
        public int DataPointer { get; set; }

        /// <summary>How much DATA had been written by the time each label was reached, which is what RESTORE uses.</summary>
        public IReadOnlyDictionary<string, int> DataMarks { get; set; } = new Dictionary<string, int>(
            StringComparer.Ordinal);

        /// <summary>
        ///     The program being run, which a FUNCTION call in the middle of an expression needs in order to run
        ///     the body it is calling.
        /// </summary>
        public BasicProgram Program { get; set; }

        /// <summary>
        ///     How deeply calls may nest before it is called a mistake. A cap rather than nothing, because a
        ///     function that calls itself without a way out would otherwise take the whole process down with a stack
        ///     overflow, which cannot be caught and reported the way a BASIC error can.
        /// </summary>
        public const int MaxCallDepth = 128;

        /// <summary>The procedure of a given name, or null.</summary>
        /// <param name="name">The name, uppercased.</param>
        /// <returns>The procedure, or null.</returns>
        public BasicProcedure FindProcedure(string name)
        {
            return Procedures != null && Procedures.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>
        ///     Works the arguments out <b>before</b> the new scope exists, which is the whole of passing them: they
        ///     mean what they mean where they were written, not in the procedure about to receive them.
        /// </summary>
        /// <param name="procedure">What is being called.</param>
        /// <param name="arguments">The expressions written at the call.</param>
        /// <param name="runtime">The running program, still in the caller's scope.</param>
        /// <param name="line">The line to blame.</param>
        /// <returns>The values to bind.</returns>
        public static IReadOnlyList<BasicValue> Bind(BasicProcedure procedure,
            IReadOnlyList<BasicExpression> arguments, BasicRuntime runtime, int line)
        {
            if (arguments.Count > procedure.Parameters.Count)
                throw new BasicError("Too many arguments to " + procedure.Name, line);

            var values = new List<BasicValue>(arguments.Count);
            foreach (var argument in arguments)
                values.Add(argument.Evaluate(runtime));

            for (var i = 0; i < values.Count; i++)
            {
                // The parameter's own name says whether it is a string, exactly as a variable's does, so passing a
                // number where a name ends in a dollar is caught here rather than somewhere inside the body.
                if (IsStringName(procedure.Parameters[i]) != values[i].IsString)
                    throw new BasicError("Type mismatch in argument " + (i + 1) + " of " + procedure.Name, line);
            }

            return values;
        }

        /// <summary>Starts a procedure: fresh locals, with the arguments already bound into them.</summary>
        /// <param name="procedure">What is being called.</param>
        /// <param name="arguments">The values to bind.</param>
        /// <param name="returnTo">Where to carry on afterwards.</param>
        /// <param name="line">The line to blame.</param>
        /// <returns>The scope that was pushed.</returns>
        public BasicScope EnterProcedure(BasicProcedure procedure, IReadOnlyList<BasicValue> arguments, int returnTo,
            int line)
        {
            if (_scopes.Count >= MaxCallDepth)
                throw new BasicError("Too many nested calls to " + procedure.Name, line);

            var scope = new BasicScope(procedure, returnTo)
            {
                LoopDepth = Loops.Count,
                SelectDepth = SelectValues.Count
            };

            for (var i = 0; i < procedure.Parameters.Count; i++)
            {
                var name = procedure.Parameters[i];

                // A parameter the caller left out is not an error, it is simply unset, which is the same rule every
                // other BASIC variable follows.
                scope.Variables[name] = i < arguments.Count
                    ? arguments[i]
                    : IsStringName(name) ? BasicValue.EmptyString : BasicValue.Zero;
            }

            _scopes.Push(scope);
            return scope;
        }

        /// <summary>Finishes a procedure and throws its locals away.</summary>
        /// <returns>The scope that was popped.</returns>
        public BasicScope LeaveProcedure()
        {
            if (_scopes.Count == 0)
                return null;

            var scope = _scopes.Pop();

            // Anything the procedure left open goes with it. EXIT SUB out of a FOR loop is ordinary BASIC, and a
            // frame left behind would be stepped by the next NEXT anywhere in the program.
            while (Loops.Count > scope.LoopDepth)
                Loops.Pop();

            while (SelectValues.Count > scope.SelectDepth)
                SelectValues.Pop();

            return scope;
        }

        /// <summary>
        ///     Which set of variables a name means here: the procedure's own, or the program's when there is no
        ///     procedure running or the name has been SHARED out of it.
        /// </summary>
        private Dictionary<string, BasicValue> VariablesFor(string name)
        {
            var scope = CurrentScope;
            return scope == null || scope.IsShared(name) ? _variables : scope.Variables;
        }

        /// <summary>The same question for arrays, answered the same way.</summary>
        private Dictionary<string, BasicArray> ArraysFor(string name)
        {
            var scope = CurrentScope;
            return scope == null || scope.IsShared(name) ? _arrays : scope.Arrays;
        }

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
            if (VariablesFor(name).TryGetValue(name, out var value))
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

            VariablesFor(name)[name] = value;
        }

        /// <summary>Whether a name has been dimensioned as an array.</summary>
        /// <param name="name">The name.</param>
        /// <returns>TRUE when it is an array.</returns>
        public bool IsArray(string name)
        {
            return ArraysFor(name).ContainsKey(name);
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

            ArraysFor(name)[name] = new BasicArray(upperBounds, IsStringName(name));
        }

        /// <summary>Reads an array element.</summary>
        /// <param name="name">The array name.</param>
        /// <param name="subscripts">The subscripts.</param>
        /// <param name="line">The line to blame.</param>
        /// <returns>The element.</returns>
        public BasicValue ReadElement(string name, int[] subscripts, int line)
        {
            if (!ArraysFor(name).TryGetValue(name, out var array))
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
            if (!ArraysFor(name).TryGetValue(name, out var array))
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
