// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Not an error: the way a host says "I need a line of input and I cannot give you one yet".
    ///     <para>
    ///         <b>INPUT has to stop in the middle of a statement, and a screen cannot afford to stop at all.</b> A
    ///         BASIC program is run in bounded slices precisely so that one which loops forever does not take the
    ///         interface with it, so a host that blocked inside ReadLine waiting for a keystroke would freeze the
    ///         very screen the keystroke has to arrive through. Signalling out and coming back later is the only
    ///         shape that works.
    ///     </para>
    ///     <para>
    ///         <b>Coming back means running the whole statement again</b>, which is safe for exactly one reason:
    ///         asking is the first thing INPUT does, so nothing has happened yet to undo. <see cref="ResumeAt" />
    ///         carries where that statement was, because a statement knows its own position and the loop that ran it
    ///         does not survive the throw.
    ///     </para>
    /// </summary>
    public sealed class BasicInputRequest : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="BasicInputRequest" /> class.</summary>
        /// <param name="prompt">What to show the user.</param>
        public BasicInputRequest(string prompt) : base("The program is waiting for input")
        {
            Prompt = prompt ?? string.Empty;
            ResumeAt = -1;
        }

        /// <summary>Initializes a new instance of the <see cref="BasicInputRequest" /> class.</summary>
        public BasicInputRequest() : this(string.Empty)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="BasicInputRequest" /> class.</summary>
        /// <param name="message">What happened.</param>
        /// <param name="innerException">What caused it.</param>
        public BasicInputRequest(string message, Exception innerException) : base(message, innerException)
        {
            Prompt = string.Empty;
            ResumeAt = -1;
        }

        /// <summary>What to show the user.</summary>
        public string Prompt { get; }

        /// <summary>Which statement to run again once there is an answer.</summary>
        public int ResumeAt { get; set; }
    }
}
