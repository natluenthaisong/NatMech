using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

public class GamePauseTests
{
    private const int PausedFrames = 90;

    [Test]
    public void UserPauseFreezesTheFightAndResumeCarriesOnCleanly()
    {
        var pausedAt = -1f;
        var framesPaused = 0;
        Vector3?[] positionsAtPause = [];
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), 2, 17,
            new ScenarioRunOptions
            {
                StopAt = 40f,
                Probe = p =>
                {
                    Vector3?[] Positions() => Enum.GetValues<PartyRole>().Select(r => p.Member(r)?.Position).ToArray();
                    if (pausedAt < 0f && p.Crossed(20f))
                    {
                        Assert.That(p.Game.CanPauseByUser, Is.True);
                        p.Game.TogglePauseByUser();
                        pausedAt = p.Time;
                        positionsAtPause = Positions();
                        return;
                    }
                    if (!p.Game.PausedByUser) return;
                    Assert.That(p.Game.Paused, Is.True);
                    Assert.That(p.Time, Is.EqualTo(pausedAt), "the timeline doesn't advance");
                    Assert.That(Positions(), Is.EqualTo(positionsAtPause), "bots and bosses hold still");
                    if (++framesPaused == PausedFrames) p.Game.TogglePauseByUser();
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(framesPaused, Is.EqualTo(PausedFrames));
    }
}
