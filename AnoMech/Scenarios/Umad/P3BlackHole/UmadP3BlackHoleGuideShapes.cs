using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
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
// A head marker floating over a spot (Lockon sheet id), seen from anywhere in the arena. Floor only:
// the overlay already labels the spot.
public sealed record GuideBeacon(Vector3 At, uint LockonId) : GuideShape(GuideColour.Hint, OverlayOnly: false);

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

// The guides on the floor with the game's own pieces: the "ally" omens (the friendly-coloured twins
// of the circle telegraph) for spots, and Dancing Mad P1's Tele-portent arrows laid along each line,
// pointing at its end. Nothing is moved once placed (a moved world VFX either restarts or stays
// put), so a spot that changes is respawned and the arrows are re-laid only when the way to go turns.
internal sealed class UmadP3BlackHoleGroundGuide(SimWorld world)
{
    private const string CirclePath = "vfx/omen/eff/general_1bpf.avfx";

    private readonly List<SimGroundMarker> rings = new();
    private readonly List<ArrowTrail> trails = new();
    private readonly Beacon beacon = new(world);
    private int arrowsSpawned;

    // tint: multiplies the omens' own colours (see Rainbow); null keeps them as the game draws them.
    // spotTether: Channeling row for a tether from you to the beacon, 0 for none.
    public void Show(IEnumerable<GuideShape> shapes, Vector4? tint = null, ushort spotTether = 0, bool reverseTether = false)
    {
        var colour = tint ?? Vector4.One;
        int ringCount = 0, lineCount = 0;
        GuideBeacon? wantedBeacon = null;
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
                case GuideBeacon b:
                    wantedBeacon ??= b;
                    break;
                case GuideLine line:
                    if (lineCount == trails.Count) trails.Add(new ArrowTrail(this));
                    trails[lineCount++].Show(line.From, line.To);
                    break;
            }
        }
        for (var i = ringCount; i < rings.Count; i++) rings[i].Hide();
        for (var i = lineCount; i < trails.Count; i++) trails[i].Clear();
        beacon.Show(wantedBeacon, spotTether, reverseTether);
    }

    public void Clear()
    {
        foreach (var marker in rings) marker.Hide();
        foreach (var trail in trails) trail.Clear();
        beacon.Show(null, 0, false);
    }

    // An invisible helper standing on the spot wears the head marker: head markers only attach to
    // characters. A new symbol gets a new helper, since a second marker on one actor may not replace
    // the first; a spot that moves just moves the helper, and the marker goes with it. The helper is
    // also the far end of the tether to you; a beam's visual lives on its source end, so neither way
    // round touches the slot of a Black Hole tether you hold (the hole is that one's source).
    private sealed class Beacon(SimWorld world)
    {
        // The engine creates the actor a few frames after the spawn; a marker or tether sent before
        // that is lost.
        private const int FramesBeforeMarker = 15;
        private const float MoveTolerance = 0.3f;
        // Within this of the spot, Closer turns into its "close enough" version.
        private const float ArrivedRadius = UmadP3BlackHoleSpotHint.SpotRadius + 0.5f;

        private SimEnemy? helper;
        private uint lockonId;
        private int framesUntilMarker;
        private SimTether? tether;
        private (ushort Id, bool Reversed) tetherShown;

        public void Show(GuideBeacon? wanted, ushort spotTether, bool reverseTether)
        {
            if (wanted is null) { Despawn(); return; }
            if (helper is null || !helper.IsActive || wanted.LockonId != lockonId)
            {
                Despawn();
                lockonId = wanted.LockonId;
                helper = world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: UmadConstants.BNpcBaseId.KefkaHelper, NameId: UmadConstants.BNpcNameId.Kefka, Level: 1,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
                    Placement: new Placement(wanted.At, 0f), LocalOnly: true));
                framesUntilMarker = FramesBeforeMarker;
                return;
            }
            if (Vector3.Distance(helper.Position, wanted.At) > MoveTolerance) helper.SetPosition(wanted.At);
            if (framesUntilMarker > 0 && --framesUntilMarker == 0) helper.AttachLockonVfx(lockonId);
            if (framesUntilMarker == 0) ShowTether(helper, spotTether, reverseTether);
        }

        private void ShowTether(SimEnemy spot, ushort id, bool reversed)
        {
            var player = world.Party.Player;
            if (player is null || !player.IsAlive()) id = 0;
            else if (id == UmadConstants.TetherId.Closer
                     && Vector2.Distance(new(player.Position.X, player.Position.Z), new(spot.Position.X, spot.Position.Z)) <= ArrivedRadius)
                id = UmadConstants.TetherId.CloserClear;
            if (tether is { IsActive: true } && tetherShown == (id, reversed)) return;
            ClearTether();
            if (id == 0) return;
            tether = reversed ? world.Tether(player, spot, id) : world.Tether(spot, player, id);
            tetherShown = (id, reversed);
        }

        private void ClearTether()
        {
            tether?.Despawn();
            tether = null;
        }

        private void Despawn()
        {
            ClearTether();
            helper?.Despawn();
            helper = null;
        }
    }

    private SimEventObject? SpawnArrow(Vector3 at, float heading)
    {
        var serial = arrowsSpawned++;
        return world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = UmadConstants.EObjId.TelePortent,
            Placement = new Placement(at, heading),
            EventId = UmadConstants.EObjId.PropEventId,
            EntityId = UmadConstants.Spawn.GuideArrowEntityIdBase + (uint)(serial % 0x30),
            Arg2 = ((uint)(UmadConstants.Spawn.GuideArrowArg2Serial + serial % 0x30) << 16) | 0x3U,
            LocalOnly = true,
        });
    }

    // Arrows counted back from the line's end, so walking straight at it only drops the ones behind
    // you; a turn past TurnTolerance, or a new end, lays the trail again.
    private sealed class ArrowTrail(UmadP3BlackHoleGroundGuide owner)
    {
        public const float Spacing = 5f;
        public const float FirstBack = 2.5f;
        private const int MaxArrows = 3;
        private const float TurnTolerance = 15f * MathF.PI / 180f;
        private const float EndTolerance = 0.5f;
        // An arrow this close to (or behind) the player has been passed.
        private const float PassedMargin = 1.5f;

        private readonly List<SimEventObject?> arrows = new();
        private Vector3 end;
        private float heading;
        private bool laid;

        public void Show(Vector3 from, Vector3 to)
        {
            var along = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
            var length = along.Length();
            if (length < FirstBack + PassedMargin) { Clear(); return; }
            var newHeading = MathF.Atan2(along.X, along.Z);
            if (!laid || Vector3.Distance(end, to) > EndTolerance
                      || MathF.Abs(MathUtil.NormalizeRotation(newHeading - heading)) > TurnTolerance)
            {
                Clear();
                end = to;
                heading = newHeading;
                laid = true;
                var forward = new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading));
                for (var k = 0; k < MaxArrows; k++)
                {
                    var back = FirstBack + k * Spacing;
                    if (back > length - PassedMargin) break;
                    arrows.Add(owner.SpawnArrow(end - forward * back, heading));
                }
                return;
            }
            for (var k = 0; k < arrows.Count; k++)
            {
                if (arrows[k] is not { } arrow || FirstBack + k * Spacing <= length - PassedMargin) continue;
                arrow.Despawn();
                arrows[k] = null;
            }
        }

        public void Clear()
        {
            foreach (var arrow in arrows) arrow?.Despawn();
            arrows.Clear();
            laid = false;
        }
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
