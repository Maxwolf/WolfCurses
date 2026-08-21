# WolfCurses.Apps

The other half of what a terminal used to be for. Where [the arcade](../WolfCurses.Games/README.md) shows what a game built on [WolfCurses](../../README.md) looks like, this asks the same question about office software: an editor, a spreadsheet, a card file, a calculator, a diary.

```cmd
dotnet run --project example/WolfCurses.Apps
```

**One application so far: a word processor**, laid out and coloured after the MS-DOS Editor. Spreadsheet, database, calculator and diary are planned and unwritten.

That order is deliberate. Adding an application is meant to be a folder, a form carrying `[ParentWindow(typeof(AppsWindow))]`, a value on `AppsCommandsEnum`, and one `AddCommand` line in `AppsWindow`, with no registration step anywhere. It is worth proving that claim before writing anything that depends on it.

This is an interactive terminal UI, so run it from a real terminal window rather than by double-clicking, and give the window some room.

## The word processor

Menus across the top, a framed blue field with the file name in its top edge, scrollbars down the right and along the bottom, and a key-hint strip underneath.

**Keyboard.** Arrows move and SHIFT selects; CTRL with left and right walks words; HOME and END take the line, and CTRL with them the document; PAGE UP and PAGE DOWN move a screen; TAB inserts a real tab; CTRL+A selects everything. **F10 opens the menus** (so does ALT with a menu's underlined letter, where the terminal passes ALT through), and ESC shuts an open menu or leaves the editor.

**Mouse.** Click to place the caret, drag to sweep a selection, click a menu title or entry, drag the scrollbar thumb or click its arrows, and roll the wheel to scroll. The editor draws its own pointer, because a terminal stops drawing one the moment mouse reporting is switched on.

**File.** Opens on `documents/rfc1149.txt`; Open browses that folder, and Save and Save As write back.

Five pieces of it are in the library rather than here, which is the point of these examples: `TextBuffer` and `TextViewport` (the document and the window onto it), `TabStops` (a tab is one character and several columns, and keeping those in step is most of what makes a caret land correctly), `MenuBar`, and `ScrollBar`.

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
