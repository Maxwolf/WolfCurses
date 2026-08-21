// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Something the program did wrong, at a line the user can go and look at.
    ///     <para>
    ///         <b>One exception type for every kind of failure, on purpose.</b> A BASIC program is read and run by
    ///         the same screen the user is typing it in, so the only thing the interpreter can usefully do about any
    ///         mistake is stop and say which line it was and what it did not like. Distinguishing a syntax error from
    ///         a division by zero in the type system would buy nothing, because there is exactly one handler.
    ///     </para>
    /// </summary>
    public sealed class BasicError : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="BasicError" /> class.</summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="line">The source line it happened on, counting from one; zero when there is not one.</param>
        public BasicError(string message, int line = 0) : base(Describe(message, line))
        {
            Line = line;
            Reason = message ?? string.Empty;
        }

        /// <summary>Initializes a new instance of the <see cref="BasicError" /> class.</summary>
        public BasicError() : this(string.Empty)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="BasicError" /> class.</summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">What caused it.</param>
        public BasicError(string message, Exception innerException) : base(message, innerException)
        {
            Reason = message ?? string.Empty;
        }

        /// <summary>The source line it happened on, or zero.</summary>
        public int Line { get; }

        /// <summary>What went wrong, without the line prefix.</summary>
        public string Reason { get; }

        /// <summary>Puts the line in front of the reason, which is how every BASIC has ever reported a fault.</summary>
        private static string Describe(string message, int line)
        {
            return line > 0
                ? string.Format(CultureInfo.InvariantCulture, "Line {0}: {1}", line, message)
                : message ?? string.Empty;
        }
    }
}
