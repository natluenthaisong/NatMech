using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

public class UmadP3BlackHoleSpotHintTests
{
    private const int DoubleTetherStrat = 2;

    private static UmadP3BlackHoleState State(ScenarioProbe p) =>
        ((UmadP3BlackHoleScenario)p.Game.ActiveScenario!).LastState!;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    [TestCase(1)]
    [TestCase(7)]
    public void FrozenPlayerStillSeesTheSpotTheStratSendsTheirSeatTo(int seed)
    {
        var checks = 0;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DoubleTetherStrat, seed,
            new ScenarioRunOptions
            {
                PlayerRole = PartyRole.MeleeDpsA,
                StopAt = 20f,
                Takeovers = [new Takeover(9f, new Vector2(0f, 0f))],
                Probe = p =>
                {
                    p.Game.GodMode = true;
                    var world = p.Game.World;
                    var player = world.Party.Player!;
                    if (p.Crossed(13f))
                    {
                        checks++;
                        var hint = UmadP3BlackHoleSpotHint.For(State(p).ScenarioObjects, world, player);
                        Assert.That(hint, Is.Not.Null);
                        Assert.That(Flat(hint!.Spot).Length(), Is.LessThan(1f), "everyone stacks in the middle");
                        Assert.That(hint.Stack, Is.True);
                    }
                    if (p.Crossed(19.5f))
                    {
                        checks++;
                        var hint = UmadP3BlackHoleSpotHint.For(State(p).ScenarioObjects, world, player);
                        Assert.That(hint, Is.Not.Null);
                        Assert.That(Vector2.Distance(Flat(hint!.Spot), Flat(player.Position)), Is.GreaterThan(5f),
                            "the player froze in the middle; the slap spot is out at the edge of the cones");
                        var partner = world.Party.Get(PartyRole.MeleeDpsB)!;
                        Assert.That(Vector2.Distance(Flat(hint.Spot), Flat(partner.PlannedDestination!.Value)), Is.LessThan(1f),
                            "the DPS share one Slap Happy spot");
                        Assert.That(hint.Stack, Is.True);
                    }
                },
            });
        Assert.That(checks, Is.EqualTo(2), run.ToString());
    }

    [Test]
    public void NoHintWhileTheStratHasTheSeatOnAMovingTarget()
    {
        var checks = 0;
        ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), DoubleTetherStrat, 1,
            new ScenarioRunOptions
            {
                PlayerRole = PartyRole.MainTank,
                StopAt = 27f,
                Probe = p =>
                {
                    if (!p.Crossed(26.3f)) return;
                    checks++;
                    var world = p.Game.World;
                    var support1 = State(p).Roles.Get(0)!;
                    Assert.That(support1.IsIntercepting || !support1.IsAlive(), Is.True, "support #1 is walking onto the solo tether");
                    Assert.That(UmadP3BlackHoleSpotHint.For(State(p).ScenarioObjects, world, support1), Is.Null,
                        "the tether guide shows the beam; a stale stack spot would point the wrong way");
                },
            });
        Assert.That(checks, Is.EqualTo(1));
    }
}
