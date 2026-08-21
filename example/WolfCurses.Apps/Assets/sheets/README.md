# Sample sheet

Copied next to the executable into a `sheets/` folder, which is where the spreadsheet opens and
where its Open dialog starts. **Written for this repository**, so there is no third-party
provenance to record: it is a joke ledger about somebody spending consulting income on graphics
cards and coffee.

| File | What it is |
| --- | --- |
| `spreadsheet.csv` | A year of income and spending, a table of every purchase behind it, and a few sums at the bottom |

## Why it is shaped the way it is

It has to do four jobs at once, and each one constrains it.

**It has to be long enough to scroll.** Fifty-six rows against fifteen on screen, so the scrollbar
has a thumb worth dragging and PAGE DOWN has somewhere to go.

**It has to have numbers worth drawing a picture of.** Twelve months of four categories is a real
series: it varies, it has a spike in it (the graphics cards), and the month names sit in the column
immediately to the left of the figures, which is where the chart takes its labels from. The
instructions point at `B9:B20` in particular, because that is the twelve months on their own:
select the whole of column B and the total comes with it, which is two hundred and forty thousand
against twenty thousand and flattens every other bar.

**It has to prove the formulas are real.** The Net column subtracts three cells from a fourth, the
Total row sums twelve, and the block at the bottom uses `MAX`, `COUNT`, `AVERAGE` and `ROUND`. Every
one of them is checked by `SheetLibraryTests`, which works the answers out from the neighbouring
cells rather than reading them out of this file. A formula whose range is off by a row still
produces a number, and a test that only asserted a number appeared would sleep through it.

**It has to say how to drive the program.** That is what the merged rows at the top are for. A
comma separated file has nowhere to record that a cell is merged, so the loader infers it: a row
with something in its first cell and nothing in any other is a banner and is drawn across the whole
width. Nothing else in a table has that shape. The consequence is a hard limit on their length:
six twelve-wide columns less one for the gap is **seventy-one characters**, and a banner longer
than that is silently cut off on the eighty-column screen the suite targets. There is a test.

## The awkward fields are deliberate

Three rows exist to prove the reader is a real one rather than a call to `Split`:

- `"1200W power supply, obviously"`: a quoted field containing the delimiter.
- `"27"" monitor, the fourth one"`: a quoted field containing **both** a doubled quote and a
  delimiter, which is the shape that separates the two.
- `"=ROUND(AVERAGE(D25:D48),2)"`: a *formula* that has to be quoted, because its own argument
  separator is a comma.

## The joke

The drug is caffeine. The espresso machine is filed under Drugs and the fridge that keeps the
energy drinks cold is filed under Hardware, which is the sort of accounting decision that tells you
everything. The water cooling loop is bought twice and there is a line item for towels between the
two attempts.
