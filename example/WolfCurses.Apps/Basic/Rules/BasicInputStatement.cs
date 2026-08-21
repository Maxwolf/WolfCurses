// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     INPUT, which asks and then puts what it got somewhere.
    ///     <para>
    ///         Several variables on one INPUT are filled from one comma separated answer, which is what BASIC did.
    ///         A numeric variable given something that is not a number takes zero rather than stopping the program,
    ///         because stopping on a typo is not what a BASIC program expects.
    ///     </para>
    /// </summary>
    public sealed class BasicInputStatement : BasicStatement
    {
        /// <summary>What to ask.</summary>
        private readonly string _prompt;

        /// <summary>Where the answers go.</summary>
        private readonly IReadOnlyList<BasicTarget> _targets;

        /// <summary>Initializes a new instance of the <see cref="BasicInputStatement" /> class.</summary>
        /// <param name="prompt">What to ask.</param>
        /// <param name="targets">Where the answers go.</param>
        /// <param name="line">The source line.</param>
        public BasicInputStatement(string prompt, IReadOnlyList<BasicTarget> targets, int line) : base(line)
        {
            _prompt = prompt;
            _targets = targets;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var answer = runtime.Host.ReadLine(_prompt) ?? string.Empty;
            var parts = answer.Split(',');

            for (var i = 0; i < _targets.Count; i++)
            {
                var part = i < parts.Length ? parts[i].Trim() : string.Empty;

                var value = _targets[i].IsString
                    ? new BasicValue(part)
                    : new BasicValue(double.TryParse(part, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var number)
                        ? number
                        : 0d);

                _targets[i].Assign(runtime, value, Line);
            }

            return index + 1;
        }
    }
}
