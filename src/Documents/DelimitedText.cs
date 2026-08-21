// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Text;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     Reads and writes comma separated values, and the other delimiters that share its rules: tabs, semicolons,
    ///     pipes.
    ///     <para>
    ///         <b>Here because everybody writes this and almost everybody writes it as
    ///         <c>line.Split(',')</c>.</b> That version is correct on the file you tested it against and wrong on
    ///         the first real one, because a field may be quoted, a quoted field may contain the delimiter, a quote,
    ///         or a line break, and a doubled quote inside one means a single literal quote. None of that is
    ///         expressible line by line at all: a record and a line are different things the moment a field
    ///         contains a newline, which is why this takes the whole text rather than one line.
    ///     </para>
    ///     <para>
    ///         Pure text, no console, no filesystem, exactly like <see cref="TextSearch" /> and
    ///         <see cref="TextWords" /> beside it. The library already decodes three image formats on the same
    ///         reasoning: a file format everybody needs and nobody wants to take a dependency for belongs in the
    ///         box.
    ///     </para>
    ///     <para>
    ///         <b>Reading is deliberately lenient and never throws.</b> A parser for data somebody else produced has
    ///         no useful way to refuse: a quote in the middle of an unquoted field is a literal quote, text after a
    ///         closing quote is appended to it, and an unterminated quoted field runs to the end of the text. Real
    ///         exports contain all three, and the alternative is a spreadsheet that cannot open a file rather than
    ///         one that opens it slightly oddly. Nothing is trimmed either, since a leading space may be data.
    ///     </para>
    /// </summary>
    public static class DelimitedText
    {
        /// <summary>The delimiter meant by the C in CSV.</summary>
        public const char DefaultDelimiter = ',';

        /// <summary>The character that quotes a field, and which doubled inside one means itself.</summary>
        private const char Quote = '"';

        /// <summary>
        ///     Splits delimited text into rows of fields.
        ///     <para>
        ///         <b>Rows are left ragged.</b> A row with fewer fields than its neighbours keeps fewer, because
        ///         only the caller knows whether the missing ones are empty cells, a short record or a fault. Real
        ///         files are ragged constantly, most often by a trailing blank line that is a row of one empty
        ///         field.
        ///     </para>
        /// </summary>
        /// <param name="text">The whole document. Null and empty both give no rows at all.</param>
        /// <param name="delimiter">What separates fields; a comma unless said otherwise.</param>
        /// <returns>The rows, each a list of fields.</returns>
        public static IReadOnlyList<IReadOnlyList<string>> Read(string text, char delimiter = DefaultDelimiter)
        {
            var rows = new List<IReadOnlyList<string>>();

            if (string.IsNullOrEmpty(text))
                return rows;

            var fields = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var i = 0;

            while (i < text.Length)
            {
                var character = text[i];

                if (quoted)
                {
                    if (character == Quote)
                    {
                        // A doubled quote inside a quoted field is one literal quote; a single one ends the field.
                        if (i + 1 < text.Length && text[i + 1] == Quote)
                        {
                            field.Append(Quote);
                            i += 2;
                            continue;
                        }

                        quoted = false;
                        i++;
                        continue;
                    }

                    // Delimiters and line breaks are ordinary characters in here, which is the whole reason quoting
                    // exists and the reason a record cannot be found by splitting on newlines first.
                    field.Append(character);
                    i++;
                    continue;
                }

                if (character == delimiter)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    i++;
                    continue;
                }

                if (character == '\r' || character == '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();

                    rows.Add(fields.ToArray());
                    fields.Clear();

                    // CRLF is one separator rather than two, or every row would be followed by an empty one.
                    i += character == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                    continue;
                }

                if (character == Quote && field.Length == 0)
                {
                    quoted = true;
                    i++;
                    continue;
                }

                // A quote anywhere else in an unquoted field is just a quote. Inches and minutes are written that
                // way and refusing them would be refusing the file.
                field.Append(character);
                i++;
            }

            // Whatever is still in hand is the last row, unless the text ended on a separator and there is nothing
            // in hand at all: a file conventionally ends with a line break and that must not invent a row.
            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
            }

            return rows;
        }

        /// <summary>
        ///     Turns rows of fields back into delimited text, quoting whatever has to be quoted.
        ///     <para>
        ///         <b>The result ends with a row separator</b>, which is what a text file conventionally carries and
        ///         also what makes reading back what was written give the same rows: without it, a document whose
        ///         last row is a single empty field is indistinguishable from a document with no rows.
        ///     </para>
        /// </summary>
        /// <param name="rows">The rows to write. A null row is skipped; a null field is written as empty.</param>
        /// <param name="delimiter">What to separate fields with.</param>
        /// <param name="newLine">
        ///     What to separate rows with. Defaults to this machine's line ending. A caller that read a file and
        ///     means to write it back should pass the ending it arrived with, for the same reason
        ///     <see cref="TextBuffer" /> remembers one rather than normalizing it.
        /// </param>
        /// <returns>The delimited text.</returns>
        public static string Write(IEnumerable<IEnumerable<string>> rows, char delimiter = DefaultDelimiter,
            string newLine = null)
        {
            if (rows == null)
                return string.Empty;

            newLine ??= Environment.NewLine;

            var sb = new StringBuilder();

            foreach (var row in rows)
            {
                if (row == null)
                    continue;

                var first = true;

                foreach (var field in row)
                {
                    if (!first)
                        sb.Append(delimiter);

                    Append(sb, field, delimiter);
                    first = false;
                }

                sb.Append(newLine);
            }

            return sb.ToString();
        }

        /// <summary>Writes one field, in quotes when it contains anything that would otherwise be read as structure.</summary>
        /// <param name="sb">Where the field goes.</param>
        /// <param name="field">The field itself.</param>
        /// <param name="delimiter">What separates fields, which is one of the things that forces quoting.</param>
        private static void Append(StringBuilder sb, string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field))
                return;

            if (field.IndexOf(delimiter) < 0 && field.IndexOf(Quote) < 0 && field.IndexOf('\r') < 0 &&
                field.IndexOf('\n') < 0)
            {
                sb.Append(field);
                return;
            }

            sb.Append(Quote);

            foreach (var character in field)
            {
                // Doubled, which is how the format says a quote inside a quoted field, and the reason reading has
                // to look one character ahead.
                if (character == Quote)
                    sb.Append(Quote);

                sb.Append(character);
            }

            sb.Append(Quote);
        }
    }
}
