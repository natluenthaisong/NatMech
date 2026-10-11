using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.Playground;

namespace AnoMech.Tests;

public class UmadPlaygroundScenarioTests
{
    private const PartyRole Me = PartyRole.MeleeDpsA;

    [Test]
    public void StandingStillGetsYouCaught()
    {
        var run = ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 40f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(0.5f, new Vector2(0f, 8f))],
        });
        var mine = run.Deaths.Where(d => d.Role == Me).ToList();
        Assert.That(mine, Is.Not.Empty, run.ToString());
        Assert.That(mine[0].Cause, Does.Contain("Chaos's beam"), "the first beam lands where you stood, before Exdeath arrives");
        Assert.That(mine[0].Time, Is.InRange(5.5f, 7.5f));
    }

    [Test]
    public void ExdeathWalksYouDownToCatchingRange()
    {
        float? reachedAt = null;
        ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 20f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(0.5f, new Vector2(0f, 8f))],
            Probe = p =>
            {
                p.Game.GodMode = true;
                var exdeath = p.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == UmadConstants.BNpcBaseId.Exdeath);
                if (reachedAt is not null || exdeath is null || p.Member(Me) is not { } me) return;
                var reach = exdeath.HitboxRadius + me.HitboxRadius + 0.4f;
                if (Vector2.Distance(new(exdeath.Position.X, exdeath.Position.Z), new(me.Position.X, me.Position.Z)) <= reach)
                    reachedAt = p.Time;
            },
        });
        Assert.That(reachedAt, Is.Not.Null.And.InRange(5f, 14f), "starts 16y north at 3s, walking from 3.5y/s");
    }

    [Test]
    public void StandingInAnArrowsPathLaunchesYou()
    {
        // The ring turns at 0.35 rad/s from slot 0 due south; be where slot 0 passes at ~2.5s.
        var angle = 0.35f * 2.5f;
        var onTheRide = new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * 13f;
        var bound = false;
        ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 3.5f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(1.5f, onTheRide)],
            Probe = p => bound |= p.Member(Me)?.HasStatus(UmadConstants.StatusId.Bind) == true,
        });
        Assert.That(bound, Is.True, "the arrow bound you for the launch");
    }

    [Test]
    public void InGodModeItKeepsGoing()
    {
        var stillRunning = false;
        ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 60f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(0.5f, new Vector2(0f, 8f))],
            Probe = p =>
            {
                p.Game.GodMode = true;
                if (p.Crossed(59f)) stillRunning = p.Game.IsScenarioActive && !p.World.Events.IsEmpty;
            },
        });
        Assert.That(stillRunning, Is.True, "no timeline end: the run lasts as long as you do");
    }

    [Test]
    public void EachArrowLaunchesYouOntoTheNextOneWhereItWillBe()
    {
        const float ring = 0.7f;
        var from = UmadPlaygroundScenario.LandingFrom(7, ring - 0.35f * 0.8f);
        var landing = UmadPlaygroundScenario.LandingFrom(0, ring);
        Assert.That(landing.Length(), Is.EqualTo(13f).Within(1e-3f), "on the ring");
        var nextAngle = ring + 0.35f * 0.8f + MathF.Tau / 8f;
        Assert.That(Vector2.Distance(new(landing.X, landing.Z), new Vector2(MathF.Sin(nextAngle), MathF.Cos(nextAngle)) * 13f), Is.LessThan(1e-3f),
            "on arrow 1, led by the ring's turn during the carry");
        Assert.That(from.Length(), Is.EqualTo(13f).Within(1e-3f), "the last arrow sends you round to the first");
    }
}
