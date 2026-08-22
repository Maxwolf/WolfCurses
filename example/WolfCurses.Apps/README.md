# WolfCurses.Apps

The other half of what a terminal used to be for. Where [the arcade](../WolfCurses.Games/README.md) shows what a game built on [WolfCurses](../../README.md) looks like, this asks the same question about office software: an editor, a spreadsheet, a card file, a calculator, a diary.

```cmd
dotnet run --project example/WolfCurses.Apps
```

**Seven applications**: a word processor laid out after the MS-DOS Editor, a BASIC environment after the one that shipped with MS-DOS, a spreadsheet, a desk calculator, a calendar and planner, a card file, and a media player.

That order is deliberate. Adding an application is meant to be a folder, a form carrying `[ParentWindow(typeof(OfficeWindow))]`, a value on `OfficeCommandsEnum`, and one `AddCommand` line in `OfficeWindow`, with no registration step anywhere. It is worth proving that claim before writing anything that depends on it.

This is an interactive terminal UI, so run it from a real terminal window rather than by double-clicking, and give the window some room.

## The word processor

Menus across the top, a framed blue field with the file name in its top edge, scrollbars down the right and along the bottom, and a key-hint strip underneath.

**Keyboard.** Arrows move and SHIFT selects; CTRL with left and right walks words; HOME and END take the line, and CTRL with them the document; PAGE UP and PAGE DOWN move a screen; TAB inserts a real tab; CTRL+A selects everything. **F10 opens the menus** (so does ALT with a menu's underlined letter, where the terminal passes ALT through), and ESC shuts an open menu or leaves the editor.

**Clipboard.** CTRL+X cuts, CTRL+INS copies and CTRL+V pastes, and the MS-DOS Editor's own SHIFT+DEL and SHIFT+INS do the same two. Copy is CTRL+INS rather than CTRL+C because a console turns CTRL+C into the signal that quits the program before any application can read it as a key. The clipboard belongs to the suite rather than to the editor, so what you copy will still be there for the spreadsheet; it is not the operating system's clipboard, and nothing copied here leaves the program.

**Mouse.** Click to place the caret, drag to sweep a selection, click a menu title or entry, hover an open menu to walk its highlight, drag the scrollbar thumb or click its arrows, and roll the wheel to scroll. The editor draws its own pointer, because a terminal stops drawing one the moment mouse reporting is switched on.

**Search.** CTRL+F asks what to find and selects the first match; F3 and SHIFT+F3 walk to the next and previous, coming round the ends rather than stopping; CTRL+H asks what to change and what to change it to, and changes every one. Match Case and Whole Word are ticked entries on the Search menu rather than checkboxes in a dialog, so the setting is readable without opening anything.

**Spelling.** F7 checks the document and stops on each word it does not know, offering what was probably meant; choosing a correction applies it and carries straight on to the next. Words with digits in them, acronyms in capitals and single letters are left alone. Tools also counts the words. The dictionary is 370,105 public domain words shipped alongside; see [its provenance note](Assets/dictionary/README.md) for why the small frequency list everyone reaches for first could not be used.

**File.** Opens on `documents/rfc1149.txt`; Open browses that folder, and Save and Save As write back.

Five pieces of it are in the library rather than here, which is the point of these examples: `TextBuffer` and `TextViewport` (the document and the window onto it), `TabStops` (a tab is one character and several columns, and keeping those in step is most of what makes a caret land correctly), `MenuBar`, and `ScrollBar`.

## BASIC

A program in an editor, and the screen it draws on when you run it. **F5 runs**, ESC stops a running program and goes back to the listing, ESC again leaves; F3 opens another `.bas` from disk.

The language covers expressions with BASIC's own precedence, variables and arrays, `PRINT` with its layout punctuation, `INPUT`, `IF` in both forms with `ELSEIF`, `FOR`/`NEXT` with `STEP`, `WHILE`/`WEND`, all four shapes of `DO`/`LOOP`, `SELECT CASE` with `IS` and `TO`, `GOTO`/`GOSUB`/`RETURN`, `DIM`, `SUB` and `FUNCTION` with `SHARED` and recursion, `EXIT FOR` and `EXIT DO`, `DATA`/`READ`/`RESTORE`, and the usual string and maths functions. **Graphics too**: `SCREEN` picks a mode and `PSET`, `LINE` (with `B` and `BF`), `CIRCLE` and `PAINT` draw into it, coming out as real pixels where the terminal supports them and half blocks where it does not. `GET` and `PUT` move sprites about, with XOR as the default so stamping twice rubs one out again. `SOUND` and `PLAY` make actual noise through the PC speaker, including PLAY's whole note language; the notes go to a thread of their own, because playing one blocks for its length and the screen has to stay answerable to ESC. Errors name the line they happened on.

Seven sample programs ship, all written for this repository: `welcome.bas` walks through the language, `greet.bas` asks questions, `shapes.bas` draws with `LOCATE`, `procedures.bas` shows SUBs, FUNCTIONs and recursion, `drawing.bas` is the graphics demonstration, `sprite.bas` walks a face across the screen with GET and PUT, and `music.bas` works through PLAY and SOUND. The tunes in that last one are all long out of copyright: Grieg's *In the Hall of the Mountain King* of 1875, whose accelerando makes it the best demonstration of tempo there is, a Beethoven melody of 1824 and a French air of 1761, plus scales and a fanfare written here. **The QBasic samples everybody remembers are Microsoft's** and carry no redistribution licence, so they are not included; open your own copy with F3.

## The spreadsheet

A grid of cells with the same menus and frame round it, lettered columns, numbered rows, a cell entry line under the sheet and scrollbars on two sides. It opens on `sheets/spreadsheet.csv`, which is a joke ledger about a year of consulting income going out again on graphics cards and coffee.

**Editing.** Arrows move; typing anything starts editing the cell with that character already in it; **F2** keeps what is there instead. ENTER accepts and moves down, TAB accepts and moves right, ESC abandons the edit. DEL clears whatever is selected, and SHIFT with the arrows sweeps a rectangle. HOME and END take the row, CTRL with them the whole sheet, and CTRL+END goes to the end of the *data* rather than the end of the two-hundred-row grid.

**Formulas.** A cell beginning with `=` is worked out: arithmetic with the usual precedence, brackets, powers, cell references, ranges, and `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `ROUND`, `ABS`, `INT` and `SQRT`. **A cell holds what you typed and is worth something else** - the entry line under the grid shows the formula while the cell shows the answer, which is the whole distinction a spreadsheet is built on and is why saving gives back the formula rather than the number. Mistakes are values rather than exceptions: `#DIV/0!`, `#NAME?`, `#VALUE!`, `#REF!` and `#CIRC!` for a cell that needs its own value to work out its own value. **Data > Total Selection** writes a `=SUM(...)` underneath whatever is selected.

**Charts.** **F6** draws the selection as labelled bars and **F7** as a line graph, both of them the library's own widgets. Labels come from the cells beside the numbers, so a column of figures with the month names to its left charts itself with no configuration at all. A rectangle is charted by its first column and the caption says so, rather than quietly picking one.

**Mouse.** Click a cell, drag to sweep a rectangle, click a column letter or a row number to select the whole of it, drag the scrollbar thumb, roll the wheel. With a menu open, moving the pointer down the panel highlights each entry and sliding along the bar opens each menu in turn.

The columns are **ruled off from each other**, in the column each cell was already leaving blank to keep its text clear of its neighbour's, so the grid costs no width at all. The rule takes each cell's own background, which keeps a swept selection in one piece rather than striping it; a merged banner has one at its right-hand end and none inside it.

**Merged cells** are how the instructions at the top of the sample are drawn across the sheet. A comma separated file has nowhere to record a merge, so the loader infers one: a row with something in its first cell and nothing in any other is a banner. **Data > Merge Across** and **Unmerge** do it by hand.

**Shortcuts here are function keys** rather than control combinations, because a control combination the console keeps for itself never arrives at all, and a menu advertising a key that does nothing is worse than one advertising none. The clipboard three are the exception, and cutting or copying a range puts it on the suite clipboard as tab separated text, so it arrives in the word processor as a table rather than a run-on line.

Four pieces of this are in the library rather than here: `TableViewport` (which columns are on screen and which one a click landed in, when the columns are not all the same width), `TextRow` (a row built of styled runs, so the menu panel can be drawn over it), `DelimitedText` (reading and writing the file), and `TextBuffer` again, this time holding one line of text as the cell editor.

## The calculator

A desk calculator with a paper tape: keys you can click, keys you can type, and a record of what you did.

**The keys.** Digits, the four operations, percent, square root, reciprocal, square, sign, rub out, clear entry, clear, and five memory keys. Click any of them, or type: **the number pad works**, and so does the top row of digits. ENTER totals, BACKSPACE rubs out, DEL clears the entry, C resets, and `%` `R` `N` are percent, root and sign. The memory keys are F5 to F8. Shortcuts here are function keys rather than control combinations for the same reason as in the spreadsheet: a combination the console keeps for itself never arrives, and a key that does nothing is worse than no key.

**It works left to right with no precedence**, so `2 + 3 x 4` is **20**, not 14. That is what every desk calculator and adding machine has always done, and it is not a shortcut: pressing an operator finishes whatever was pending before starting the next. The tape is there so the working is visible rather than surprising. Pressing `=` again repeats the last operation, so `2 + 3 = = =` counts up in threes.

**The arithmetic is decimal**, so `0.1 + 0.2` is `0.3` exactly. A calculator that says 0.30000000000000004 is a broken calculator however defensible the floating point; the spreadsheet next door uses double on purpose and for the opposite reason.

**Percent takes its meaning from the operator waiting for it**, which surprises people every time and is exactly why the key is worth having: `200 + 10 %` is 220, a discount off a total without typing the total twice. With a times or a divide there is nothing for a percentage to be *of*, so it is simply a hundredth.

**Dividing by nothing is an error you have to clear**, shown on the display, with every other key refused until it is. An error that quietly became part of a later sum would be worse than one that stops you.

**Edit > Copy** puts the display on the suite clipboard as a plain number, so a total worked out here pastes into a spreadsheet cell next door; **Paste** types a number back in a digit at a time, so every rule about what may be typed still holds.

The keys themselves are the library's `Keypad`, which is what this application exists to demonstrate: its keys are not all one width, so the layout has to be remembered rather than recomputed, and the drawing and the hit test read the same copy of it.

## The planner

A month you can walk around, the day's entries beside it, and a clock that moves while you look at it.

**Four ways of looking at it**, on TAB or on F5 to F8, and they are a zoom rather than four skins on the same thing: each answers a question the others cannot.

| View | What only it answers |
| --- | --- |
| **Month** | Which day of the week something falls on. |
| **Week** | What a week actually contains, written out, which a grid with room for a number and a dot cannot show. |
| **Year** | Which parts of the year are busy. Twelve strips of days, marked where something happens, with a count on the end. |
| **Coming up** | What is next, with the empty days simply not there. |

**The arrow keys mean something different in each, deliberately.** Stepping a day at a time through a year is useless and stepping a month at a time through a week is meaningless, so UP and DOWN are a week in the month view, a month in the year view, and the previous or next *entry* in the list. What never changes is that there is one chosen day and every view shows where it is, so choosing a date in the year view and pressing F5 lands on it in the month.

**The year is strips, not twelve little calendars**, because twelve of those need thirty-two rows and a terminal has twenty-four. A strip gives up which weekday a date falls on, which is exactly what the month view is for, and buys the whole year at once, which is exactly what it is not. Easter turning up on a different day of April each year is visible at a glance.

**Getting about.** Arrows move a day at a time, PAGE UP and PAGE DOWN turn the month, HOME comes back to today, and clicking a date chooses it in any view. The month follows the cursor, so walking off the end of one simply arrives in the next.

**Entries.** **F2** asks what happens and then at what time; leaving the time blank makes it an all-day entry, and so does cancelling that second question, because throwing away something already typed would be the worse answer. **DEL** offers whatever is on the day and removes the one you pick. F3 opens another planner, F4 saves.

**The holidays are worked out, not looked up.** Page to 2099 and Easter is still in the right place. Four shapes of rule are behind that: a fixed date, the n-th weekday of a month (Thanksgiving), the last weekday of a month (Memorial Day), and Easter, which is none of those and drags Good Friday and Easter Monday along with it. A holiday cannot be deleted, and that is not a restriction so much as an honest one: there is nothing stored to delete, and next year's would be computed again anyway.

**Annual entries.** A date written `05-04` happens every year and one written `2026-05-04` happens once. That is the whole of the file format, and it is what keeps the shipped sample meaning something in any year rather than going stale the moment 2026 ends. A leap-day annual entry happens only in leap years, since there is no other day it could honestly be moved to.

**The clock.** It shows the real date and time and ticks once a second. Two things about it are the reason this application exists: the time is read on the *simulation* tick and not while drawing, because a render runs about a thousand times a second and would ask the operating system as often; and **today is re-asked every second too**, so a planner left open overnight moves its own highlight instead of going on marking yesterday.

The sample is Maxwolf's own calendar, which is mostly moulting, tail maintenance and apologising to a coast guard, with the occasional city.

## The card file

An address book you can flip through: one card at a time with its fields laid out, or all of them at once as a table, with a row of letter tabs across the top.

```
┌─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┐  25 cards
│A│B│C│D│E│F│G│H│I│J│K│L│M│N│O│P│Q│R│S│T│U│V│W│X│Y│Z│#│  Type a letter to
└─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┘  flip to that tab.
┌ contacts.csv ─────────────────────────────────────────── 4 of 25 ┐
│Name     Coast Guard (Western Station)                            │
│Kind     Institution                                              │
│Phone    555-0100                                                 │
│Address  Pier 4                                                   │
│Notes    Written apology due the first of every month.            │
│         They have a folder. The folder has a name and the name   │
│         is mine.                                                 │
```

**It is the screen that has to distrust a file it wrote itself.** Everything else here reads a file somebody else made, or reads its own and gets away with assuming the shape. Save this one, open `contacts.csv` in the word processor two menu items up, move the columns about, delete one, hand-type a row that is a field short, and open it here again: it still reads, because **nothing is read by position**. The header row says what the columns are and every value is fetched by name. A file with no header at all is read positionally, which is the one case that has to be guessed at and the reason this program always writes one.

**Getting about.** LEFT and RIGHT flip cards, UP and DOWN walk the fields, and **typing a letter flips to that tab** the way a card index has always worked. **TAB** switches to the list, where up and down move through the cards instead and left and right scroll the table sideways. A tab with nothing behind it is drawn greyed and refuses both the pointer and the key, which is what a real drawer looks like; two letters are empty in the sample on purpose.

**Cards.** **F2** or ENTER edits the field the cursor is on, **F7** starts a new card, **DEL** throws one away after asking, and **F9** finds text in any field of any card. Find starts *after* the card you are on and wraps round to it, and the prompt comes back holding the last search, so F9 and ENTER is Find Next.

**Renaming a card moves it**, because the deck is an index rather than a list, and the cursor goes with it rather than staying on the position it used to be at.

**Where the four modal controls turn up as a workflow.** Opening another file with unsaved changes asks whether to save first, which may ask for a folder and then a name, and only then asks which file to open. Which fields the list shows is a multiple-choice list of all six with the current ones already ticked.

**The note is the interesting field.** It holds line breaks, which is the CSV case that settles the whole design of the reader: a record and a line stop being the same thing, so no line-by-line splitter can read the file at all. The card wraps it over several rows; the list flattens it onto one, because a row of a table is a row. A note longer than a single line is written in the word processor, since a one-line prompt gives back one line.

The sample is Maxwolf's address book, which is mostly people who tidy up afterwards: a city planner with a stamp reading RETROACTIVELY PERMITTED, a roofer on retainer, a salvage firm whose invoices say "site tidying", and an optician who cannot help and is kind about it twice a year.

## The media player

Pick a film and watch it in the terminal, with the sound playing. It works over whatever [ffmpeg](https://ffmpeg.org) is already on the machine, and it is the one screen here with no dependency it could have taken instead: the library has no way to make a sound, and getting one means platform interop it does not have.

```
 File  Play                                                               Help
 sample.mp4   h264 640x360 30fps + aac 2ch   drawn as sixel at 780x320
 ...the picture, full width...
 1:15 ━━━━━━━━━●───────────────────────────────────────────────── 9:56
  Playing  30fps   SPACE=Play/Pause  F3=Open  Arrows=Seek  F10=Menu
```

**It tells you what it found, and it degrades three different ways.** Without **ffmpeg** nothing can be decoded; without **ffprobe** files still play but with no known length to scrub along; without **ffplay** everything works silently. And separately from all of that, a terminal that cannot take pictures still gets the sound and the bars. The idle page says which of those you are in before you have done anything, and Help has the same report.

**Nothing is shipped to play**, because a video is megabytes and every one worth watching belongs to somebody. **F7** plays ffmpeg's own test pattern and **F8** a test tone: no file, no download, no licence, and they exercise the whole pipeline.

**ffmpeg is asked for pixels exactly the size of the window.** That is the difference between thirty frames a second and three: resampling is the dominant cost in the rendering stack, so a 1920x1080 frame resized into a seventy-column window in managed code, thirty times a second, is where all the time goes. Asking the renderer how many pixels it puts in a character cell and having ffmpeg scale and letterbox to exactly that means nothing is ever resampled on this side.

**Something with no picture in it gets a spectrum instead**, twenty-odd bars with peak markers that fall back slowly. The bars are the library's; the transform is this application's, and the three things that make it a spectrum rather than a picture of noise are written up where it happens.

**Getting about.** **SPACE** plays and pauses, the arrows seek five seconds and thirty, **HOME** goes back to the start, and the bar can be clicked anywhere to seek there. **F3** opens a file, **F5** plays it again, **F6** closes it.

**Frames are dropped, never delayed.** A frame belongs at a moment, so a terminal that was busy when the moment came skips to the frame that belongs *now* rather than showing the late one. That is the whole reason `PlaybackClock` is deliberately the opposite of `IntervalTimer`, and it is why the picture stays with the sound instead of drifting further behind it for as long as the program runs.

## ESC

**ESC backs out of any application to the menu**, done as a single `OfficeWindow.OnKeyPressed` override rather than in each application, exactly as the other two examples do it.

An application with something nested open gets first refusal, through a small `IHandlesEscape` interface, so pressing ESC with a menu down shuts the menu rather than dropping you out of the editor. The library ships no ESC handling of its own on purpose; this is the five lines of the pattern it declined to make everyone's business.
