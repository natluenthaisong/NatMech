using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public enum GuideColour { Take, Held, Spot, Hint }

// The guides as plain shapes, so one description feeds both ways of drawing them: the overlay
// (colours, text) and the game's own floor markers (one colour, no text). OverlayOnly: pointless
// or in the way as a floor marker.
public abstract record GuideShape(GuideColour Colour, bool OverlayOnly);
public sealed record GuideRing(Vector3 Centre, float Radius, GuideColour Colour, bool OverlayOnly = false)
    : GuideShape(Colour, OverlayOnly);
public sealed record GuideLine(Vector3 From, Vector3 To, GuideColour Colour, bool Thick = false, bool OverlayOnly = false)
    : GuideShape(Colour, OverlayOnly);
public sealed record GuideText(Vector3 At, string Text, GuideColour Colour) : GuideShape(Colour, OverlayOnly: true);

// A guide line drawn as ">" marks sliding toward its end, like a racing game's turn guide, so which
// way to go reads at a glance. Each mark is two arms, each from its back end to the mark's tip.
public static class GuideChevrons
{
    public const float Spacing = 1.5f;
    public const float Speed = 3f;
    public const float ArmLength = 0.45f;
    private const int MaxCount = 16;

    public static IEnumerable<(Vector3 Back, Vector3 Tip)> Arms(Vector3 from, Vector3 to, double seconds)
    {
        var along = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
        var length = along.Length();
        if (length < ArmLength * 2f) yield break;
        var forward = along / length;
        var side = new Vector3(-forward.Z, 0f, forward.X) * ArmLength;
        var count = 0;
        for (var at = (float)(seconds * Speed % Spacing); at <= length && count < MaxCount; at += Spacing)
        {
            if (at < ArmLength) continue;
            count++;
            var tip = from + forward * at;
            var back = tip - forward * ArmLength;
            yield return (back + side, tip);
            yield return (back - side, tip);
        }
    }
}

internal static class UmadP3BlackHoleGuideOverlay
{
    // ImGui packs colours as 0xAABBGGRR.
    private static uint Packed(GuideColour colour) => colour switch
    {
        GuideColour.Take => 0xFF00C8FF,
        GuideColour.Held => 0xFF50FF50,
        GuideColour.Spot => 0xFFFFDC00,
        _ => 0xFFFF60FF,
    };

    public static void Draw(IEnumerable<GuideShape> shapes, SimWorld world, double seconds)
    {
        var overlay = new WorldOverlay(world.Coordinates);
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case GuideRing ring: overlay.Circle(ring.Centre, ring.Radius, Packed(ring.Colour)); break;
                case GuideLine { Thick: true } beam: overlay.Line(beam.From, beam.To, Packed(beam.Colour), 5f); break;
                case GuideLine line:
                    foreach (var (back, tip) in GuideChevrons.Arms(line.From, line.To, seconds))
                        overlay.Line(back, tip, Packed(line.Colour), 3f);
                    break;
                case GuideText text: overlay.Text(text.At, text.Text, Packed(text.Colour)); break;
            }
        }
    }
}

// The guides on the floor with the game's "ally" omens, the friendly-coloured twins of the circle
// and line telegraphs, so they read as a marker rather than an attack. Markers are pooled and moved
// each frame instead of respawned.
internal sealed class UmadP3BlackHoleGroundGuide(SimWorld world)
{
    private const string CirclePath = "vfx/omen/eff/general_1bpf.avfx";
    private const string RectPath = "vfx/omen/eff/general02_pf.avfx";
    private const float ArmHalfWidth = 0.12f;
    private const float ThickLineHalfWidth = 0.3f;
    private const float ShortestLine = 0.3f;

    private readonly List<SimGroundMarker> rings = new();
    private readonly List<SimGroundMarker> lines = new();

    // tint: multiplies the omens' own colours (see Rainbow); null keeps them as the game draws them.
    // seconds: drives the chevrons' slide.
    public void Show(IEnumerable<GuideShape> shapes, double seconds, Vector4? tint = null)
    {
        var colour = tint ?? Vector4.One;
        int ringCount = 0, lineCount = 0;
        foreach (var shape in shapes)
        {
            if (shape.OverlayOnly) continue;
            switch (shape)
            {
                case GuideRing ring:
                    var ringMarker = Marker(rings, ringCount++, CirclePath);
                    ringMarker.Tint(colour);
                    ringMarker.Place(ring.Centre, 0f, new Vector3(ring.Radius, 1f, ring.Radius));
                    break;
                case GuideLine { Thick: true } beam:
                    PlaceBar(Marker(lines, lineCount++, RectPath), beam.From, beam.To, ThickLineHalfWidth, colour);
                    break;
                case GuideLine line:
                    foreach (var (back, tip) in GuideChevrons.Arms(line.From, line.To, seconds))
                        PlaceBar(Marker(lines, lineCount++, RectPath), back, tip, ArmHalfWidth, colour);
                    break;
            }
        }
        for (var i = ringCount; i < rings.Count; i++) rings[i].Hide();
        for (var i = lineCount; i < lines.Count; i++) lines[i].Hide();
    }

    private static void PlaceBar(SimGroundMarker marker, Vector3 from, Vector3 to, float halfWidth, Vector4 colour)
    {
        var along = to - from;
        var length = new Vector2(along.X, along.Z).Length();
        if (length < ShortestLine) { marker.Hide(); return; }
        marker.Tint(colour);
        marker.Place(from, MathF.Atan2(along.X, along.Z), new Vector3(halfWidth, 1f, length));
    }

    public void Clear()
    {
        foreach (var marker in rings) marker.Hide();
        foreach (var marker in lines) marker.Hide();
    }

    // A hue cycling every `period` seconds, scaled by `brightness`. The tint multiplies the omen's
    // own colour, so a fully saturated hue could black out a channel the omen leans on; Saturation
    // keeps some white in every colour.
    private const float Saturation = 0.8f;

    public static Vector4 Rainbow(double seconds, float brightness, double period = 2.0)
    {
        var hue = (float)(seconds / period % 1.0) * 6f;
        var x = 1f - MathF.Abs(hue % 2f - 1f);
        var (r, g, b) = (int)hue switch
        {
            0 => (1f, x, 0f),
            1 => (x, 1f, 0f),
            2 => (0f, 1f, x),
            3 => (0f, x, 1f),
            4 => (x, 0f, 1f),
            _ => (1f, 0f, x),
        };
        float Channel(float c) => (1f - Saturation + Saturation * c) * brightness;
        return new Vector4(Channel(r), Channel(g), Channel(b), 1f);
    }

    private SimGroundMarker Marker(List<SimGroundMarker> pool, int index, string path)
    {
        if (index == pool.Count) pool.Add(world.SpawnGroundMarker(path));
        return pool[index];
    }
}
