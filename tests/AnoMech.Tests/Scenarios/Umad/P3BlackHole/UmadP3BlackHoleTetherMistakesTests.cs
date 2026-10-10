using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

public class UmadP3BlackHoleTetherMistakesTests
{
    private static UmadP3BlackHoleScenario Scenario(ScenarioProbe p) => (UmadP3BlackHoleScenario)p.Game.ActiveScenario!;

    [TestCase(0, 1)]
    [TestCase(1, 7)]
    [TestCase(2, 1)]
    [TestCase(2, 7)]
    public void PlayingTheStratLogsNoMistakes(int strat, int seed)
    {
        IReadOnlyList<string> mistakes = [];
        var markedMistake = false;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), strat, seed,
            new ScenarioRunOptions
            {
                Probe = p =>
                {
                    mistakes = Scenario(p).TetherMistakes;
                    markedMistake |= p.Game.HasScenarioMistake;
                },
            });
        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(mistakes, Is.Empty);
        Assert.That(markedMistake, Is.False);
    }

    // Second in line without Accretion is DPS #2, who takes the first wave-2 tether off DPS #1 out
    // at their pull spot: a player who stays in the middle can't, so DPS #1 is still holding it when
    // it fires at Nothingness 4.
    [TestCase(0, 3)]
    [TestCase(2, 3)]
    [TestCase(2, 8)]
    public void APlayerWhoStaysInTheMiddleIsToldTheyMissedTheirTether(int strat, int seed)
    {
        var mistakes = new List<string>();
        var markedMistake = false;
        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), strat, seed,
            new ScenarioRunOptions
            {
                StopAt = 69f,
                PlayerRole = PartyRole.MeleeDpsA,
                Overrides = o =>
                {
                    var overrides = (UmadP3BlackHoleStateOverrides)o;
                    overrides.LineNumber.Set(PartyRole.MeleeDpsA, 2);
                    overrides.Accretion.Set(PartyRole.MeleeDpsA, false);
                },
                Takeovers = [new Takeover(56f, new Vector2(0f, 0f))],
                Probe = p =>
                {
                    p.Game.GodMode = true;
                    mistakes = Scenario(p).TetherMistakes.ToList();
                    markedMistake |= p.Game.HasScenarioMistake;
                },
            });
        Assert.That(mistakes, Has.Some.Match(@"^Nothingness 4: you weren't holding your (North|East|South|West) tether$"),
            run.ToString());
        Assert.That(markedMistake, Is.True);
    }
}
