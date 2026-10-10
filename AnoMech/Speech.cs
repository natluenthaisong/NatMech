using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AnoMech;

// Windows' built-in SAPI voice, late-bound through COM so the plugin carries no speech package.
// Framework thread only. A missing or broken voice is logged once and then stays silent.
internal sealed class Speech : IDisposable
{
    // Queued, not purged: two calls landing in the same frame are both heard.
    private const int SpeakAsync = 1;

    private object? voice;
    private bool unavailable;

    public void Say(string text)
    {
        if (unavailable) return;
        try
        {
            voice ??= Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice", throwOnError: true)!);
            voice!.GetType().InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, [text, SpeakAsync]);
        }
        catch (Exception e)
        {
            unavailable = true;
            Core.DiagnosticLog.Warn($"[Speech] Windows voice unavailable, callouts stay text-only: {e.Message}");
        }
    }

    public void Dispose()
    {
        if (voice != null && Marshal.IsComObject(voice)) Marshal.FinalReleaseComObject(voice);
        voice = null;
    }
}
