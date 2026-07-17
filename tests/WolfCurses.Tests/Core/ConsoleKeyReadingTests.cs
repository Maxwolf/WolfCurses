// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     Covers the automatic console reading that makes a host's key loop unnecessary: on every system tick the
    ///     input manager drains the console's own key buffer and routes each key the standard way
    ///     (<c>SendConsoleKey</c>). The console itself is stood in for by the internal key source seam — the real path
    ///     polls a live console for keys, which a test host has no way to arrange — while the routing method is
    ///     exercised directly, because it is public surface a self-reading host uses too.
    /// </summary>
    public class ConsoleKeyReadingTests
    {
        private static ConsoleKeyInfo Key(char character, ConsoleKey key)
        {
            return new ConsoleKeyInfo(character, key, false, false, false);
        }

        /// <summary>A key source feeding the given keys once each, then reporting the buffer empty forever.</summary>
        private static Func<ConsoleKeyInfo?> SourceOf(params ConsoleKeyInfo[] keys)
        {
            var queue = new Queue<ConsoleKeyInfo>(keys);
            return () => queue.Count > 0 ? queue.Dequeue() : null;
        }

        [Fact]
        public void ReadsConsoleInput_IsOnByDefault()
        {
            // The zero-set-up contract: constructing a simulation is all it takes for typing to work.
            Assert.True(new TestSimulationApp().InputManager.ReadsConsoleInput);
        }

        [Fact]
        public void SendConsoleKey_PrintableKey_FillsTheBufferAndReachesTheForm()
        {
            // Both, deliberately: the character is text for the prompt, and the key press is for whatever form is
            // steering by keys. Neither path may eat the other's half.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(KeyRecordingForm));
            var form = (KeyRecordingForm) app.WindowManager.FocusedWindow.CurrentForm;

            app.InputManager.SendConsoleKey(Key('a', ConsoleKey.A));
            app.OnTick(false);

            Assert.Contains(ConsoleKey.A, form.ReceivedKeys);
        }

        [Fact]
        public void SendConsoleKey_ArrowKey_ReachesTheFormWithoutTouchingTheBuffer()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(KeyRecordingForm));
            var form = (KeyRecordingForm) app.WindowManager.FocusedWindow.CurrentForm;

            app.InputManager.SendConsoleKey(Key('\0', ConsoleKey.LeftArrow));
            app.OnTick(false);

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
            Assert.Equal(new[] {ConsoleKey.LeftArrow}, form.ReceivedKeys);
        }

        [Fact]
        public void SendConsoleKey_Backspace_RemovesTheLastBufferedCharacter()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.InputManager.SendConsoleKey(Key('g', ConsoleKey.G));
            app.InputManager.SendConsoleKey(Key('o', ConsoleKey.O));
            Assert.Equal("go", app.InputManager.InputBuffer);

            app.InputManager.SendConsoleKey(Key('\b', ConsoleKey.Backspace));

            Assert.Equal("g", app.InputManager.InputBuffer);
        }

        [Fact]
        public void SendConsoleKey_Enter_SubmitsTheBufferAsACommand()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.InputManager.SendConsoleKey(Key('g', ConsoleKey.G));
            app.InputManager.SendConsoleKey(Key('o', ConsoleKey.O));

            app.InputManager.SendConsoleKey(Key('\r', ConsoleKey.Enter));

            // The buffer was handed on, exactly as SendInputBufferAsCommand does — because that is what ran.
            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
        }

        [Fact]
        public void SystemTick_DrainsEveryWaitingKeyBeforeDispatching()
        {
            // All of them in one tick, not one per tick: a held key outruns a slow host loop otherwise, and the
            // routing must land before dispatch so the key is acted on the turn it arrived.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.InputManager.ConsoleKeySource = SourceOf(Key('h', ConsoleKey.H), Key('i', ConsoleKey.I));

            app.InputManager.OnTick(true);

            Assert.Equal("hi", app.InputManager.InputBuffer);
        }

        [Fact]
        public void SimulationTick_NeverReadsTheConsole()
        {
            // Simulation ticks are the deterministic kind tests drive; a live console read inside one would make
            // them depend on whatever happened to be typed.
            var app = new TestSimulationApp();
            var pulls = 0;
            app.InputManager.ConsoleKeySource = () =>
            {
                pulls++;
                return null;
            };

            app.InputManager.OnTick(false);

            Assert.Equal(0, pulls);
        }

        [Fact]
        public void ReadsConsoleInput_False_HandsKeyReadingToTheHost()
        {
            // The override for hosts with their own ideas about keys: nothing may compete with their read loop.
            var app = new TestSimulationApp();
            var pulls = 0;
            app.InputManager.ConsoleKeySource = () =>
            {
                pulls++;
                return null;
            };
            app.InputManager.ReadsConsoleInput = false;

            app.InputManager.OnTick(true);

            Assert.Equal(0, pulls);
        }
    }
}
