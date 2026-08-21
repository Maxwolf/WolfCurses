# WolfCurses.Apps

The other half of what a terminal used to be for. Where [the arcade](../WolfCurses.Games/README.md) shows what a game built on [WolfCurses](../../README.md) looks like, this asks the same question about office software: an editor, a spreadsheet, a card file, a calculator, a diary.

```cmd
dotnet run --project example/WolfCurses.Apps
```

**Two applications so far**: a word processor laid out after the MS-DOS Editor, and a BASIC environment after the one that shipped with MS-DOS. Spreadsheet, database, calculator and diary are planned and unwritten.

That order is deliberate. Adding an application is meant to be a folder, a form carrying `[ParentWindow(typeof(AppsWindow))]`, a value on `AppsCommandsEnum`, and one `AddCommand` line in `AppsWindow`, with no registration step anywhere. It is worth proving that claim before writing anything that depends on it.

This is an interactive terminal UI, so run it from a real terminal window rather than by double-clicking, and give the window some room.

## The word processor

Menus across the top, a framed blue field with the file name in its top edge, scrollbars down the right and along the bottom, and a key-hint strip underneath.

**Keyboard.** Arrows move and SHIFT selects; CTRL with left and right walks words; HOME and END take the line, and CTRL with them the document; PAGE UP and PAGE DOWN move a screen; TAB inserts a real tab; CTRL+A selects everything. **F10 opens the menus** (so does ALT with a menu's underlined letter, where the terminal passes ALT through), and ESC shuts an open menu or leaves the editor.

**Clipboard.** CTRL+X cuts, CTRL+INS copies and CTRL+V pastes, and the MS-DOS Editor's own SHIFT+DEL and SHIFT+INS do the same two. Copy is CTRL+INS rather than CTRL+C because a console turns CTRL+C into the signal that quits the program before any application can read it as a key. The clipboard belongs to the suite rather than to the editor, so what you copy will still be there for the spreadsheet; it is not the operating system's clipboard, and nothing copied here leaves the program.

**Mouse.** Click to place the caret, drag to sweep a selection, click a menu title or entry, drag the scrollbar thumb or click its arrows, and roll the wheel to scroll. The editor draws its own pointer, because a terminal stops drawing one the moment mouse reporting is switched on.

**Search.** CTRL+F asks what to find and selects the first match; F3 and SHIFT+F3 walk to the next and previous, coming round the ends rather than stopping; CTRL+H asks what to change and what to change it to, and changes every one. Match Case and Whole Word are ticked entries on the Search menu rather than checkboxes in a dialog, so the setting is readable without opening anything.

**Spelling.** F7 checks the document and stops on each word it does not know, offering what was probably meant; choosing a correction applies it and carries straight on to the next. Words with digits in them, acronyms in capitals and single letters are left alone. Tools also counts the words. The dictionary is 370,105 public domain words shipped alongside; see [its provenance note](Assets/dictionary/README.md) for why the small frequency list everyone reaches for first could not be used.

**File.** Opens on `documents/rfc1149.txt`; Open browses that folder, and Save and Save As write back.

Five pieces of it are in the library rather than here, which is the point of these examples: `TextBuffer` and `TextViewport` (the document and the window onto it), `TabStops` (a tab is one character and several columns, and keeping those in step is most of what makes a caret land correctly), `MenuBar`, and `ScrollBar`.

## BASIC

A program in an editor, and the screen it draws on when you run it. **F5 runs**, ESC stops a running program and goes back to the listing, ESC again leaves; F3 opens another `.bas` from disk.

The language covers expressions with BASIC's own precedence, variables and arrays, `PRINT` with its layout punctuation, `INPUT`, `IF` in both forms with `ELSEIF`, `FOR`/`NEXT` with `STEP`, `WHILE`/`WEND`, all four shapes of `DO`/`LOOP`, `SELECT CASE` with `IS` and `TO`, `GOTO`/`GOSUB`/`RETURN`, `DIM`, `SUB` and `FUNCTION` with `SHARED` and recursion, `EXIT FOR` and `EXIT DO`, `DATA`/`READ`/`RESTORE`, and the usual string and maths functions. **Graphics too**: `SCREEN` picks a mode and `PSET`, `LINE` (with `B` and `BF`), `CIRCLE` and `PAINT` draw into it, coming out as real pixels where the terminal supports them and half blocks where it does not. `GET` and `PUT` move sprites about, with XOR as the default so stamping twice rubs one out again. `SOUND` and `PLAY` make actual noise through the PC speaker, including PLAY's whole note language; the notes go to a thread of their own, because playing one blocks for its length and the screen has to stay answerable to ESC. Errors name the line they happened on.

Seven sample programs ship, all written for this repository: `welcome.bas` walks through the language, `greet.bas` asks questions, `shapes.bas` draws with `LOCATE`, `procedures.bas` shows SUBs, FUNCTIONs and recursion, `drawing.bas` is the graphics demonstration, `sprite.bas` walks a face across the screen with GET and PUT, and `music.bas` works through PLAY and SOUND. The tunes in that last one are all long out of copyright: Grieg's *In the Hall of the Mountain King* of 1875, whose accelerando makes it the best demonstration of tempo there is, a Beethoven melody of 1824 and a French air of 1761, plus scales and a fanfare written here. **The QBasic samples everybody remembers are Microsoft's** and carry no redistribution licence, so they are not included; open your own copy with F3.

## Planned

| Application | What only it will demonstrate |
| --- | --- |
| **Spreadsheet** | A language: cell references, a formula parser, evaluation in dependency order, and a circular reference as the interesting failure. |
| **Database** | Reading back what it wrote, so the only screen that has to distrust a file it created. Also where the four modal controls appear as a workflow rather than one at a time. |
| **Calculator** | The mouse as labelled buttons. Minesweeper divides to find a cell and Missile Command aims at a continuum; a keypad needs a retained layout to hit-test against. |
| **Diary** | Dates and wall-clock time: a month grid, today, and a clock that moves while you look at it. |

## ESC

**ESC backs out of any application to the menu**, done as a single `AppsWindow.OnKeyPressed` override rather than in each application, exactly as the other two examples do it.

An application with something nested open gets first refusal, through a small `IHandlesEscape` interface, so pressing ESC with a menu down shuts the menu rather than dropping you out of the editor. The library ships no ESC handling of its own on purpose; this is the five lines of the pattern it declined to make everyone's business.
