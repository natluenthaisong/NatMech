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

    public static void Draw(IEnumerable<GuideShape> shapes, SimWorld world)
    {
        var overlay = new WorldOverlay(world.Coordinates);
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case GuideRing ring: overlay.Circle(ring.Centre, ring.Radius, Packed(ring.Colour)); break;
                case GuideLine line: overlay.Line(line.From, line.To, Packed(line.Colour), line.Thick ? 5f : 2f); break;
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
    private const float LineHalfWidth = 0.15f;
    private const float ThickLineHalfWidth = 0.3f;
    private const float ShortestLine = 0.3f;

    private readonly List<SimGroundMarker> rings = new();
    private readonly List<SimGroundMarker> lines = new();

    // tint: multiplies the omens' own colours (see Rainbow); null keeps them as the game draws them.
    public void Show(IEnumerable<GuideShape> shapes, Vector4? tint = null)
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
                case GuideLine line:
                    var along = line.To - line.From;
                    var length = new Vector2(along.X, along.Z).Length();
                    if (length < ShortestLine) break;
                    var halfWidth = line.Thick ? ThickLineHalfWidth : LineHalfWidth;
                    var lineMarker = Marker(lines, lineCount++, RectPath);
                    lineMarker.Tint(colour);
                    lineMarker.Place(line.From, MathF.Atan2(along.X, along.Z), new Vector3(halfWidth, 1f, length));
                    break;
            }
        }
        for (var i = ringCount; i < rings.Count; i++) rings[i].Hide();
        for (var i = lineCount; i < lines.Count; i++) lines[i].Hide();
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
