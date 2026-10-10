using System.Reflection;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace AnoMech.Standalone.Host;

// A Dalamud chat/toast stand-in that hands every printed or shown message to Sink, so deaths and
// mistakes the sim posts to chat reach the app's log. Everything else is a no-op.
internal class CaptureService : DispatchProxy
{
    public Action<string>? Sink { get; set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is { Name: var name } && (name.StartsWith("Print") || name.StartsWith("Show")) && args is [var first, ..])
        {
            var text = first switch
            {
                string s => s,
                XivChatEntry entry => entry.Message.TextValue,
                SeString se => se.TextValue,
                _ => null,
            };
            if (text is not null) Sink?.Invoke(text);
        }
        return method is { ReturnType.IsValueType: true } && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }

    public static T Create<T>(Action<string> sink) where T : class
    {
        var proxy = DispatchProxy.Create<T, CaptureService>();
        ((CaptureService)(object)proxy).Sink = sink;
        return proxy;
    }
}
