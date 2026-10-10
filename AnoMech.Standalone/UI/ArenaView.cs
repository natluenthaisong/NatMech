using System.Drawing.Drawing2D;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Standalone.Host;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Standalone.UI;

// Top-down arena: north up, one scenario-local yalm = Scale pixels. Draws whatever the running
// SimHost's world holds; owns no fight state of its own.
internal sealed class ArenaView : Control
{
    public const float ArenaRadius = 20f;
    private const float ViewRadius = 22f;
    private const float PlayerDotRadius = 0.6f;

    private static readonly Color Floor = Color.FromArgb(38, 40, 48);
    private static readonly Color Edge = Color.FromArgb(110, 115, 130);
    private static readonly Color Tank = Color.FromArgb(70, 130, 255);
    private static readonly Color Healer = Color.FromArgb(70, 200, 100);
    private static readonly Color Dps = Color.FromArgb(235, 80, 80);
    private static readonly Color Hole = Color.FromArgb(150, 80, 220);
    private static readonly Color GrabbyTether = Color.FromArgb(200, 120, 255);

    private readonly Font labelFont = new("Segoe UI", 8f, FontStyle.Bold);
    private readonly Font waymarkFont = new("Segoe UI", 10f, FontStyle.Bold);

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public SimHost? Host { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<Graphics, RectangleF>? DrawOverlay { get; set; }

    private PointF centre;
    private float scale;

    public ArenaView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        BackColor = Color.FromArgb(22, 23, 28);
    }

    public PointF ToScreen(Vector3 local) => new(centre.X + local.X * scale, centre.Y + local.Z * scale);
    public float ToPixels(float yalms) => yalms * scale;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var side = MathF.Min(Width, Height);
        centre = new PointF(Width / 2f, Height / 2f);
        scale = side / (2f * ViewRadius);

        FillCircle(g, Floor, Vector3.Zero, ArenaRadius);
        using (var edge = new Pen(Edge, 2f)) DrawCircle(g, edge, Vector3.Zero, ArenaRadius);
        if (Host is not { } host) return;

        DrawWaymarks(g, host);
        DrawOverlay?.Invoke(g, ClientRectangle);
        DrawEnemies(g, host);
        DrawTethers(g, host);
        DrawParty(g, host);
    }

    private void DrawWaymarks(Graphics g, SimHost host)
    {
        if (host.Scenario.Phase.Zone.WaymarkPresets is not [var layout, ..]) return;
        foreach (var marker in layout.Markers)
        {
            var label = marker.Slot.ToString();
            var colour = label switch
            {
                "A" or "One" => Color.FromArgb(230, 80, 80),
                "B" or "Two" => Color.FromArgb(230, 210, 80),
                "C" or "Three" => Color.FromArgb(80, 160, 230),
                _ => Color.FromArgb(180, 90, 220),
            };
            var text = label switch { "One" => "1", "Two" => "2", "Three" => "3", "Four" => "4", _ => label };
            var at = ToScreen(marker.Offset);
            using var brush = new SolidBrush(Color.FromArgb(70, colour));
            g.FillEllipse(brush, at.X - 12, at.Y - 12, 24, 24);
            DrawCentredText(g, text, waymarkFont, colour, at);
        }
    }

    private void DrawEnemies(Graphics g, SimHost host)
    {
        foreach (var enemy in host.World.Children.OfType<SimEnemy>())
        {
            if (!enemy.IsAlive() || !IsShown(enemy)) continue;
            var isHole = enemy.BNpcBaseId == BNpcBaseId.BlackHole;
            var radius = isHole ? 1.25f : MathF.Min(enemy.HitboxRadius, 3f);
            var colour = isHole ? Hole : Color.FromArgb(240, 170, 60);
            using (var brush = new SolidBrush(Color.FromArgb(isHole ? 170 : 60, colour))) FillCircle(g, brush, enemy.Position, radius);
            using (var pen = new Pen(colour, 2f)) DrawCircle(g, pen, enemy.Position, radius);
            if (!isHole) DrawCentredText(g, enemy.DisplayName, labelFont, colour, ToScreen(enemy.Position) with { Y = ToScreen(enemy.Position).Y - ToPixels(radius) - 8 });
        }
    }

    // Invisible helpers carry AoEs and tethers but are never drawn in game either.
    private static bool IsShown(SimEnemy enemy) => enemy.SpawnConfig.Visibility switch
    {
        SpawnVisibility.InvisibleHelper => false,
        SpawnVisibility.Visible => true,
        _ => enemy.Proxy?.IsDrawObjectVisible ?? false,
    };

    private void DrawTethers(Graphics g, SimHost host)
    {
        foreach (var tether in host.World.Children.OfType<SimTether>())
        {
            if (!tether.IsActive || tether.A is not { } a || tether.B is not { } b) continue;
            var grabby = tether.TetherId == TetherId.GrabbyTether;
            using var pen = new Pen(grabby ? GrabbyTether : Color.Gray, grabby ? 3f : 2f);
            g.DrawLine(pen, ToScreen(a.Position), ToScreen(b.Position));
        }
    }

    private void DrawParty(Graphics g, SimHost host)
    {
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (host.World.Party.Get(role) is not { } member) continue;
            var isPlayer = role == host.World.Party.PlayerRole;
            var alive = member.IsAlive();
            var colour = !alive ? Color.DimGray : role.IsTank() ? Tank : role.IsDps() ? Dps : Healer;
            using (var brush = new SolidBrush(colour)) FillCircle(g, brush, member.Position, PlayerDotRadius);
            if (isPlayer)
            {
                using var ring = new Pen(Color.White, 3f);
                DrawCircle(g, ring, member.Position, PlayerDotRadius + 0.35f);
            }
            var label = isPlayer ? "YOU" : SettingsGrid.RoleLabel(role);
            var at = ToScreen(member.Position);
            DrawCentredText(g, label, labelFont, Color.White, at with { Y = at.Y + ToPixels(PlayerDotRadius) + 9 });
        }
    }

    public void FillCircle(Graphics g, Color colour, Vector3 local, float radius)
    {
        using var brush = new SolidBrush(colour);
        FillCircle(g, brush, local, radius);
    }

    public void FillCircle(Graphics g, Brush brush, Vector3 local, float radius)
    {
        var at = ToScreen(local);
        var r = ToPixels(radius);
        g.FillEllipse(brush, at.X - r, at.Y - r, 2 * r, 2 * r);
    }

    public void DrawCircle(Graphics g, Pen pen, Vector3 local, float radius)
    {
        var at = ToScreen(local);
        var r = ToPixels(radius);
        g.DrawEllipse(pen, at.X - r, at.Y - r, 2 * r, 2 * r);
    }

    public static void DrawCentredText(Graphics g, string text, Font font, Color colour, PointF at)
    {
        var size = g.MeasureString(text, font);
        var topLeft = new PointF(at.X - size.Width / 2f, at.Y - size.Height / 2f);
        using var shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
        using var brush = new SolidBrush(colour);
        g.DrawString(text, font, shadow, topLeft.X + 1, topLeft.Y + 1);
        g.DrawString(text, font, brush, topLeft);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            labelFont.Dispose();
            waymarkFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
