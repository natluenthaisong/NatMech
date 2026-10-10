using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// The test project's namespace: the linked fakes (FakeGame) call DalamudServices.Install unqualified.
namespace AnoMech.Tests;

// The app's counterpart of the test project's DalamudServices, without NUnit: code reaches Dalamud
// through Plugin's static services, which only Dalamud fills in, so stand-ins go in by reflection.
internal static class DalamudServices
{
    public static void InstallNullServices()
    {
        Install(nameof(Plugin.Log), DispatchProxy.Create<IPluginLog, NullService>());
        Install(nameof(Plugin.PluginInterface), DispatchProxy.Create<IDalamudPluginInterface, NullService>());
        Install(nameof(Plugin.ChatGui), DispatchProxy.Create<IChatGui, NullService>());
        Install(nameof(Plugin.ToastGui), DispatchProxy.Create<IToastGui, NullService>());
    }

    internal static void Install(string property, object service)
        => typeof(Plugin).GetProperty(property, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, service);

    public class NullService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method is { ReturnType.IsValueType: true } && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
