using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Standalone.Host;

namespace AnoMech.Standalone;

internal static class Program
{
    private const int MaxSelfTestFrames = 200 * 60;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--selftest", ..]) return SelfTest(args.Length > 1 ? args[1] : "selftest.log");
        ApplicationConfiguration.Initialize();
        Application.Run(new Form { Text = "NatMech Practice" });
        return 0;
    }

    // Plays a whole run with the player standing still in god mode and writes what happened, so a
    // build can be checked without opening the window. Exit code 0 when the fight reached its end.
    private static int SelfTest(string outPath)
    {
        var lines = new List<string>();
        var host = new SimHost(PartyRole.MeleeDpsA, strat: 2, seed: 1, godMode: true);
        // Counted in frames, not fight time: a fight clock that never starts must still end the test.
        for (var frame = 0; frame < MaxSelfTestFrames && !host.Finished && !host.FrozenAfterDeath; frame++)
            host.Advance(SimHost.Step, Vector2.Zero);
        lines.Add($"finished={host.Finished} time={host.Time:F2} role={host.Role}");
        lines.AddRange(host.Log.Select(l => $"{l.Time,7:F2} {l.Text}"));
        lines.Add($"tether mistakes: {host.Scenario.TetherMistakes.Count}");
        File.WriteAllLines(outPath, lines);
        return host.Finished ? 0 : 1;
    }
}
