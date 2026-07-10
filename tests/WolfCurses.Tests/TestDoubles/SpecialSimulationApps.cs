using System;
using System.Collections.Generic;
using System.Reflection;
using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>Abstract window type: WindowFactory refuses to instantiate these with an ArgumentException.</summary>
    public abstract class AbstractTestWindow : Window<TestCommands, TestWindowData>
    {
        protected AbstractTestWindow(SimulationApp simUnit) : base(simUnit)
        {
        }
    }

    /// <summary>App whose only allowed window is abstract, to verify how WindowManager.Add surfaces the mistake.</summary>
    public sealed class AbstractWindowSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[] { typeof(AbstractTestWindow) };

        protected override void OnFirstTick()
        {
        }

        protected override void OnPreDestroy()
        {
        }

        public override string OnPreRender()
        {
            return string.Empty;
        }
    }

    /// <summary>Destroys itself from inside OnFirstTick, the earliest simulation callback, to prove that a
    ///     mid-tick teardown cannot crash the remainder of the tick.</summary>
    public sealed class SelfDestructingSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

        protected override void OnFirstTick()
        {
            Destroy();
        }

        protected override void OnPreDestroy()
        {
        }

        public override string OnPreRender()
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     WindowFactory keys its lookup by Type.Name, so two windows with the same short name collide even in
    ///     different namespaces. Constructing this app pins where that ArgumentException surfaces.
    /// </summary>
    public sealed class DuplicateNameSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[]
        {
            typeof(CloneNamespaceA.CloneWindow),
            typeof(CloneNamespaceB.CloneWindow)
        };

        protected override void OnFirstTick()
        {
        }

        protected override void OnPreDestroy()
        {
        }

        public override string OnPreRender()
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Mirrors the idiomatic consumer pattern (used by WolfCurses.Example and downstream apps): OnFirstTick attaches
    ///     the initial window by delegating to Restart(). Because Restart() used to unconditionally reset the tick
    ///     counter, this pattern re-entered OnFirstTick on every subsequent tick — endlessly clearing and re-adding the
    ///     window so no form could ever survive a tick. Counts first-tick fires and exposes the current window so the
    ///     regression test can assert the loop is gone and the window instance is stable.
    /// </summary>
    public sealed class RestartOnFirstTickSimulationApp : SimulationApp
    {
        public int FirstTickCount { get; private set; }

        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

        protected override void OnFirstTick()
        {
            FirstTickCount++;
            Restart();
        }

        public override void Restart()
        {
            base.Restart();
            WindowManager.Add(typeof(TestWindow));
        }

        protected override void OnPreDestroy()
        {
        }

        public override string OnPreRender()
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Exposes the seeded <see cref="SimulationApp" /> constructor so tests can prove the shared Randomizer is
    ///     reproducible when a seed is plumbed through the base class.
    /// </summary>
    public sealed class SeededSimulationApp : SimulationApp
    {
        public SeededSimulationApp(int seed) : base(seed)
        {
        }

        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

        protected override void OnFirstTick()
        {
        }

        protected override void OnPreDestroy()
        {
        }

        public override string OnPreRender()
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Overrides <see cref="SimulationApp.AdditionalFormAssemblies" /> so FormFactory folds an extra assembly into
    ///     form discovery, and counts how many times the hook is read so a test can prove the base constructor consumes
    ///     it. The extra assembly returned is this test assembly (same one auto-scanned), which also exercises the
    ///     de-duplication path — a genuinely separate third assembly is not available in-process.
    /// </summary>
    public sealed class AdditionalAssembliesSimulationApp : SimulationApp
    {
        public int AdditionalFormAssembliesReadCount { get; private set; }

        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

        public override IEnumerable<Assembly> AdditionalFormAssemblies
        {
            get
            {
                AdditionalFormAssembliesReadCount++;
                return new[] { GetType().Assembly };
            }
        }

        protected override void OnFirstTick()
        {
        }

        protected override void OnPreDestroy()
        {
        }

        public override string OnPreRender()
        {
            return string.Empty;
        }
    }
}

namespace WolfCurses.Tests.TestDoubles.CloneNamespaceA
{
    public class CloneWindow : WolfCurses.Window.Window<TestCommands, TestWindowData>
    {
        public CloneWindow(SimulationApp simUnit) : base(simUnit)
        {
        }
    }
}

namespace WolfCurses.Tests.TestDoubles.CloneNamespaceB
{
    public class CloneWindow : WolfCurses.Window.Window<TestCommands, TestWindowData>
    {
        public CloneWindow(SimulationApp simUnit) : base(simUnit)
        {
        }
    }
}
