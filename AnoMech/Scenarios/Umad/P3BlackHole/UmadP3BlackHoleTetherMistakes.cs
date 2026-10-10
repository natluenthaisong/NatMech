using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// Judges the local player's tether job at the moment a hole fires, against the strat's
// TetherGuides. Silent when no strat runs: nothing has been given to anyone then.
internal static class UmadP3BlackHoleTetherMistakes
{
    public static string? Check(UmadP3BlackHoleScenarioObjects objects, SimCharacter hole, SimCharacter? holder, SimCharacter player, int set)
    {
        var owner = Owner(objects, hole);
        var tether = $"{UmadP3BlackHoleCallouts.Compass(hole.Position)} tether";
        if (ReferenceEquals(owner, player) && !ReferenceEquals(holder, player))
            return $"Nothingness {set}: you weren't holding your {tether}";
        if (ReferenceEquals(holder, player) && owner is ISimPartyMember meantFor && !ReferenceEquals(owner, player))
            return $"Nothingness {set}: you were holding the {tether} meant for {Game.DescribeName(meantFor)}";
        return null;
    }

    // The newest guide naming the hole is the strat's current holder for it.
    private static SimCharacter? Owner(UmadP3BlackHoleScenarioObjects objects, SimCharacter hole)
    {
        SimCharacter? owner = null;
        var newest = float.MinValue;
        foreach (var (member, guide) in objects.TetherGuides)
        {
            if (!guide.Holes.Contains(hole) || guide.IssuedAt <= newest) continue;
            owner = member;
            newest = guide.IssuedAt;
        }
        return owner;
    }
}
