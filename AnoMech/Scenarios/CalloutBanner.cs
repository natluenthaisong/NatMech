using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios;

// Scenario callouts the way cactbot shows them: large text near the top of the screen, newest on
// top, each for a few seconds; alerts in orange, mistakes in red. Drawn on the foreground list so an open NatMech
// window can't cover it. Game toasts were the other option, but they queue, and back-to-back
// calls arrived late.
internal sealed class CalloutBanner
{
    private const double ShowSeconds = 5;
    private const int MaxShown = 3;
    // ImGui packs colours as 0xAABBGGRR.
    private const uint AlertColor = 0xFF1EA0FF;
    private const uint InfoColor = 0xFFF0F0F0;
    private const uint MistakeColor = 0xFF4040FF;
    private const uint ShadowColor = 0xD9000000;

    private readonly List<(string Text, uint Color, double Until)> shown = new();

    public void Show(string text, bool alert, bool speak) => Add(text, alert ? AlertColor : InfoColor, speak);

    public void ShowMistake(string text, bool speak) => Add(text, MistakeColor, speak);

    private void Add(string text, uint color, bool speak)
    {
        shown.Insert(0, (text, color, ImGui.GetTime() + ShowSeconds));
        if (shown.Count > MaxShown) shown.RemoveAt(shown.Count - 1);
        if (speak) Plugin.Speech.Say(Spoken(text));
    }

    public void Clear() => shown.Clear();

    public void Draw()
    {
        var now = ImGui.GetTime();
        shown.RemoveAll(call => call.Until <= now);
        if (shown.Count == 0) return;

        var viewport = ImGui.GetMainViewport();
        var drawList = ImGui.GetForegroundDrawList();
        var font = ImGui.GetFont();
        var baseSize = ImGui.GetFontSize();
        var y = viewport.Pos.Y + viewport.Size.Y * 0.22f;
        for (var i = 0; i < shown.Count; i++)
        {
            var (text, color, _) = shown[i];
            var scale = i == 0 ? 2.2f : 1.5f;
            var extent = ImGui.CalcTextSize(text) * scale;
            var at = new Vector2(viewport.Pos.X + (viewport.Size.X - extent.X) / 2f, y);
            drawList.AddText(font, baseSize * scale, at + new Vector2(2f, 2f), ShadowColor, text, 0f);
            drawList.AddText(font, baseSize * scale, at, color, text, 0f);
            y += extent.Y + 6f;
        }
    }

    // The arrows and slashes read well on screen but the voice speaks them literally.
    private static string Spoken(string text) =>
        text.Replace(" => ", ", then ").Replace(" + ", ", ").Replace("/", " ").Replace("x2", "twice");
}
