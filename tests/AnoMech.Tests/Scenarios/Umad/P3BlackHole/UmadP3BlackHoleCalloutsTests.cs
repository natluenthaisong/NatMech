using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Tests;

public class UmadP3BlackHoleCalloutsTests
{
    private const int DsaStrat = 0;
    private const int DoubleTetherStrat = 2;

    private static UmadP3BlackHoleState State(ScenarioProbe p) =>
        ((UmadP3BlackHoleScenario)p.Game.ActiveScenario!).LastState!;

    private static SimCharacter Seat(ScenarioProbe p, int seat) => State(p).Roles.Get(seat)!;

    private static string? CallFor(ScenarioProbe p, int seat) =>
        UmadP3BlackHoleCallouts.Call(State(p).ScenarioObjects, Seat(p, seat));

    private static string Compass(Vector3 p) =>
        MathF.Abs(p.X) > MathF.Abs(p.Z) ? (p.X > 0 ? "East" : "West") : (p.Z > 0 ? "South" : "North");

    private static List<string> TetherCalls(UmadP3BlackHoleCallouts callouts, ScenarioProbe p, int seat) =>
        callouts.Next(State(p), p.World, Seat(p, seat), State(p).Roles[seat])
                .Select(c => c.Text)
                .Where(text => text.Contains("tether"))
                .ToList();

    private static string SlapCall(uint slap) => slap == ActionId.SlapHappy_Left
        ? "Right => Party stack + Out of middle"
        : "Left => Role stacks + Out of middle";

    private static string ThunderCall(ThunderIIIAssignment plan, PartyRole role)
    {
        var (first, second) = ThunderIIIPlanning.Roles(plan);
        if (role == first) return second is null ? "Tank cleave on you x2" : "Tank cleave on you => Tank swap";
        return role == second ? "Tank swap after the first cleave" : "Away from Exdeath: tank cleaves";
    }

    [TestCase(PartyRole.MainTank, 9)]
    [TestCase(PartyRole.OffTank, 9)]
    [TestCase(PartyRole.MeleeDpsA, 9)]
    [TestCase(PartyRole.RegenHealer, 31)]
    public void MechanicCallsFollowTheFightInOrder(PartyRole role, int seed)
    {
        var callouts = new UmadP3BlackHoleCallouts();
        var calls = new List<string>();
        UmadP3BlackHoleState? state = null;
        string? lineCall = null;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DoubleTetherStrat, seed,
            new ScenarioRunOptions
            {
                Probe = p =>
                {
                    state = State(p);
                    var member = p.Member(role)!;
                    var number = member.HasStatus(StatusId.FirstInLine) ? 1
                               : member.HasStatus(StatusId.SecondInLine) ? 2
                               : member.HasStatus(StatusId.ThirdInLine) ? 3 : 0;
                    if (lineCall is null && number > 0)
                        lineCall = member.HasStatus(StatusId.Accretion) ? $"Number {number} + Accretion" : $"Number {number}";
                    calls.AddRange(callouts.Next(state, p.World, member, role)
                                           .Select(c => c.Text)
                                           .Where(text => !text.Contains("tether")));
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());

        var stacksFirst = role.IsDps() == state!.StackTargets[0].IsDps();
        Assert.That(calls, Is.EqualTo(new[]
        {
            lineCall,
            SlapCall(state.SlapAttacks[0]),
            ThunderCall(state.ThunderSet1, role),
            "Get behind Chaos",
            SlapCall(state.SlapAttacks[1]),
            "Get behind Chaos",
            "Out of middle",
            ThunderCall(state.ThunderSet2, role),
            state.ImplosionAttack == ActionId.LongitudinalImplosion ? "Sides => Front/Back" : "Front/Back => Sides",
            "Heal to full",
            SlapCall(state.SlapAttacks[2]),
            "Out of middle",
            "Bait puddles x2",
            stacksFirst ? "Stack middle => Towers" : "Towers => Stack middle",
            stacksFirst ? "Get towers" : "Stack middle",
            "Keep moving",
        }));
    }

    [TestCase(3)]
    [TestCase(11)]
    public void DoubleTethersCallBothTethersOnceForTheirOneTaker(int seed)
    {
        var checks = 0;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DoubleTetherStrat, seed,
            new ScenarioRunOptions
            {
                StopAt = 34f,
                Probe = p =>
                {
                    if (!p.Crossed(32.4f)) return;
                    checks++;
                    Assert.That(CallFor(p, 4), Is.EqualTo("Get both tethers"));
                    Assert.That(Enumerable.Range(0, 8).Where(seat => seat != 4).Select(seat => CallFor(p, seat)),
                        Has.None.EqualTo("Get both tethers"));
                    var callouts = new UmadP3BlackHoleCallouts();
                    Assert.That(TetherCalls(callouts, p, 4), Is.EqualTo(new[] { "Get both tethers" }));
                    Assert.That(TetherCalls(callouts, p, 4), Is.Empty, "called once, not every frame");
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(checks, Is.EqualTo(1));
    }

    [Test]
    public void APassedTetherIsNotCalledAgainBeforeTheReturnToMiddle()
    {
        var callouts = new UmadP3BlackHoleCallouts();
        var calls = new List<string>();
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DsaStrat, 5,
            new ScenarioRunOptions
            {
                StopAt = 75f,
                Probe = p =>
                {
                    calls.AddRange(TetherCalls(callouts, p, 0));
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(calls, Is.EqualTo(new[] { "Get East tether", "Get East tether", "Pass tether" }));
    }

    [TestCase(5)]
    [TestCase(23)]
    public void HolderIsToldToPassWhenTheStratHandsTheirTetherOn(int seed)
    {
        var checks = 0;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DsaStrat, seed,
            new ScenarioRunOptions
            {
                StopAt = 68f,
                Probe = p =>
                {
                    if (p.Crossed(63.9f))
                    {
                        checks++;
                        Assert.That(State(p).ScenarioObjects.Tethers.Any(t => ReferenceEquals(t.B, Seat(p, 4))), Is.True,
                            "precondition: DPS #1 holds the first wave-2 tether");
                        Assert.That(CallFor(p, 4), Is.Null, "nothing to do while holding");
                    }
                    if (p.Crossed(64.05f))
                    {
                        checks++;
                        var handedOn = State(p).ScenarioObjects.Tethers.Single(t => ReferenceEquals(t.B, Seat(p, 4))).A!;
                        Assert.That(CallFor(p, 4), Is.EqualTo("Pass tether"));
                        Assert.That(CallFor(p, 5), Is.EqualTo($"Get {Compass(handedOn.Position)} tether"));
                    }
                    if (p.Crossed(67f))
                    {
                        checks++;
                        Assert.That(CallFor(p, 4), Is.Null, "back in the middle");
                        Assert.That(CallFor(p, 5), Is.Null, "took it");
                    }
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(checks, Is.EqualTo(3));
    }
}
