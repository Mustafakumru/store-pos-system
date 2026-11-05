using System.Runtime.CompilerServices;
using LCWaikiki.POS.WpfClient.Core;

namespace LCWaikiki.POS.WpfClient.Tests;

internal static class TestSetup
{
    /// <summary>Testler gerçek uygulama log dosyasına (ProgramData) yazmasın.</summary>
    [ModuleInitializer]
    internal static void Init() => AppLog.Enabled = false;
}
