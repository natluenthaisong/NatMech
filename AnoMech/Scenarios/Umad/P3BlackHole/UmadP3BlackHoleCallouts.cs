using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// Alert: something to do right now (cactbot's alertText); otherwise a heads-up.
public sealed record Callout(string Text, bool Alert);

// One player's Black Hole calls, worded like cactbot's: their line number, each boss mechanic as
// its cast starts (Knock Down's second stack as the first one lands), and the tether jobs read
// off the strat's TetherGuides. Next returns each call once.
internal sealed class UmadP3BlackHoleCallouts
{
    private static readonly string[] Cardinals = ["North", "East", "South", "West"];

    private readonly Dictionary<SimEnemy, (int Cast, int Effect)> seen = new();
    private string? tetherCall;
    private bool lineCalled;
    private int thunderCasts;
    private int knockDowns;

    public void Reset()
    {
        seen.Clear();
        tetherCall = null;
        lineCalled = false;
        thunderCasts = 0;
        knockDowns = 0;
    }

    public List<Callout> Next(UmadP3BlackHoleState state, SimWorld world, SimCharacter player, PartyRole role)
    {
        var calls = new List<Callout>();
        if (!lineCalled && LineCall(player) is { } line)
        {
            lineCalled = true;
            calls.Add(new Callout(line, false));
        }
        foreach (var enemy in world.Children.OfType<SimEnemy>())
        {
            var casting = enemy.Casting;
            var last = seen.GetValueOrDefault(enemy);
            if (casting.CastSeq != last.Cast && CastCall(casting.ActionId, state, role) is { } cast) calls.Add(cast);
            if (casting.EffectSeq != last.Effect && EffectCall(casting.EffectActionId, state, role) is { } effect) calls.Add(effect);
            seen[enemy] = (casting.CastSeq, casting.EffectSeq);
        }
        var tether = Call(state.ScenarioObjects, player);
        if (tether is not null && tether != tetherCall) calls.Add(new Callout(tether, true));
        tetherCall = tether;
        return calls;
    }

    private static string? LineCall(SimCharacter player)
    {
        var number = player.HasStatus(StatusId.FirstInLine) ? 1
                   : player.HasStatus(StatusId.SecondInLine) ? 2
                   : player.HasStatus(StatusId.ThirdInLine) ? 3
                   : 0;
        if (number == 0) return null;
        return player.HasStatus(StatusId.Accretion) ? $"Number {number} + Accretion" : $"Number {number}";
    }

    private Callout? CastCall(uint actionId, UmadP3BlackHoleState state, PartyRole role) => actionId switch
    {
        ActionId.SlapHappy_Left => new Callout("Right => Party stack + Out of middle", true),
        ActionId.SlapHappy_Right => new Callout("Left => Role stacks + Out of middle", true),
        ActionId.ThunderIII_Cast => ThunderCall(++thunderCasts == 1 ? state.ThunderSet1 : state.ThunderSet2, role),
        ActionId.DamningEdict => new Callout("Get behind Chaos", false),
        ActionId.LookUponMeAndDespair or ActionId.LookUponMeAndDespair2 => new Callout("Out of middle", true),
        ActionId.LongitudinalImplosion => new Callout("Sides => Front/Back", false),
        ActionId.LatitudinalImplosion => new Callout("Front/Back => Sides", false),
        ActionId.WhiteHole => new Callout("Heal to full", true),
        ActionId.BlizzardIII_Cast => new Callout("Bait puddles x2", false),
        ActionId.KnockDown_Cast => new Callout(StacksFirst(state, role) ? "Stack middle => Towers" : "Towers => Stack middle", false),
        ActionId.BlizzardIII_Raidwide => new Callout("Keep moving", false),
        _ => null,
    };

    private Callout? EffectCall(uint actionId, UmadP3BlackHoleState state, PartyRole role)
    {
        if (actionId != ActionId.KnockDown || ++knockDowns != 1) return null;
        return new Callout(StacksFirst(state, role) ? "Get towers" : "Stack middle", true);
    }

    // The tank plan the bots follow (ThunderIIIPlanning): no second tank means the first takes
    // both hits.
    private static Callout ThunderCall(ThunderIIIAssignment plan, PartyRole role)
    {
        var (first, second) = ThunderIIIPlanning.Roles(plan);
        if (role == first)
            return new Callout(second is null ? "Tank cleave on you x2" : "Tank cleave on you => Tank swap", true);
        if (role == second)
            return new Callout("Tank swap after the first cleave", true);
        return new Callout("Away from Exdeath: tank cleaves", false);
    }

    // Stomp-a-Mole's groups: supports and DPS. Whoever holds the first stack marker's role stacks
    // first while the other group takes the towers.
    private static bool StacksFirst(UmadP3BlackHoleState state, PartyRole role) =>
        role.IsDps() == state.StackTargets[0].IsDps();

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

    internal static string Compass(Vector3 position)
    {
        var quarterTurns = (int)MathF.Round(MathF.Atan2(position.X, -position.Z) / (MathF.PI / 2f));
        return Cardinals[(quarterTurns % 4 + 4) % 4];
    }
}
