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

    // ImGui packs colours as 0xAABBGGRR.
    private const uint SpotColor = 0xFFFF60FF;
    private const float TextHeight = 1.2f;

    internal static void Draw(UmadP3BlackHoleScenarioObjects objects, SimWorld world)
    {
        if (world.Party.Player is not { } player || For(objects, world, player) is not { } hint) return;
        var overlay = new WorldOverlay(world.Coordinates);
        overlay.Circle(hint.Spot, SpotRadius, SpotColor);
        overlay.Text(hint.Spot + new Vector3(0f, TextHeight, 0f), Label(hint), SpotColor);
        if (Vector2.Distance(Flat(player.Position), Flat(hint.Spot)) > SpotRadius)
            overlay.Line(player.Position, hint.Spot, SpotColor, 2f);
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
