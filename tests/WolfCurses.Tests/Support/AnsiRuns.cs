using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     Inspects the SGR runs in a widget's rendered output, so a test can assert something about the escape
    ///     sequences without asserting the exact bytes of a gradient nobody wants to write out by hand.
    ///     <para>
    ///         The reason this exists is a defect the byte-exact tests could never have caught. The cell-by-cell
    ///         widgets used to decide "is this a new run?" by comparing the <em>color</em> they were about to draw,
    ///         but colors are quantized on the way out: <see cref="WolfCurses.Graphics.AnsiColorModeEnum.Grayscale" />
    ///         has 26 possible sequences and <see cref="WolfCurses.Graphics.AnsiColorModeEnum.Palette256" /> has 256,
    ///         so dozens of distinct ramp colors reach the terminal as the identical <c>38;5;n</c>. The output looked
    ///         right — it <em>was</em> right — while carrying a reset and a re-open between neighbouring cells that
    ///         the terminal draws the same way, on strings that are rebuilt every frame. Counting redundant runs is
    ///         the only way to see that from the outside.
    ///     </para>
    /// </summary>
    internal static class AnsiRuns
    {
        /// <summary>The sequence that closes a run.</summary>
        private const string Reset = "\x1b[0m";

        /// <summary>Matches a plain SGR sequence, which is all these widgets are allowed to emit.</summary>
        private static readonly Regex _sgr = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

        /// <summary>
        ///     How many times the output closes a run and immediately re-opens a byte-identical one. Zero is the
        ///     contract: a run may only end where the sequence that reaches the terminal actually changes.
        ///     <para>
        ///         Counted per line, since closing a style before <see cref="Environment.NewLine" /> is a deliberate
        ///         invariant of its own (a style must never survive a line break) and would otherwise be miscounted
        ///         as redundancy. Only <em>adjacent</em> pairs count — a reset, then text, then the same open again
        ///         is two genuinely separate runs.
        ///     </para>
        /// </summary>
        /// <param name="rendered">A widget's rendered output.</param>
        /// <returns>The number of reset/re-open pairs that change nothing.</returns>
        public static int CountRedundantRuns(string rendered)
        {
            if (string.IsNullOrEmpty(rendered))
                return 0;

            var redundant = 0;
            foreach (var line in rendered.Split(Environment.NewLine))
            {
                var matches = _sgr.Matches(line);
                string open = null;

                for (var i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    if (!string.Equals(match.Value, Reset, StringComparison.Ordinal))
                    {
                        open = match.Value;
                        continue;
                    }

                    if (open != null && i + 1 < matches.Count &&
                        matches[i + 1].Index == match.Index + match.Length &&
                        string.Equals(matches[i + 1].Value, open, StringComparison.Ordinal))
                        redundant++;

                    open = null;
                }
            }

            return redundant;
        }

        /// <summary>Every escape sequence in the output, in the order it appears.</summary>
        /// <param name="rendered">A widget's rendered output.</param>
        public static IReadOnlyList<string> Escapes(string rendered)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(rendered))
                return found;

            foreach (Match match in _sgr.Matches(rendered))
                found.Add(match.Value);

            return found;
        }

        /// <summary>Strips every SGR sequence, leaving what the terminal would actually show.</summary>
        /// <param name="rendered">A widget's rendered output.</param>
        public static string Strip(string rendered)
        {
            return rendered == null ? null : _sgr.Replace(rendered, string.Empty);
        }
    }
}
