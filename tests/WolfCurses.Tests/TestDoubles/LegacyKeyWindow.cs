using System;
using System.Collections.Generic;
using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     A window overriding only the bare-<see cref="ConsoleKey" /> key press overload, the way every window written
    ///     before <see cref="ConsoleKeyInfo" /> travelled the queue did. It records what it hears and calls base — the
    ///     compatibility contract under test is that such an override keeps firing exactly when it always did, and that
    ///     its base call still delivers the <i>full</i> key info to the form underneath rather than a reconstruction
    ///     with the character stripped.
    /// </summary>
    public class LegacyKeyWindow : Window<TestCommandsEnum, TestWindowData>
    {
        public LegacyKeyWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        public List<ConsoleKey> ReceivedKeys { get; } = new();

        public override void OnKeyPressed(ConsoleKey key)
        {
            ReceivedKeys.Add(key);
            base.OnKeyPressed(key);
        }
    }
}
