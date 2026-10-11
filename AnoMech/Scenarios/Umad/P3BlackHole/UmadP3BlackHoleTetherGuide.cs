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

// What the selected strat told the local player's seat about tethers: their tethers highlighted,
// a line to the nearest point of a beam they still have to step on, and the spot to hold once they
// have it.
internal static class UmadP3BlackHoleTetherGuide
{
    private const float HoleRingRadius = 2f;
    // Keeps the step-in point out of the hole's own hitbox (1.25y) at the beam's far end.
    private const float BeamHoleClearance = 2.5f;
    private const float HoldSpotRadius = 1.5f;
    private const float TextHeight = 2.6f;

    public static void AddShapes(List<GuideShape> shapes, UmadP3BlackHoleScenarioObjects objects, SimWorld world)
    {
        if (world.Party.Player is not { } player || !player.IsAlive()) return;
        if (!objects.TetherGuides.TryGetValue(player, out var guide)) return;
        var mine = objects.Tethers
            .Where(t => t.A is { } hole && guide.Holes.Contains(hole)
                        && (ReferenceEquals(t.B, player) || !UmadP3BlackHoleCallouts.HandedOn(objects, player, guide, hole)))
            .ToList();
        if (mine.Count == 0) return;

        foreach (var tether in mine)
        {
            var held = ReferenceEquals(tether.B, player);
            var colour = held ? GuideColour.Held : GuideColour.Take;
            // The game already draws a beam you hold; a ring around it would only cover it.
            shapes.Add(new GuideRing(tether.A!.Position, HoleRingRadius, colour, OverlayOnly: held));
            if (tether.B is { } holder) shapes.Add(new GuideLine(tether.A.Position, holder.Position, colour, Thick: true, OverlayOnly: true));
        }

        var head = player.Position + new Vector3(0f, TextHeight, 0f);
        var toTake = mine.Where(t => !ReferenceEquals(t.B, player)).ToList();
        if (toTake.Count > 0)
        {
            var stepIn = toTake.Select(t => NearestPointOnBeam(t, player.Position))
                               .MinBy(p => Vector3.DistanceSquared(p, player.Position));
            shapes.Add(new GuideLine(player.Position, stepIn, GuideColour.Take));
            shapes.Add(new GuideText(head, guide.Holes.Count > 1 ? "Take both tethers" : "Take tether", GuideColour.Take));
            return;
        }

        var passing = UmadP3BlackHoleCallouts.MustPass(objects, player, guide);
        if (passing) shapes.Add(new GuideText(head, "Pass tether", GuideColour.Take));
        if (guide.HoldAt is not { } spot) return;
        shapes.Add(new GuideRing(spot, HoldSpotRadius, GuideColour.Spot));
        if (Vector2.Distance(Flat(player.Position), Flat(spot)) <= HoldSpotRadius) return;
        shapes.Add(new GuideLine(player.Position, spot, GuideColour.Spot));
        if (!passing) shapes.Add(new GuideText(head, "Hold here", GuideColour.Spot));
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
