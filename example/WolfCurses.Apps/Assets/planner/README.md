# Sample planner

Copied next to the executable into a `planner/` folder, which is where the planner opens and where its Open
dialog starts. **Written for this repository**: it is Maxwolf's own calendar, which is mostly moulting, tail
maintenance and apologising to a coast guard, with the occasional city.

| File | What it is |
| --- | --- |
| `planner.csv` | Thirty-odd entries, some annual and some dated, alongside the holidays the program works out for itself |

## The format

Three columns: **Date**, **Time**, **What**. The header row is not special and nothing knows it is there; it is
skipped because its date does not parse, which is the same reason a blank line or a stray note in the file is
skipped.

The date is the only interesting part:

| Written | Means |
| --- | --- |
| `05-04` | The fourth of May, **every year** |
| `2026-08-21` | The twenty-first of August 2026, once |

The time may be left empty, which means the entry takes the whole day. An all-day entry sorts *before* the timed
ones rather than at midnight, because something taking the whole day is not at midnight and putting it there
would say it was.

## Why half of it is annual

So that it still means something in 2040. A file of fixed dates is interesting for as long as the year it was
written for, and a calendar you can page through is one somebody will page past the end of. The annual entries
are spread across all twelve months, so whichever month the planner opens on has something on it.

**A leap-day annual entry happens only in leap years.** There is no other day the twenty-ninth of February could
honestly be moved to, and quietly picking one would be the program inventing an anniversary. Nothing in this file
relies on that, but the rule is tested.

## The holidays are not in here

They are worked out from the year instead, which is why paging to 2099 still finds Easter in the right place.
That also means a holiday cannot be deleted from the planner: there is nothing stored to delete, and next year's
would be computed again anyway.

## The awkward rows are deliberate

Three of them exist to prove the reader is a real one:

- `"Groundhog Day: check own shadow, apologise to town"` contains the delimiter.
- `"Return library books (overdue: 3 years, 1 city)"` does as well, in the middle of a joke.
- `"Vet appointment (they still say ""good boy"")"` contains **both** a doubled quote and a delimiter, which is
  the shape that separates a real CSV reader from a call to `Split`.

## The joke

It is a macro calendar: the entries alternate between the very large and the very mundane, which is the whole of
the gag. A city gets destroyed on Saturday, apologised to on Sunday morning and helped with the rebuild on Sunday
afternoon. The library books are three years overdue and so is one city. Nobody real is named anywhere in it, on
purpose: the billionaire is *a certain billionaire* and the city is *a certain city*, which is funnier anyway.

**Nothing in here is regretted.** An entry once read "quietly regret eating a certain billionaire" and was wrong
about the character, which is the sort of thing only the person whose calendar it is can tell you. Apologising to
a city is fine, because that is paperwork rather than remorse; the meal is savoured and seconds are planned.
