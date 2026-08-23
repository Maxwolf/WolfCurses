#!/usr/bin/env bash
#
# Copies the mounted working tree into the container and runs whatever it was asked to.
#
# The copy is the whole point rather than a nicety. Building in the mount would put a Linux build's
# bin/ and obj/ on top of the host's, which on a Windows machine means both trees are then wrong and
# the next build on either side has to be told to start again. Taking what git would take - tracked
# files plus untracked ones that .gitignore does not cover - gets the working tree including
# whatever is uncommitted, and leaves out exactly the build output that must not travel.
set -euo pipefail

src=${SRC:-/src}
work=${WORK:-/work}

if [ ! -d "$src" ]; then
    echo "nothing mounted at $src - run with: -v /path/to/WolfCurses:/src:ro" >&2
    exit 2
fi

# The mount belongs to whoever owns it on the host, which is not this container's root, and git
# refuses to read a repository it thinks somebody else owns.
git config --global --add safe.directory "$src"

mkdir -p "$work"
( cd "$src" && tar -cf - $(git ls-files -co --exclude-standard) ) | tar -C "$work" -xf -

cd "$work"
exec "$@"
