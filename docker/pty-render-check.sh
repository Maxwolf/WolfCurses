#!/usr/bin/env bash
#
# What this library writes when its output is a REAL TERMINAL rather than a pipe.
#
# This is the half `dotnet test` structurally cannot reach. A test host always redirects stdout, and
# the library reads exactly that to decide what it is talking to: `AnsiConsole.DetectGraphicsProtocol`
# answers None the moment output is redirected, `SupportsPictures` answers false wherever virtual
# terminal processing could not be enabled, and `ConsolePresenter` only exists when stdout is a
# console at all. So every screen test in the repository - all 2974 of them - runs the branch where
# there is no terminal, and the branch where there is one had never been executed by anything but a
# person watching it.
#
# `script` gives the demo a pseudo-terminal and copies everything the terminal receives into a file,
# which is what makes the whole of that reachable: the auto-detection runs for real against TERM,
# a real sixel or kitty payload is emitted, the presenter's synchronized-output markers go out, and
# the application's own shutdown path runs to the end.
#
# Usage: pty-render-check [case ...]      (default: all of them)
set -uo pipefail

repo=${WORK:-$(pwd)}
cd "$repo"

# The menu number is read out of the enum rather than written down here, for the reason the demo's
# own tests are written that way: six screens have gone in above End and moved it from 21 to 27, and
# a hardcoded number quietly starts quitting the application in the middle of a check.
end=$(grep -oE 'CloseSimulation[[:space:]]*=[[:space:]]*[0-9]+' example/WolfCurses.Demo/DemoCommandsEnum.cs | grep -oE '[0-9]+$')
slideshow=$(grep -oE 'Slideshow[[:space:]]*=[[:space:]]*[0-9]+' example/WolfCurses.Demo/DemoCommandsEnum.cs | grep -oE '[0-9]+$')

if [ -z "$end" ] || [ -z "$slideshow" ]; then
    echo "could not read the demo's menu numbers out of DemoCommandsEnum.cs" >&2
    exit 2
fi

esc=$(printf '\033')

# U+E000 in UTF-8. The private-use marker that tells ConsolePresenter which rows are a picture, and
# which it must strip before writing: if one of these ever reaches a terminal it is drawn as a box.
marker=$(printf '\356\200\200')

failures=0

# Runs the demo under a pseudo-terminal with a given TERM and checks what came out.
#
#   $1 name        what to call this case in the output
#   $2 term        what to claim the terminal is
#   $3 expect      text that must appear on the menu header, naming what it chose to draw with
#   $4 require     a byte sequence that must appear, or empty for none
#   $5 forbid      a byte sequence that must NOT appear, or empty for none
check() {
    local name=$1 term=$2 expect=$3 require=$4 forbid=$5
    local out status size
    out=$(mktemp)

    # Keys are fed through the pty: dismiss the splash, open the slideshow (which draws a picture
    # unconditionally, so it does not matter what this terminal can carry), back out, quit, and one
    # last key for the goodbye prompt Program.Main waits on.
    #
    # The first ENTER is sent twice on purpose. A key pressed before the application has attached a
    # window is spent and dropped by the library - deliberately, since that is what stops the input
    # queue spinning forever - so on a loaded machine a single one can go nowhere and leave every
    # later keystroke landing on the wrong screen. A second costs nothing: an empty line at the menu
    # acts on the highlight, and the highlight is hidden until an arrow key summons it.
    (
        sleep 6; printf '\r'
        sleep 2; printf '\r'
        sleep 1; printf '%s\r' "$slideshow"
        sleep 5; printf '\033'
        sleep 2; printf '%s\r' "$end"
        sleep 3; printf '\r'
        sleep 2
    ) | TERM="$term" script -qec \
            "dotnet run --project example/WolfCurses.Demo --no-build -c Release" /dev/null \
            > "$out" 2>&1
    status=$?

    size=$(wc -c < "$out")
    local problems=()

    [ "$status" -eq 0 ] || problems+=("the demo exited $status")
    [ "$size" -gt 2000 ] || problems+=("only $size bytes reached the terminal")

    # It got as far as the picture screen. A byte count cannot say that on its own: a colourless
    # terminal is served about a hundredth of what a kitty one is, so any floor high enough to mean
    # something on one is a false failure on the other.
    grep -aqF "ANSI Slideshow" "$out" || problems+=("the slideshow screen never drew")

    # Its own goodbye, which is the proof it left through Program.Main rather than being killed - and
    # therefore that DisableMouse ran. Forgetting that does not break the program, it breaks the
    # user's console window, and on classic conhost it stays broken after the process is gone.
    grep -aqF "Goodbye!" "$out" || problems+=("it never reached its own goodbye")

    # The presenter's real path: one buffered write per frame wrapped in DEC 2026 synchronized output.
    grep -aqF "${esc}[?2026h" "$out" || problems+=("no synchronized-output markers were written")

    grep -aqF "$marker" "$out" && problems+=("a picture marker was written to the terminal")

    grep -aqF "$expect" "$out" || problems+=("the menu never said \"$expect\"")

    if [ -n "$require" ]; then
        grep -aqF "$require" "$out" || problems+=("the expected payload never appeared")
    fi

    if [ -n "$forbid" ]; then
        grep -aqF "$forbid" "$out" && problems+=("a payload appeared that this terminal cannot read")
    fi

    if [ ${#problems[@]} -eq 0 ]; then
        printf 'ok    %-10s TERM=%-16s %8d bytes\n' "$name" "$term" "$size"
        rm -f "$out"
        return 0
    fi

    printf 'FAIL  %-10s TERM=%-16s %8d bytes\n' "$name" "$term" "$size"
    printf '        - %s\n' "${problems[@]}"
    printf '        capture kept at %s\n' "$out"
    failures=$((failures + 1))
    return 1
}

# a=T,f=32 is the kitty renderer transmitting 32-bit RGBA pixels for display. Deliberately NOT a bare
# ESC_G, which the start-up probe's own query also is: matching that would pass on every terminal.
kitty_payload="${esc}_Ga=T,f=32,"
sixel_payload="${esc}P0;1;0q"

run_case() {
    case $1 in
        kitty)
            check kitty xterm-kitty "Render type: kitty" "$kitty_payload" "$sixel_payload" ;;
        sixel)
            check sixel foot "Render type: sixel" "$sixel_payload" "$kitty_payload" ;;
        cells)
            # A terminal with colour and no graphics protocol: half blocks, in 256-colour escapes,
            # and no true-pixel payload of either kind.
            check cells xterm-256color "Render type: half blocks" "${esc}[38;5;" "$kitty_payload" ;;
        plain)
            # A terminal that says outright it can do nothing. NO_COLOR asked for no escapes rather
            # than fewer of them, so no colour is set at all - and the shaded ASCII ramp is what the
            # pictures become.
            check plain dumb "Render type: half blocks" "" "${esc}[38;" ;;
        *)
            echo "unknown case: $1" >&2
            return 2 ;;
    esac
}

echo "building the demo once, so the runs below start at once"
dotnet build example/WolfCurses.Demo -c Release -v q --nologo || exit 2

cases=("$@")

if [ ${#cases[@]} -eq 0 ]; then
    cases=(kitty sixel cells plain)
fi

for name in "${cases[@]}"; do
    run_case "$name"
done

if [ "$failures" -ne 0 ]; then
    echo "$failures terminal check(s) failed"
    exit 1
fi

echo "every terminal check passed"
