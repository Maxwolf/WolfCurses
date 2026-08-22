# WolfCurses examples

Three runnable console applications built on [WolfCurses](../README.md). Each is its own project, referencing the library directly.

## [WolfCurses.Demo](WolfCurses.Demo/README.md) - the library tour

Images, sprites, widgets, colour, and every dialog.

![Two penguins standing on a photograph drawn in the terminal, with a frames-per-second readout above them](../docs/demo-sprite-collision.png)

## [WolfCurses.Games](WolfCurses.Games/README.md) - the arcade

Ten games, each built on a different part of the library.

![Pac-Man: a maze drawn as a connected network of lines, with pellets, four coloured ghosts and a status panel](../docs/game-pacman.png)

## [WolfCurses.Apps](WolfCurses.Apps/README.md) - the office suite

Seven applications: a word processor, BASIC, a spreadsheet, a calculator, a planner, a card file and a media player.

![A spreadsheet with lettered columns, numbered rows, formulas, menus across the top and scrollbars down two sides](../docs/app-spreadsheet.png)

## Running them

```cmd
dotnet run --project example/WolfCurses.Demo
dotnet run --project example/WolfCurses.Games
dotnet run --project example/WolfCurses.Apps
```

All three are terminal UIs, so run them from a real terminal window rather than by double-clicking, and give the window some room.

Prebuilt, self-contained downloads for Windows, macOS and Linux are on the [releases page](https://github.com/Maxwolf/WolfCurses/releases), with all of them in a single archive and no .NET install needed.
