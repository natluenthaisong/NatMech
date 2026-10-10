using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// Holes: the black holes whose tethers the strat gives this player. HoldAt: where to stand once
// they hold them; null while the strat has only said "take it". IssuedAt: scenario time; when two
// players' guides name the same hole, the newer one is the strat's current owner.
public sealed record TetherGuide(IReadOnlyList<SimCharacter> Holes, Vector3? HoldAt, float IssuedAt);

// On-screen hint for the local player, read from what the selected strat told their seat: their
// tethers highlighted, a line to the nearest point of a beam they still have to step on, and the
// spot to hold once they have it.
internal static class UmadP3BlackHoleTetherGuide
{
    // ImGui packs colours as 0xAABBGGRR.
    private const uint TakeColor = 0xFF00C8FF;
    private const uint HeldColor = 0xFF50FF50;
    private const uint SpotColor = 0xFFFFDC00;

    private const float HoleRingRadius = 2f;
    // Keeps the step-in point out of the hole's own hitbox (1.25y) at the beam's far end.
    private const float BeamHoleClearance = 2.5f;
    private const float HoldSpotRadius = 1.5f;
    private const float TextHeight = 2.6f;

    public static void Draw(UmadP3BlackHoleScenarioObjects objects, SimWorld world)
    {
        if (world.Party.Player is not { } player || !player.IsAlive()) return;
        if (!objects.TetherGuides.TryGetValue(player, out var guide)) return;
        var mine = objects.Tethers
            .Where(t => t.A is { } hole && guide.Holes.Contains(hole)
                        && (ReferenceEquals(t.B, player) || !UmadP3BlackHoleCallouts.HandedOn(objects, player, guide, hole)))
            .ToList();
        if (mine.Count == 0) return;

        var overlay = new WorldOverlay(world.Coordinates);
        foreach (var tether in mine)
        {
            var color = ReferenceEquals(tether.B, player) ? HeldColor : TakeColor;
            overlay.Circle(tether.A!.Position, HoleRingRadius, color);
            if (tether.B is { } holder) overlay.Line(tether.A.Position, holder.Position, color, 5f);
        }

        var head = player.Position + new Vector3(0f, TextHeight, 0f);
        var toTake = mine.Where(t => !ReferenceEquals(t.B, player)).ToList();
        if (toTake.Count > 0)
        {
            var stepIn = toTake.Select(t => NearestPointOnBeam(t, player.Position))
                               .MinBy(p => Vector3.DistanceSquared(p, player.Position));
            overlay.Line(player.Position, stepIn, TakeColor, 2f);
            overlay.Text(head, guide.Holes.Count > 1 ? "Take both tethers" : "Take tether", TakeColor);
            return;
        }

        var passing = UmadP3BlackHoleCallouts.MustPass(objects, player, guide);
        if (passing) overlay.Text(head, "Pass tether", TakeColor);
        if (guide.HoldAt is not { } spot) return;
        overlay.Circle(spot, HoldSpotRadius, SpotColor);
        if (Vector2.Distance(Flat(player.Position), Flat(spot)) <= HoldSpotRadius) return;
        overlay.Line(player.Position, spot, SpotColor, 2f);
        if (!passing) overlay.Text(head, "Hold here", SpotColor);
    }

    private static Vector3 NearestPointOnBeam(SimTether tether, Vector3 from)
    {
        var a = tether.A!.Position;
        var b = tether.B?.Position ?? a;
        var ab = Flat(b) - Flat(a);
        var lengthSquared = ab.LengthSquared();
        if (lengthSquared < 1e-4f) return a;
        var minT = MathF.Min(1f, BeamHoleClearance / MathF.Sqrt(lengthSquared));
        var t = Math.Clamp(Vector2.Dot(Flat(from) - Flat(a), ab) / lengthSquared, minT, 1f);
        return Vector3.Lerp(a, b, t);
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
