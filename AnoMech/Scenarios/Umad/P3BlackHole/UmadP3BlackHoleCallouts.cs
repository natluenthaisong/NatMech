using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// The local player's tether calls, worded like cactbot's Black Hole calls and read off the same
// TetherGuides the on-screen guide draws. Next returns a call once, when it starts applying.
internal sealed class UmadP3BlackHoleCallouts
{
    private static readonly string[] Cardinals = ["North", "East", "South", "West"];

    private string? current;

    public void Reset() => current = null;

    public string? Next(UmadP3BlackHoleScenarioObjects objects, SimCharacter player)
    {
        var call = Call(objects, player);
        if (call == current) return null;
        current = call;
        return call;
    }

    public static string? Call(UmadP3BlackHoleScenarioObjects objects, SimCharacter player)
    {
        if (!objects.TetherGuides.TryGetValue(player, out var mine)) return null;
        var toTake = objects.Tethers
            .Where(t => t.A is { } hole && mine.Holes.Contains(hole) && !ReferenceEquals(t.B, player)
                        && !HandedOn(objects, player, mine, hole))
            .ToList();
        if (toTake.Count > 0)
            return mine.Holes.Count > 1 ? "Get both tethers" : $"Get {Compass(toTake[0].A!.Position)} tether";
        return MustPass(objects, player, mine) ? "Pass tether" : null;
    }

    // Only holes the strat gave this player count: a tether's random first holder isn't told to
    // pass it.
    public static bool MustPass(UmadP3BlackHoleScenarioObjects objects, SimCharacter player, TetherGuide mine) =>
        objects.Tethers.Any(t => ReferenceEquals(t.B, player) && t.A is { } hole && mine.Holes.Contains(hole)
                                 && HandedOn(objects, player, mine, hole));

    // A newer guide gave the hole to someone else; this player's own guide only lapses when the
    // strat next speaks to them (a return to the middle), so it can still name the hole.
    public static bool HandedOn(UmadP3BlackHoleScenarioObjects objects, SimCharacter player, TetherGuide mine, SimCharacter hole) =>
        objects.TetherGuides.Any(other =>
            !ReferenceEquals(other.Key, player) && other.Value.IssuedAt > mine.IssuedAt && other.Value.Holes.Contains(hole));

    private static string Compass(Vector3 position)
    {
        var quarterTurns = (int)MathF.Round(MathF.Atan2(position.X, -position.Z) / (MathF.PI / 2f));
        return Cardinals[(quarterTurns % 4 + 4) % 4];
    }
}
