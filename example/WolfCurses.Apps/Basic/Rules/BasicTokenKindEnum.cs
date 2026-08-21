// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     What a piece of BASIC source turned out to be.
    ///     <para>
    ///         <b>There is no Keyword kind, deliberately.</b> BASIC keywords are not reserved words: a program may
    ///         have a variable called <c>END</c>-something, <c>NAME</c> is both a statement and a perfectly ordinary
    ///         variable, and whether <c>TO</c> is a keyword depends entirely on whether a <c>FOR</c> is being read.
    ///         Deciding that in the lexer means the lexer has to know the grammar. So every word comes out as
    ///         <see cref="Word" /> carrying its text, and the parser, which does know what it is expecting, matches
    ///         on that.
    ///     </para>
    /// </summary>
    public enum BasicTokenKindEnum
    {
        /// <summary>Nothing left.</summary>
        EndOfFile = 0,

        /// <summary>The end of a statement: a real line break, or a colon separating statements on one line.</summary>
        EndOfLine = 1,

        /// <summary>A number, already converted.</summary>
        Number = 2,

        /// <summary>A quoted string, already unquoted.</summary>
        String = 3,

        /// <summary>A word: a keyword, a variable, a function name, or a label. The parser decides which.</summary>
        Word = 4,

        /// <summary>Punctuation or an operator.</summary>
        Symbol = 5
    }
}
