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
        if (args is ["--snapshot", var png, var at, ..] && float.TryParse(at, out var seconds))
            return Snapshot(png, seconds, args.Length > 3 && int.TryParse(args[3], out var seed) ? seed : 1);
        ApplicationConfiguration.Initialize();
        Application.Run(new UI.MainForm());
        return 0;
    }

    // Plays a whole run with the player standing still in god mode and writes what happened, so a
    // build can be checked without opening the window. Exit code 0 when the fight reached its end.
    private static int SelfTest(string outPath)
    {
        var lines = new List<string>();
        var host = new SimHost(PartyRole.MeleeDpsA, strat: 2, seed: 1, godMode: true);
        var start = host.Player!.Position;
        for (var frame = 0; frame < 60; frame++) host.Advance(SimHost.Step, new Vector2(1f, 0f));
        var walked = host.Player.Position - start;
        lines.Add($"walk check: held east for 1 s, moved ({walked.X:F2}, {walked.Z:F2}) y");
        // Counted in frames, not fight time: a fight clock that never starts must still end the test.
        for (var frame = 0; frame < MaxSelfTestFrames && !host.Finished && !host.FrozenAfterDeath; frame++)
            host.Advance(SimHost.Step, Vector2.Zero);
        lines.Add($"finished={host.Finished} time={host.Time:F2} role={host.Role}");
        lines.AddRange(host.Log.Select(l => $"{l.Time,7:F2} {l.Text}"));
        lines.Add($"tether mistakes: {host.Scenario.TetherMistakes.Count}");
        File.WriteAllLines(outPath, lines);
        return host.Finished ? 0 : 1;
    }

    // Renders the arena and side panel offscreen at a fight time (god mode, player standing still)
    // to a PNG, so drawing can be checked without anyone looking at the window.
    private static int Snapshot(string pngPath, float seconds, int seed)
    {
        var host = new SimHost(PartyRole.MeleeDpsA, strat: 2, seed, godMode: true);
        using var coach = new UI.Coach(host);
        for (var frame = 0; frame < MaxSelfTestFrames && host.Time < seconds; frame++)
        {
            host.Advance(SimHost.Step, Vector2.Zero);
            coach.Update(speak: false);
        }
        using var arena = new UI.ArenaView { Size = new Size(820, 820), Host = host };
        arena.DrawOverlay = (g, _) => coach.DrawOnArena(g, arena, guide: true, spot: true);
        using var side = new UI.SidePanel { Size = new Size(340, 820) };
        side.Show(host, coach, seed);
        using var bitmap = new Bitmap(1160, 820);
        arena.DrawToBitmap(bitmap, new Rectangle(0, 0, 820, 820));
        side.DrawToBitmap(bitmap, new Rectangle(820, 0, 340, 820));
        bitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
        return 0;
    }
}
