using System.Drawing.Drawing2D;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Standalone.Host;

namespace AnoMech.Standalone.UI;

// The plugin's coaching, read off the same sim state: callouts (UmadP3BlackHoleCallouts), tether
// mistakes, the tether guide, plus what the game would show and the 2D view can't: the shape of
// every attack as it's cast and as it lands. Timed on the fight clock, so a pause holds them.
internal sealed class Coach : IDisposable
{
    private const double CalloutSeconds = 5;
    private const double ImpactSeconds = 1.0;
    // Sampling step for attack shapes: AoeQuery.SignedDistance is exact; the grid only limits looks.
    private const float ShapeCell = 0.5f;

    public static readonly Color AlertColour = Color.FromArgb(255, 160, 30);
    public static readonly Color InfoColour = Color.FromArgb(235, 235, 235);
    public static readonly Color MistakeColour = Color.FromArgb(255, 70, 70);
    private static readonly Color TakeColour = Color.FromArgb(255, 200, 0);
    private static readonly Color HeldColour = Color.FromArgb(80, 255, 80);
    private static readonly Color SpotColour = Color.FromArgb(0, 220, 255);
    private static readonly Color HintColour = Color.FromArgb(255, 96, 255);

    private readonly SimHost host;
    private readonly UmadP3BlackHoleCallouts callouts = new();
    private readonly List<(string Text, Color Colour, double At)> calls = new();
    private readonly List<(AoeQuery Query, double At)> impacts = new();
    private readonly Font labelFont = new("Segoe UI", 9f, FontStyle.Bold);
    private int mistakesShown;

    public Coach(SimHost host)
    {
        this.host = host;
        AoeQuery.Evaluated += OnAoeEvaluated;
    }

    public IEnumerable<(string Text, Color Colour, double Age)> RecentCalls =>
        calls.Select(c => (c.Text, c.Colour, host.Time - c.At));

    public void Update(bool speak)
    {
        double now = host.Time;
        if (host.State is { } state && host.Player is { } player)
            foreach (var call in callouts.Next(state, host.World, player, host.Role))
                Add(call.Text, call.Alert ? AlertColour : InfoColour, now, speak);
        var mistakes = host.Scenario.TetherMistakes;
        for (; mistakesShown < mistakes.Count; mistakesShown++)
            Add(mistakes[mistakesShown], MistakeColour, now, speak);
        calls.RemoveAll(c => now - c.At > CalloutSeconds);
        impacts.RemoveAll(i => now - i.At > ImpactSeconds);
    }

    private void Add(string text, Color colour, double now, bool speak)
    {
        calls.Insert(0, (text, colour, now));
        if (speak) Plugin.Speech.Say(text.Replace(" => ", ", then ").Replace(" + ", ", ").Replace("/", " ").Replace("x2", "twice"));
    }

    private void OnAoeEvaluated(AoeQuery query) => impacts.Add((query, host.Time));

    public void DrawOnArena(Graphics g, ArenaView arena, bool guide, bool spot)
    {
        DrawTelegraphs(g, arena);
        double now = host.Time;
        foreach (var (query, at) in impacts)
        {
            var alpha = (int)(170 * (1 - (now - at) / ImpactSeconds));
            if (alpha > 0) FillShape(g, arena, query, Color.FromArgb(alpha, 255, 90, 60));
        }
        if (spot) DrawSpotHint(g, arena);
        if (guide) DrawGuide(g, arena);
    }

    private void DrawSpotHint(Graphics g, ArenaView arena)
    {
        if (host.State is not { } state || host.Player is not { } player) return;
        if (UmadP3BlackHoleSpotHint.For(state.ScenarioObjects, host.World, player) is not { } hint) return;
        using var pen = new Pen(HintColour, 2.5f);
        arena.DrawCircle(g, pen, hint.Spot, UmadP3BlackHoleSpotHint.SpotRadius);
        var at = arena.ToScreen(hint.Spot);
        ArenaView.DrawCentredText(g, UmadP3BlackHoleSpotHint.Label(hint), labelFont, HintColour,
            at with { Y = at.Y - arena.ToPixels(UmadP3BlackHoleSpotHint.SpotRadius) - 9 });
        if (Vector2.Distance(new(player.Position.X, player.Position.Z), new(hint.Spot.X, hint.Spot.Z)) > UmadP3BlackHoleSpotHint.SpotRadius)
        {
            pen.DashStyle = DashStyle.Dot;
            g.DrawLine(pen, arena.ToScreen(player.Position), at);
        }
    }

    // Shown for the whole cast, as the game's omen would be.
    private void DrawTelegraphs(Graphics g, ArenaView arena)
    {
        foreach (var enemy in host.World.Children.OfType<SimEnemy>())
        {
            if (!enemy.IsAlive() || !enemy.IsCasting) continue;
            var cast = enemy.Casting;
            if (Natives.Data.Action(cast.ActionId) is not { } action || !IsArea(action.CastType)) continue;
            var source = new Placement(cast.TargetLocation ?? enemy.Position, cast.Rotation);
            FillShape(g, arena, new AoeQuery(cast.ActionId, source, casterHitboxRadius: enemy.HitboxRadius), Color.FromArgb(55, 255, 150, 40));
        }
    }

    private static bool IsArea(CastType type) => type is not (CastType.None or CastType.SingleTarget);

    private static void FillShape(Graphics g, ArenaView arena, AoeQuery query, Color colour)
    {
        var cell = arena.ToPixels(ShapeCell) + 0.6f;
        using var brush = new SolidBrush(colour);
        for (var x = -ArenaView.ArenaRadius; x <= ArenaView.ArenaRadius; x += ShapeCell)
        for (var z = -ArenaView.ArenaRadius; z <= ArenaView.ArenaRadius; z += ShapeCell)
        {
            if (x * x + z * z > ArenaView.ArenaRadius * ArenaView.ArenaRadius) continue;
            if (query.SignedDistance(new Vector3(x, 0f, z)) is not < 0f) continue;
            var at = arena.ToScreen(new Vector3(x, 0f, z));
            g.FillRectangle(brush, at.X - cell / 2f, at.Y - cell / 2f, cell, cell);
        }
    }

    private void DrawGuide(Graphics g, ArenaView arena)
    {
        if (host.State is not { } state || host.Player is not { } player) return;
        var objects = state.ScenarioObjects;
        if (!objects.TetherGuides.TryGetValue(player, out var mine)) return;
        foreach (var tether in objects.Tethers)
        {
            if (tether.A is not { } hole || !mine.Holes.Contains(hole)) continue;
            var held = ReferenceEquals(tether.B, player);
            if (!held && UmadP3BlackHoleCallouts.HandedOn(objects, player, mine, hole)) continue;
            using var pen = new Pen(held ? HeldColour : TakeColour, 3f) { DashStyle = held ? DashStyle.Solid : DashStyle.Dash };
            arena.DrawCircle(g, pen, hole.Position, 2f);
        }
        if (mine.HoldAt is { } spot)
        {
            using var pen = new Pen(SpotColour, 2.5f);
            arena.DrawCircle(g, pen, spot, 1.5f);
            g.DrawLine(pen, arena.ToScreen(player.Position), arena.ToScreen(spot));
        }
    }

    public void Dispose()
    {
        AoeQuery.Evaluated -= OnAoeEvaluated;
        labelFont.Dispose();
    }
}
