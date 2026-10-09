using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios.Umad.P3BlackHole;
using static AnoMech.Scenarios.Umad.P3BlackHole.UmadP3BlackHoleAi;
using StatusId = AnoMech.Scenarios.Umad.UmadConstants.StatusId;

namespace AnoMech.Tests;

public class UmadP3BlackHoleMarkersTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void RandomizedDebuffsReceiveLineMarkersWithAccretionLast(int strat)
    {
        for (var seed = 0; seed < 12; seed++)
        {
            var observed = false;
            var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), strat, seed,
                new ScenarioRunOptions
                {
                    StopAt = 10f,
                    Probe = p =>
                    {
                        if (!p.Crossed(9.2f)) return;
                        observed = true;
                        var marks = p.World.PartyMarkers;
                        Assert.That(marks, Has.Count.EqualTo(8));
                        Assert.That(marks.Values.Distinct().Count(), Is.EqualTo(8));
                        foreach (var (role, sign) in marks)
                        {
                            var member = p.Member(role)!;
                            var line = sign switch
                            {
                                Sign.Attack1 or Sign.Attack2 or Sign.Attack3 => StatusId.FirstInLine,
                                Sign.Bind1 or Sign.Bind2 or Sign.Bind3 => StatusId.SecondInLine,
                                _ => StatusId.ThirdInLine,
                            };
                            Assert.That(member.HasStatus(line), Is.True, $"{role} has {sign}");
                            Assert.That(member.HasStatus(StatusId.Accretion),
                                Is.EqualTo(sign is Sign.Attack3 or Sign.Bind3));
                            if (!member.HasStatus(StatusId.Accretion))
                            {
                                var first = sign is Sign.Attack1 or Sign.Bind1 or Sign.Ignore1;
                                Assert.That(role.IsDps(), Is.EqualTo(strat == 1 ? !first : first));
                            }
                            Assert.That(((FakeMarkings)Natives.Markings).Marks[sign],
                                Is.EqualTo(member.GameObjectId));
                        }
                    },
                });
            Assert.That(run.Passed, Is.True, run.ToString());
            Assert.That(observed, Is.True);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void MarksPersistUntilFinalShotThenClear(int strat)
    {
        var afterLastShot = false;
        var cleared = false;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), strat, 123,
            new ScenarioRunOptions
            {
                StopAt = 139f,
                Probe = p =>
                {
                    if (p.Crossed(138f))
                    {
                        afterLastShot = true;
                        Assert.That(p.World.PartyMarkers, Has.Count.EqualTo(8));
                    }
                    if (p.Crossed(138.5f))
                    {
                        cleared = true;
                        Assert.That(p.World.PartyMarkers, Is.Empty);
                        Assert.That(((FakeMarkings)Natives.Markings).Marks, Is.Empty);
                    }
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(afterLastShot && cleared, Is.True);
    }

    [Test]
    public void DisabledAutomarkersLeavePartyUnmarked()
    {
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), 2, 9,
            new ScenarioRunOptions
            {
                StopAt = 10f,
                Overrides = o => ((UmadP3BlackHoleStateOverrides)o).Automarkers = false,
                Probe = p => Assert.That(p.World.PartyMarkers, Is.Empty),
            });
        Assert.That(run.Passed, Is.True, run.ToString());
    }

    [Test]
    public void DoubleTetherDebuffMarkersMatchDefaultLemegetonLayout()
    {
        var roles = Enum.GetValues<PartyRole>();
        var marks = UmadP3BlackHoleMarkers.Assign(roles, TetherOrder.DpsSupportAccretionDoubleTethers);
        Assert.That(marks[PartyRole.MeleeDpsA], Is.EqualTo(Sign.Attack1));
        Assert.That(marks[PartyRole.MainTank], Is.EqualTo(Sign.Attack2));
        Assert.That(marks[PartyRole.ShieldHealer], Is.EqualTo(Sign.Attack3));
        Assert.That(marks[PartyRole.MeleeDpsB], Is.EqualTo(Sign.Bind1));
        Assert.That(marks[PartyRole.OffTank], Is.EqualTo(Sign.Bind2));
        Assert.That(marks[PartyRole.CasterDps], Is.EqualTo(Sign.Bind3));
        Assert.That(marks[PartyRole.PhysRangedDps], Is.EqualTo(Sign.Ignore1));
        Assert.That(marks[PartyRole.RegenHealer], Is.EqualTo(Sign.Ignore2));
    }
}
