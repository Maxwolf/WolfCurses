using System;
using System.Collections.Generic;
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
