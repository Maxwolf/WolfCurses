# Testing this on somebody else's machine

Three things about a text user interface can be wrong independently of each other, and the suite on
its own only ever sees the first:

1. **the code** - which `dotnet test` covers, on whatever machine you ran it on;
2. **the environment** - the C library, the locale, whether there is an ICU to format a date with;
3. **the terminal** - which `dotnet test` structurally cannot see at all, because a test host always
   redirects stdout and this library reads exactly that to decide what it is talking to.

This folder is for the second and third. The CI workflow runs all of it; everything here also runs
locally with nothing installed but Docker.

## Running the suite in another environment

```sh
docker build -f docker/Dockerfile -t wolfcurses-test docker
docker run --rm -v "$PWD:/src:ro" wolfcurses-test
```

The working tree is mounted **read-only** and the entrypoint copies it in, taking what git would take
(tracked files plus untracked ones `.gitignore` does not cover). That is not tidiness: building in
the mount would drop a Linux build's `bin/` and `obj/` on top of the host's, which on a Windows
machine leaves both trees wrong. Uncommitted work is included, so this tests what you have, not what
you pushed.

Anything after the image name replaces the command, so a filter is just:

```sh
docker run --rm -v "$PWD:/src:ro" wolfcurses-test \
    dotnet test tests/WolfCurses.Tests/WolfCurses.Tests.csproj -c Release
```

The environments worth running, and why each one:

| what | how | what it finds |
|---|---|---|
| musl instead of glibc | `-f docker/Dockerfile.alpine` | a P/Invoke or a runtime assumption that only holds on glibc |
| no locale at all | `-e LANG= -e LC_ALL=POSIX -e TERM=dumb` | what a container is by default: whether the box characters and half blocks ever really needed one |
| no ICU | `-e DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` | every date and number this library formats, in the mode a great many containers are built with |
| no colour | `-e NO_COLOR=1` | the promise that a resolved colour mode of None emits no escape at all |

## Running against a real terminal

```sh
docker run --rm -v "$PWD:/src:ro" wolfcurses-test pty-render-check
```

`script` hands the demo a pseudo-terminal, so for the first time the library's output is **not** a
pipe. That single difference turns on everything the suite cannot reach:

- `AnsiConsole.DetectGraphicsProtocol` stops answering `None` and reads `TERM` for real;
- `SupportsPictures` becomes true, so the screens gated on it draw their pictures;
- `ConsolePresenter` writes to an actual console, synchronized-output markers and all;
- the application leaves through its own shutdown path, which is what restores the console mode.

Four terminals are claimed in turn and each is checked against what actually arrived:

| `TERM` | what it should decide | checked |
|---|---|---|
| `xterm-kitty` | kitty | a real `ESC_Ga=T,f=32` transmission, and no sixel |
| `foot` | sixel | a real `ESC P0;1;0q` payload, and no kitty |
| `xterm-256color` | half blocks | 256-colour escapes, and neither payload |
| `dumb` | half blocks, no colour | not one `ESC[38;` anywhere |

Every case additionally checks that no picture marker (U+E000) ever reached the terminal, that the
presenter's DEC 2026 markers did, that the picture screen drew, and that the demo printed its own
goodbye - which is the proof it exited through `Program.Main` rather than being killed, and therefore
that `DisableMouse` ran. Forgetting that does not break the program, it breaks the user's console.

Add a case name to run one: `pty-render-check kitty`.

## ffmpeg

The image installs it because the office suite's media player shells out to `ffmpeg`, `ffprobe` and
`ffplay`, and without all three about twenty of its tests skip themselves - which a green run says
nothing about. `SDL_AUDIODRIVER=dummy` is set for the same reason: ffplay is SDL underneath and will
not start on a machine with no sound card, which is every container and every CI runner.

## What this still does not test

A real terminal *emulator*. `script` gives the program a pty, which is what the library reads to
decide how to behave, but nothing on the other end draws anything - so whether a picture is actually
legible, whether QuickEdit ate a click, or whether ConPTY forwarded a query still needs a person
looking at a screen. Those are marked unverifiable in the root `CLAUDE.md` and they still are.
