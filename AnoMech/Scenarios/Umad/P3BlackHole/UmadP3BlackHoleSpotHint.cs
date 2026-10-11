using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public sealed record SpotHint(Vector3 Spot, bool Stack);

// Where the selected strat is sending the local player's seat right now (the move a bot in that
// seat would make), so you can see the safe spot or stack spot the way Splatoon draws it. Stack:
// at least two teammates are headed to the same spot.
public static class UmadP3BlackHoleSpotHint
{
    public const float SpotRadius = 1f;
    private const float StackRadius = 3f;
    private const int StackPartners = 2;
    // The tether guide already draws its hold spot.
    private const float SameAsHoldSpot = 2f;

    public static SpotHint? For(UmadP3BlackHoleScenarioObjects objects, SimWorld world, SimCharacter player)
    {
        if (!player.IsAlive() || player.IsIntercepting || player.PlannedDestination is not { } spot) return null;
        if (objects.TetherGuides.TryGetValue(player, out var guide)
            && guide.HoldAt is { } hold && Vector2.Distance(Flat(hold), Flat(spot)) < SameAsHoldSpot)
            return null;
        var partners = 0;
        for (var i = 0; i < 8; i++)
        {
            var member = world.Party.Get(i);
            if (member is null || ReferenceEquals(member, player) || !member.IsAlive()) continue;
            if (member.PlannedDestination is { } theirs && Vector2.Distance(Flat(theirs), Flat(spot)) <= StackRadius) partners++;
        }
        return new SpotHint(spot, partners >= StackPartners);
    }

    public static string Label(SpotHint hint) => hint.Stack ? "Stack" : "Safe spot";

    private const float TextHeight = 1.2f;

    internal static void AddShapes(List<GuideShape> shapes, UmadP3BlackHoleScenarioObjects objects, SimWorld world)
    {
        if (world.Party.Player is not { } player || For(objects, world, player) is not { } hint) return;
        shapes.Add(new GuideRing(hint.Spot, SpotRadius, GuideColour.Hint));
        shapes.Add(new GuideBeacon(hint.Spot, hint.Stack ? UmadConstants.LockonId.PlaystationTriangle : UmadConstants.LockonId.PlaystationCircle));
        shapes.Add(new GuideText(hint.Spot + new Vector3(0f, TextHeight, 0f), Label(hint), GuideColour.Hint));
        if (Vector2.Distance(Flat(player.Position), Flat(hint.Spot)) > SpotRadius)
            shapes.Add(new GuideLine(player.Position, hint.Spot, GuideColour.Hint));
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
