// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Reads the little language PLAY takes: letters for notes, with octaves, lengths, dots and a tempo.
    ///     <para>
    ///         Pure text in, a list of pitches and durations out, so the whole of it is unit tested without anything
    ///         that makes a noise. <b>The arithmetic is the part worth testing</b>: a semitone is a factor of the
    ///         twelfth root of two, and getting the reference wrong puts every note in the wrong place while still
    ///         sounding like music.
    ///     </para>
    /// </summary>
    public static class BasicMusic
    {
        /// <summary>Where each note of an octave sits, in semitones above its C.</summary>
        private static readonly int[] _semitones = {9, 11, 0, 2, 4, 5, 7};

        /// <summary>
        ///     Turns a PLAY string into notes, updating the state it was given.
        ///     <para>
        ///         A frequency of zero is a rest, which is how P is expressed without a second kind of note.
        ///     </para>
        /// </summary>
        /// <param name="music">The PLAY string.</param>
        /// <param name="state">What PLAY remembers; updated as the string is read.</param>
        /// <param name="line">The line to blame.</param>
        /// <returns>The notes, each a frequency in hertz and a duration in milliseconds.</returns>
        public static IReadOnlyList<(double Frequency, double Milliseconds)> Parse(string music,
            BasicMusicState state, int line = 0)
        {
            var notes = new List<(double, double)>();
            if (string.IsNullOrEmpty(music))
                return notes;

            var at = 0;

            while (at < music.Length)
            {
                var command = char.ToUpperInvariant(music[at]);
                at++;

                switch (command)
                {
                    case ' ':
                        continue;
                    case '>':
                        state.Octave = Math.Min(6, state.Octave + 1);
                        continue;
                    case '<':
                        state.Octave = Math.Max(0, state.Octave - 1);
                        continue;
                    case 'O':
                        state.Octave = Math.Clamp(Number(music, ref at, state.Octave), 0, 6);
                        continue;
                    case 'L':
                        state.Length = Math.Clamp(Number(music, ref at, state.Length), 1, 64);
                        continue;
                    case 'T':
                        state.Tempo = Math.Clamp(Number(music, ref at, state.Tempo), 32, 255);
                        continue;
                    case 'M':

                        // The articulation commands say how notes join up, which nothing here can express, so they
                        // are read and dropped rather than being an error in somebody's tune.
                        if (at < music.Length)
                            at++;

                        continue;
                    case 'P':
                    case 'R':
                        notes.Add((0d, Duration(Number(music, ref at, state.Length), music, ref at, state)));
                        continue;
                    case 'N':
                        var number = Number(music, ref at, 0);
                        notes.Add((number <= 0 ? 0d : Pitch(number + 11), Duration(state.Length, music, ref at,
                            state)));
                        continue;
                }

                if (command is < 'A' or > 'G')
                    throw new BasicError("PLAY does not understand " + command, line);

                var semitone = _semitones[command - 'A'];

                // A sharp may be written either way round, and a flat only one way, which is what the listings do.
                while (at < music.Length && (music[at] is '#' or '+' or '-'))
                {
                    semitone += music[at] == '-' ? -1 : 1;
                    at++;
                }

                var length = Number(music, ref at, state.Length);
                notes.Add((Pitch(state.Octave * 12 + semitone), Duration(length, music, ref at, state)));
            }

            return notes;
        }

        /// <summary>
        ///     The frequency of a note counted in semitones from the C of octave zero. Four hundred and forty hertz
        ///     is the A above middle C, which is note 57 counted this way, and every other note is that scaled by
        ///     the twelfth root of two.
        /// </summary>
        /// <param name="note">Semitones above the C of octave zero.</param>
        /// <returns>The frequency in hertz.</returns>
        public static double Pitch(int note)
        {
            return 440d * Math.Pow(2d, (note - 57) / 12d);
        }

        /// <summary>How long a note of a given length lasts, dots included.</summary>
        private static double Duration(int length, string music, ref int at, BasicMusicState state)
        {
            if (length < 1)
                length = state.Length;

            var whole = 4d * 60000d / state.Tempo;
            var milliseconds = whole / length;

            // A dot adds half of what there was, and dots stack, which is how a listing writes a note and a half.
            var added = milliseconds;
            while (at < music.Length && music[at] == '.')
            {
                added /= 2d;
                milliseconds += added;
                at++;
            }

            return milliseconds;
        }

        /// <summary>Reads the digits after a command, or hands back what was already in force.</summary>
        private static int Number(string music, ref int at, int fallback)
        {
            var start = at;
            while (at < music.Length && char.IsDigit(music[at]))
                at++;

            return at == start
                ? fallback
                : int.Parse(music.Substring(start, at - start), CultureInfo.InvariantCulture);
        }
    }
}
