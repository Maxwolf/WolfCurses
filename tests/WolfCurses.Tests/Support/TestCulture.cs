using System.Globalization;
using System.Runtime.CompilerServices;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     TextProgress formats percentages with "N2", which is culture-dependent; pinning the invariant culture on
    ///     every test worker thread keeps those assertions stable on any machine locale.
    /// </summary>
    internal static class TestCulture
    {
        [ModuleInitializer]
        internal static void Init()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        }
    }
}
