// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     Everything in the planner: what somebody typed in, and the holidays worked out around it. No console
    ///     anywhere in here, so all of it can be driven from a test.
    ///     <para>
    ///         <b>The two kinds are kept apart and only ever meet when a day is asked about.</b> Holidays are
    ///         computed per year and stored nowhere, so they cannot be edited, cannot be saved, and cannot go stale
    ///         when the calendar is paged into a year nobody thought about. What is in the file is only what a
    ///         person put there.
    ///     </para>
    /// </summary>
    public sealed class PlannerDiary
    {
        /// <summary>What somebody typed in.</summary>
        private readonly List<PlannerEvent> _events = new();

        /// <summary>What somebody typed in, in the order it was added.</summary>
        public IReadOnlyList<PlannerEvent> Events => _events;

        /// <summary>Whether anything has changed since the planner was loaded or last saved.</summary>
        public bool IsModified { get; private set; }

        /// <summary>
        ///     The line ending the file arrived with, written back out unchanged, the same stance the spreadsheet
        ///     and the text buffer take: opening a file and saving it untouched should give the same bytes.
        /// </summary>
        public string NewLine { get; set; } = Environment.NewLine;

        /// <summary>Adds an entry.</summary>
        /// <param name="entry">The entry. Holidays are refused, since nothing would ever store one.</param>
        public void Add(PlannerEvent entry)
        {
            if (entry == null || entry.Kind == PlannerEventKindEnum.Holiday ||
                string.IsNullOrWhiteSpace(entry.Title))
                return;

            _events.Add(entry);
            IsModified = true;
        }

        /// <summary>Removes an entry.</summary>
        /// <param name="entry">The entry to remove.</param>
        /// <returns>TRUE when it was there to remove.</returns>
        public bool Remove(PlannerEvent entry)
        {
            if (entry == null || !_events.Remove(entry))
                return false;

            IsModified = true;
            return true;
        }

        /// <summary>Empties the planner of everything a person put in it.</summary>
        public void Clear()
        {
            _events.Clear();
            IsModified = true;
        }

        /// <summary>Says the planner matches what is on disk, which is what saving it makes true.</summary>
        public void MarkSaved()
        {
            IsModified = false;
        }

        /// <summary>
        ///     Everything happening on a day, holidays included, in the order it should be read.
        ///     <para>
        ///         Sorted by time with the untimed entries first, because something that takes the whole day is not
        ///         at midnight and putting it there would say it was. Holidays lead, since they are what the day
        ///         <i>is</i> rather than something on it.
        ///     </para>
        /// </summary>
        /// <param name="date">The day to ask about.</param>
        /// <returns>The entries.</returns>
        public IReadOnlyList<PlannerEvent> On(DateOnly date)
        {
            var found = new List<PlannerEvent>();

            foreach (var holiday in Holidays.For(date.Year))
            {
                if (holiday.FallsOn(date))
                    found.Add(holiday);
            }

            var personal = new List<PlannerEvent>();

            foreach (var entry in _events)
            {
                if (entry.FallsOn(date))
                    personal.Add(entry);
            }

            personal.Sort((left, right) =>
                string.CompareOrdinal(Key(left.Time), Key(right.Time)));

            found.AddRange(personal);

            return found;
        }

        /// <summary>Whether anything at all happens on a day, which is what the calendar marks its cells by.</summary>
        /// <param name="date">The day to ask about.</param>
        /// <returns>TRUE when something does.</returns>
        public bool HasAnythingOn(DateOnly date)
        {
            foreach (var entry in _events)
            {
                if (entry.FallsOn(date))
                    return true;
            }

            foreach (var holiday in Holidays.For(date.Year))
            {
                if (holiday.FallsOn(date))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     What a time sorts by. An empty time sorts first rather than as midnight, which is the difference
        ///     between "all day" and "at 00:00" and is the only thing about this that is not obvious.
        /// </summary>
        /// <param name="time">The time as it was typed.</param>
        /// <returns>Its sort key.</returns>
        private static string Key(string time)
        {
            return string.IsNullOrEmpty(time) ? string.Empty : "1" + time;
        }
    }
}
