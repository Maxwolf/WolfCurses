// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     SOUND: a pitch for a length of time.
    ///     <para>
    ///         The duration is written in clock ticks, of which there were 18.2 a second, and that number is not an
    ///         approximation to tidy up: a program that asks for 18 ticks means one second, and rounding the rate to
    ///         18 or 20 would make every tune it plays the wrong length.
    ///     </para>
    /// </summary>
    public sealed class BasicSoundStatement : BasicStatement
    {
        /// <summary>Ticks of the machine's clock in one second.</summary>
        private const double TicksPerSecond = 18.2;

        /// <summary>How long, in ticks.</summary>
        private readonly BasicExpression _duration;

        /// <summary>What pitch, in hertz.</summary>
        private readonly BasicExpression _frequency;

        /// <summary>Initializes a new instance of the <see cref="BasicSoundStatement" /> class.</summary>
        /// <param name="frequency">What pitch, in hertz.</param>
        /// <param name="duration">How long, in ticks.</param>
        /// <param name="line">The source line.</param>
        public BasicSoundStatement(BasicExpression frequency, BasicExpression duration, int line) : base(line)
        {
            _frequency = frequency;
            _duration = duration;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            var frequency = _frequency.Evaluate(runtime).AsNumber(Line);
            var ticks = _duration.Evaluate(runtime).AsNumber(Line);

            runtime.Host.Sound(Math.Max(0d, frequency), Math.Max(0d, ticks) / TicksPerSecond * 1000d);
            return index + 1;
        }
    }
}
