using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Standalone.Host;

namespace AnoMech.Standalone.UI;

// Everything the game's HUD would tell you, as text: fight clock, what the bosses are casting,
// your debuffs, the callouts, and the chat log (deaths, mistakes).
internal sealed class SidePanel : Control
{
    private const int LogLines = 14;

    private readonly Font heading = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font body = new("Segoe UI", 9f);
    private readonly Font callout = new("Segoe UI", 13f, FontStyle.Bold);
    private readonly Font banner = new("Segoe UI", 16f, FontStyle.Bold);
    private SimHost? host;
    private Coach? coach;
    private int seed;

    public SidePanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(30, 31, 38);
        ForeColor = Color.Gainsboro;
    }

    public void Show(SimHost runningHost, Coach runningCoach, int runSeed)
    {
        host = runningHost;
        coach = runningCoach;
        seed = runSeed;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if (host is null || coach is null) return;
        float y = 10;
        void Line(string text, Font font, Color colour)
        {
            using var brush = new SolidBrush(colour);
            var size = g.MeasureString(text, font, Width - 20);
            g.DrawString(text, font, brush, new RectangleF(10, y, Width - 20, size.Height));
            y += size.Height + 2;
        }

        Line($"{host.Time,6:F1}s   seed {seed}", heading, Color.White);
        if (host.Game.PausedByUser) Line("PAUSED  (Space to resume)", banner, Color.FromArgb(255, 166, 64));
        else if (host.FrozenAfterDeath) Line("DEAD  (R to restart)", banner, Coach.MistakeColour);
        else if (host.Finished) Line(host.Game.HasScenarioSucceeded ? "CLEARED" : "Finished", banner, Color.FromArgb(120, 230, 120));
        y += 6;

        Line("Casting", heading, Color.Silver);
        foreach (var enemy in host.World.Children.OfType<SimEnemy>())
        {
            if (!enemy.IsAlive() || !enemy.IsCasting) continue;
            var name = Natives.Data.Action(enemy.Casting.ActionId)?.Name ?? $"0x{enemy.Casting.ActionId:X}";
            Line($"{enemy.DisplayName}: {name}  {enemy.Casting.Progress * 100:F0}%", body, Color.Gainsboro);
        }
        y += 6;

        Line($"You ({host.Role})", heading, Color.Silver);
        if (host.Player is { } player)
        {
            var statuses = player.ActiveStatuses
                .Select(s => $"{Natives.Data.StatusName(s.StatusId) ?? $"#{s.StatusId}"}{(s.RemainingTime > 0 ? $" {s.RemainingTime:F0}s" : "")}{(s.Stacks > 1 ? $" x{s.Stacks}" : "")}")
                .ToList();
            Line(statuses.Count == 0 ? "no statuses" : string.Join("\n", statuses), body, Color.Gainsboro);
        }
        y += 6;

        Line("Callouts", heading, Color.Silver);
        foreach (var (text, colour, _) in coach.RecentCalls.Take(4)) Line(text, callout, colour);
        y += 6;

        Line("Log", heading, Color.Silver);
        foreach (var (time, text) in host.Log.TakeLast(LogLines).Reverse())
            Line($"{time,6:F1}  {text.Replace("[AnoMech] ", "").Replace("[NatMech] ", "")}", body,
                text.Contains("Mistake") || text.Contains("died") ? Color.FromArgb(255, 140, 140) : Color.Gainsboro);

        using var help = new SolidBrush(Color.Gray);
        g.DrawString("WASD / arrows: move    Space: pause    R: restart", body, help, 10, Height - 22);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            heading.Dispose();
            body.Dispose();
            callout.Dispose();
            banner.Dispose();
        }
        base.Dispose(disposing);
    }
}
