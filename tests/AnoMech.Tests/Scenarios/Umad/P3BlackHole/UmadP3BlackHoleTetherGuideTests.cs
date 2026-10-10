using System.Numerics;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

public class UmadP3BlackHoleTetherGuideTests
{
    private const int DsaStrat = 0;
    private const int DoubleTetherStrat = 2;

    private static UmadP3BlackHoleState State(ScenarioProbe p) =>
        ((UmadP3BlackHoleScenario)p.Game.ActiveScenario!).LastState!;

    private static TetherGuide? GuideOf(ScenarioProbe p, int seat) =>
        State(p).Roles.Get(seat) is { } member && State(p).ScenarioObjects.TetherGuides.TryGetValue(member, out var guide)
            ? guide
            : null;

    private static IEnumerable<SimCharacter> ActiveHoles(ScenarioProbe p) =>
        State(p).ScenarioObjects.Tethers.Select(t => t.A!);

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(42)]
    public void DoubleTethersGuideOneSeatOntoBothPairTethersThenToAHoldSpot(int seed)
    {
        var checks = 0;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DoubleTetherStrat, seed,
            new ScenarioRunOptions
            {
                StopAt = 131f,
                Probe = p =>
                {
                    if (p.Crossed(26.5f))
                    {
                        checks++;
                        Assert.That(GuideOf(p, 0)?.Holes, Is.EquivalentTo(ActiveHoles(p)), "support #1 takes the solo tether");
                        Assert.That(GuideOf(p, 4), Is.Null, "DPS #1 waits for the pair");
                    }
                    if (p.Crossed(33f) || p.Crossed(126.5f))
                    {
                        checks++;
                        var seat = p.Time < 100f ? 4 : 2;
                        Assert.That(GuideOf(p, seat)?.Holes, Is.EquivalentTo(ActiveHoles(p)).And.Count.EqualTo(2));
                        Assert.That(State(p).ScenarioObjects.TetherGuides.Values.Count(g => g.Holes.Count == 2), Is.EqualTo(1));
                    }
                    if (p.Crossed(38.5f) || p.Crossed(130.3f))
                    {
                        checks++;
                        var seat = p.Time < 100f ? 4 : 2;
                        Assert.That(GuideOf(p, seat)?.HoldAt, Is.Not.Null, "both held, so the guide moves on to the hold spot");
                        var holes = ActiveHoles(p).ToList();
                        var midpoint = (holes[0].Position + holes[1].Position) / 2f;
                        Assert.That(Vector2.Distance(new(GuideOf(p, seat)!.HoldAt!.Value.X, GuideOf(p, seat)!.HoldAt!.Value.Z), new(midpoint.X, midpoint.Z)),
                            Is.LessThan(4f), "held at the midpoint between the two holes, give or take a nudge off a passive hole");
                    }
                    if (p.Crossed(35f))
                    {
                        checks++;
                        Assert.That(GuideOf(p, 0), Is.Null, "support #1 is sent back to the middle");
                    }
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(checks, Is.EqualTo(6));
    }

    [TestCase(1)]
    [TestCase(42)]
    public void SingleTetherStratsNeverGuideOneSeatOntoTwoTethers(int seed)
    {
        var sawGuide = false;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DsaStrat, seed,
            new ScenarioRunOptions
            {
                StopAt = 138f,
                Probe = p =>
                {
                    var guides = State(p).ScenarioObjects.TetherGuides.Values;
                    sawGuide |= guides.Count > 0;
                    Assert.That(guides.All(g => g.Holes.Count <= 1), Is.True, $"at {p.Time:F2}s");
                    if (p.Crossed(26.5f))
                        Assert.That(GuideOf(p, 4)?.Holes, Is.EquivalentTo(ActiveHoles(p)), "DPS #1 takes the solo tether");
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(sawGuide, Is.True);
    }
}
