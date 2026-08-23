# The environments a GitHub runner cannot give this library, and the tools its own tests need.
#
# Nothing about the source is baked in: the working tree arrives as a bind mount and the entrypoint
# copies it in. That keeps one image good for every run, and keeps a container's build artifacts out
# of the host's tree, which matter because they are a different operating system's.
ARG SDK=10.0
FROM mcr.microsoft.com/dotnet/sdk:${SDK}

# ffmpeg   - what the office suite's media player shells out to. Without it a dozen of its tests
#            skip themselves and say so to nobody.
# util-linux - `script`, which is the only cheap way to hand a program a real pseudo-terminal, and
#            therefore the only way to see what this library writes when its output is NOT a pipe.
# locales  - so a run can be given a locale, or deliberately denied one.
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg util-linux locales \
 && rm -rf /var/lib/apt/lists/* \
 && sed -i 's/^# *\(en_US.UTF-8\)/\1/' /etc/locale.gen \
 && locale-gen

# A container has no sound card, and ffplay will not start without one - it is SDL underneath, and
# SDL_Init fails. The dummy driver is what makes "stopping the sound really ends the program playing
# it" runnable on a machine that has no sound at all, which is every CI runner.
ENV SDL_AUDIODRIVER=dummy
ENV SDL_AUDIO_DRIVER=dummy

# The default is a terminal that speaks 256 colours and no graphics protocol, which is the ordinary
# case; every interesting run overrides it.
ENV TERM=xterm-256color
ENV LANG=en_US.UTF-8

COPY entrypoint.sh /usr/local/bin/entrypoint
COPY pty-render-check.sh /usr/local/bin/pty-render-check
RUN chmod +x /usr/local/bin/entrypoint /usr/local/bin/pty-render-check

WORKDIR /work
ENTRYPOINT ["/usr/local/bin/entrypoint"]
CMD ["dotnet", "test", "WolfCurses.sln", "-c", "Release"]
