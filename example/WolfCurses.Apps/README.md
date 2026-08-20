# WolfCurses.Apps

The other half of what a terminal used to be for. Where [the arcade](../WolfCurses.Games/README.md) shows what a game built on [WolfCurses](../../README.md) looks like, this asks the same question about office software: an editor, a spreadsheet, a card file, a calculator, a diary.

```cmd
dotnet run --project example/WolfCurses.Apps
```

**Nothing is built yet.** What is here is the scaffolding every application will hang off: the host loop, the simulation, the menu, and the test project that drives it headlessly. The menu offers Quit and nothing else.

That order is deliberate. Adding an application is meant to be a folder, a form carrying `[ParentWindow(typeof(AppsWindow))]`, a value on `AppsCommandsEnum`, and one `AddCommand` line in `AppsWindow`, with no registration step anywhere. It is worth proving that claim before writing anything that depends on it.

This is an interactive terminal UI, so run it from a real terminal window rather than by double-clicking, and give the window some room.

## Planned

| Application | What only it will demonstrate |
| --- | --- |
| **Editor** | A caret. The library's input buffer is append-only and has no cursor by design, so this is the first screen that owns its own text buffer, and the first that needs the whole `ConsoleKeyInfo` rather than a bare key. |
| **Spreadsheet** | A language: cell references, a formula parser, evaluation in dependency order, and a circular reference as the interesting failure. |
| **Database** | Reading back what it wrote, so the only screen that has to distrust a file it created. Also where the four modal controls appear as a workflow rather than one at a time. |
| **Calculator** | The mouse as labelled buttons. Minesweeper divides to find a cell and Missile Command aims at a continuum; a keypad needs a retained layout to hit-test against. |
| **Diary** | Dates and wall-clock time: a month grid, today, and a clock that moves while you look at it. |

## ESC

**ESC backs out of any application to the menu**, done as a single `AppsWindow.OnKeyPressed` override rather than in each application, exactly as the other two examples do it.

The consequence is worth knowing up front: ESC is spent. An application wanting a menu bar of its own cannot dismiss it with ESC the way `edit.com` did, because ESC will already have left the application.
