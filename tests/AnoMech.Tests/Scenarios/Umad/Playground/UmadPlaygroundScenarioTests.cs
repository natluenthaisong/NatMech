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
    public void SteppingOnAnArrowLaunchesYou()
    {
        var bound = false;
        ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 3f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(1.5f, new Vector2(0f, 13f))],
            Probe = p => bound |= p.Member(Me)?.HasStatus(UmadConstants.StatusId.Bind) == true,
        });
        Assert.That(bound, Is.True, "the arrow due south bound you for the launch");
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
    public void EachArrowLaunchesYouOntoTheNextOne()
    {
        var east = UmadPlaygroundScenario.LandingFrom(1);
        Assert.That(Vector2.Distance(new(east.X, east.Z), new(13f, 0f)), Is.LessThan(1e-3f), "arrow 1 (south-east) lands you on arrow 2 (east)");
        var south = UmadPlaygroundScenario.LandingFrom(7);
        Assert.That(Vector2.Distance(new(south.X, south.Z), new(0f, 13f)), Is.LessThan(1e-3f), "the last arrow sends you round to the first");
    }

    private static int Catches(Vector2 standAt)
    {
        var catches = 0;
        ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 25f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(1.5f, standAt)],
            Probe = p =>
            {
                p.Game.GodMode = true;
                catches = ((UmadPlaygroundScenario)p.Game.ActiveScenario!).Catches;
            },
        });
        return catches;
    }

    [Test]
    public void ExdeathNeverCatchesYouOnTheMerryGoRound()
    {
        Assert.That(Catches(new Vector2(0f, 13f)), Is.Zero, "standing on the south arrow");
        Assert.That(Catches(new Vector2(0f, 8f)), Is.Positive, "off the ring he does");
    }

    [Test]
    public void ALapConfusesExdeathIntoThunderingChaosWhoFightsBack()
    {
        // The fake doesn't fly you, so standing on the south arrow launches you from it every 0.95s.
        var confused = 0;
        var fightBacks = 0;
        var exdeathConfused = false;
        ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 20f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(1.5f, new Vector2(0f, 13f))],
            Probe = p =>
            {
                p.Game.GodMode = true;
                var playground = (UmadPlaygroundScenario)p.Game.ActiveScenario!;
                confused = playground.ConfusedExdeaths;
                fightBacks = playground.FightBacks;
                exdeathConfused |= p.World.Children.OfType<SimEnemy>()
                    .Any(e => e.BNpcBaseId == UmadConstants.BNpcBaseId.Exdeath && e.HasStatus(UmadConstants.StatusId.Confused));
            },
        });
        Assert.That(confused, Is.EqualTo(1), "eight launches in a row");
        Assert.That(exdeathConfused, Is.True);
        Assert.That(fightBacks, Is.EqualTo(1), "Chaos answers the second Thunder III");
    }

    [Test]
    public void ThePartyFollowsYouAsATrain()
    {
        var gaps = new List<float>();
        var run = ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 8f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(0.5f, new Vector2(0f, 8f))],
            Probe = p =>
            {
                p.Game.GodMode = true;
                if (!p.Crossed(7.5f)) return;
                var line = Enumerable.Range(0, 8).Select(seat => p.World.Party.Get(seat)).OfType<SimCharacter>()
                    .OrderBy(m => ReferenceEquals(m, p.World.Party.Player) ? -1 : 0).ToList();
                for (var k = 1; k < line.Count; k++)
                    gaps.Add(Vector2.Distance(new(line[k].Position.X, line[k].Position.Z), new(line[k - 1].Position.X, line[k - 1].Position.Z)));
            },
        });
        Assert.That(gaps, Has.Count.EqualTo(7));
        Assert.That(gaps, Is.All.LessThan(3f), "each car right behind the one ahead");
        Assert.That(run.Deaths, Is.Empty, "beams and Exdeath leave the train alone (god mode keeps you up)");
    }

    [Test]
    public void OmegaMArrivesAndShieldsYouFromTheBeams()
    {
        var blocks = 0;
        var hitsWhileGuarded = -1;
        var hitsAt20 = 0;
        ScenarioRun.Execute(typeof(UmadPlaygroundScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Me,
            StopAt = 40f,
            WriteArtifactsOnFailure = false,
            Takeovers = [new Takeover(0.5f, new Vector2(0f, 8f))],
            Probe = p =>
            {
                p.Game.GodMode = true;
                var playground = (UmadPlaygroundScenario)p.Game.ActiveScenario!;
                blocks = playground.ShieldBlocks;
                if (p.Crossed(20f)) hitsAt20 = playground.BeamHits;
                hitsWhileGuarded = playground.BeamHits - hitsAt20;
            },
        });
        Assert.That(blocks, Is.Positive);
        Assert.That(hitsWhileGuarded, Is.Zero, "standing still behind Omega-M, every beam is his");
    }
}
