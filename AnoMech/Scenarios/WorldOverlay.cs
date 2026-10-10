using System;
using System.Numerics;
using AnoMech.Core.Game;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios;

// Draws on the ground of the arena, in scenario-local coordinates, onto ImGui's background
// list (over the game, under every window). Call only from a UiBuilder.Draw handler.
internal readonly struct WorldOverlay(Coordinates coordinates)
{
    // Long lines are cut into pieces and only the pieces in front of the camera are drawn, so a
    // line running behind the camera doesn't project into a streak across the screen.
    private const float SegmentLength = 1f;
    private const int MaxSegments = 64;
    private const int CircleSegments = 48;
    // ImGui packs colours as 0xAABBGGRR.
    private const uint TextShadowColor = 0xD9000000;

    public void Line(Vector3 from, Vector3 to, uint color, float thickness = 3f)
    {
        var segments = Math.Clamp((int)MathF.Ceiling(Vector3.Distance(from, to) / SegmentLength), 1, MaxSegments);
        var drawList = ImGui.GetBackgroundDrawList();
        var prevInFront = Project(from, out var prev);
        for (var i = 1; i <= segments; i++)
        {
            var inFront = Project(Vector3.Lerp(from, to, i / (float)segments), out var next);
            if (prevInFront && inFront) drawList.AddLine(prev, next, color, thickness);
            prev = next;
            prevInFront = inFront;
        }
    }

    public void Circle(Vector3 centre, float radius, uint color, float thickness = 3f)
    {
        var drawList = ImGui.GetBackgroundDrawList();
        var prevInFront = Project(centre + new Vector3(radius, 0f, 0f), out var prev);
        for (var i = 1; i <= CircleSegments; i++)
        {
            var angle = MathF.Tau * i / CircleSegments;
            var inFront = Project(centre + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius), out var next);
            if (prevInFront && inFront) drawList.AddLine(prev, next, color, thickness);
            prev = next;
            prevInFront = inFront;
        }
    }

    public void Text(Vector3 at, string text, uint color)
    {
        if (!Project(at, out var screen)) return;
        var drawList = ImGui.GetBackgroundDrawList();
        var topLeft = screen - ImGui.CalcTextSize(text) / 2f;
        drawList.AddText(topLeft + new Vector2(1f, 1f), TextShadowColor, text);
        drawList.AddText(topLeft, color, text);
    }

    private bool Project(Vector3 local, out Vector2 screen) =>
        Plugin.GameGui.WorldToScreen(coordinates.ToGlobal(local), out screen, out _);
}
