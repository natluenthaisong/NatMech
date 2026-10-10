using System.Numerics;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;

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
                    Assert.That(callouts.Next(State(p).ScenarioObjects, Seat(p, 4)), Is.EqualTo("Get both tethers"));
                    Assert.That(callouts.Next(State(p).ScenarioObjects, Seat(p, 4)), Is.Null, "called once, not every frame");
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
                    if (callouts.Next(State(p).ScenarioObjects, Seat(p, 0)) is { } call) calls.Add(call);
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
