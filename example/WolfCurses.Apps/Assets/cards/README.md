# Sample card file

Copied next to the executable into a `cards/` folder, which is where the card file opens and where its Open
dialog starts. **Written for this repository**: it is Maxwolf's address book, which is mostly people who tidy up
afterwards.

| File | What it is |
| --- | --- |
| `contacts.csv` | Twenty-five cards, one per letter of the alphabet with two letters deliberately left empty |

## The format

Six columns: **Name**, **Kind**, **Phone**, **Email**, **Address**, **Notes**. The first row names them, and that
row is the whole reason the program can be trusted with a file somebody has edited.

**Nothing is read by position.** Move the columns about, delete one, add one of your own, and the card file still
reads it: every value is fetched by column name. A row that is a field or two short reads as empty in the fields
it did not reach rather than throwing, and extra columns are ignored. A file with **no** header row at all is
read positionally in the order above, which is the one case that has to be guessed at, and the reason this
program always writes a header.

A row with nothing in its Name is skipped. The index is by name and the tabs are by first letter, so a nameless
card would be one nothing on the screen could ever reach. That also quietly disposes of the blank line at the end
of the file.

## The awkward rows are deliberate

Four of them exist to prove the reader is a real one, and the last is the one nothing else in this repository had
ever exercised:

- `"Aurelia Vance, City Planner"` contains the delimiter, in the field everything is sorted by.
- `"Suite 2, Marine Parade"` contains it again, in the middle of an address.
- `"Still says ""good boy""."` contains a doubled quote **and** a delimiter, which is the shape that separates a
  real CSV reader from a call to `Split`.
- The Coast Guard's note contains **three lines**. A field holding a line break is the case that settles the
  whole design of `DelimitedText`: a record and a line stop being the same thing, so no line-by-line splitter can
  read this file at all. Open the card and the note is on three lines; look at it in the list and it is flattened
  onto one, because a row of a table is a row.

Save the file and re-open it and all four come back exactly as they went in. That round trip is what the card
file is here to demonstrate.

## Two tabs are empty on purpose

Nothing is filed under **T** or **X**, so those tabs are drawn greyed and refuse both the pointer and the letter
key. A card index has always looked like that; a strip where every letter is live says nothing about what is
actually in the drawer.

## The joke

It is the address book of somebody forty feet tall, so it is a list of people who deal with the consequences: a
city planner with a stamp reading RETROACTIVELY PERMITTED, a roofer on retainer, a salvage firm whose invoices
say "site tidying", an optician who cannot help and is kind about it twice a year. The tailor quotes for a hoodie
in "adjustable". The library books are three years overdue and so is one city, which is the same joke the
[planner sample](../planner/README.md) tells from the other end.

Nobody real is named anywhere in it, on purpose. `Halden Voss` is *a certain billionaire*, filed under H, no
longer answering, and the note is in character: it was delicious and there will be seconds. Every phone number is
in the `555-01xx` range and every address is invented, both of which are the reserved-for-fiction conventions.
