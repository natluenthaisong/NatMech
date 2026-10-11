using System.Diagnostics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Standalone.Host;

namespace AnoMech.Standalone.UI;

internal sealed class MainForm : Form
{
    private readonly ArenaView arena = new() { Dock = DockStyle.Fill };
    private readonly SidePanel side = new() { Dock = DockStyle.Right, Width = 340 };
    private readonly ComboBox roleBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly ComboBox stratBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly TextBox seedBox = new() { Width = 90, PlaceholderText = "random" };
    private readonly CheckBox godBox = new() { Text = "God mode", AutoSize = true };
    private readonly CheckBox guideBox = new() { Text = "Tether guide", AutoSize = true, Checked = true };
    private readonly CheckBox spotBox = new() { Text = "Safe/stack spot", AutoSize = true, Checked = true };
    private readonly CheckBox speakBox = new() { Text = "Speak callouts", AutoSize = true };
    private readonly Button startButton = new() { Text = "Start (R)", AutoSize = true };
    private readonly Button pauseButton = new() { Text = "Pause (Space)", AutoSize = true };
    private readonly System.Windows.Forms.Timer frameTimer = new() { Interval = 15 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private TimeSpan lastFrame;
    private SimHost? host;
    private Coach? coach;

    public MainForm()
    {
        Text = "NatMech Practice — Dancing Mad P3 Black Hole";
        ClientSize = new Size(1180, 820);
        MinimumSize = new Size(900, 640);
        BackColor = Color.FromArgb(22, 23, 28);
        ForeColor = Color.Gainsboro;
        KeyPreview = true;

        foreach (var role in Enum.GetValues<PartyRole>()) roleBox.Items.Add(SettingsGrid.RoleLabel(role));
        roleBox.SelectedIndex = (int)PartyRole.MeleeDpsA;
        var scenario = new UmadP3BlackHoleScenario();
        foreach (var strat in scenario.AiStrats) stratBox.Items.Add(strat.Name);
        stratBox.SelectedIndex = scenario.DefaultAi;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6, 6, 6, 0), WrapContents = false };
        toolbar.Controls.AddRange([
            Label("Role"), roleBox, Label("Strat"), stratBox, Label("Seed"), seedBox,
            startButton, pauseButton, godBox, guideBox, spotBox, speakBox,
        ]);
        Controls.Add(arena);
        Controls.Add(side);
        Controls.Add(toolbar);

        startButton.Click += (_, _) => Restart();
        pauseButton.Click += (_, _) => TogglePause();
        guideBox.CheckedChanged += (_, _) => arena.Focus();
        spotBox.CheckedChanged += (_, _) => arena.Focus();
        speakBox.CheckedChanged += (_, _) => arena.Focus();
        arena.DrawOverlay = (g, _) => coach?.DrawOnArena(g, arena, guideBox.Checked, spotBox.Checked);
        frameTimer.Tick += (_, _) => Frame();
        Shown += (_, _) => { Restart(); frameTimer.Start(); };
    }

    private static Label Label(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };

    private void Restart()
    {
        var seed = int.TryParse(seedBox.Text, out var s) ? s : Random.Shared.Next();
        coach?.Dispose();
        host = new SimHost((PartyRole)roleBox.SelectedIndex, stratBox.SelectedIndex, seed, godBox.Checked);
        coach = new Coach(host);
        arena.Host = host;
        side.Show(host, coach, seed);
        lastFrame = clock.Elapsed;
        arena.Focus();
    }

    private void TogglePause()
    {
        host?.Game.TogglePauseByUser();
        arena.Focus();
    }

    private void Frame()
    {
        var now = clock.Elapsed;
        var seconds = (float)(now - lastFrame).TotalSeconds;
        lastFrame = now;
        if (host is null) return;
        var typing = seedBox.Focused;
        host.Advance(seconds, ContainsFocus && !typing ? Keyboard.MoveDirection() : default);
        coach?.Update(speakBox.Checked);
        pauseButton.Text = host.Game.PausedByUser ? "Resume (Space)" : "Pause (Space)";
        arena.Invalidate();
        side.Invalidate();
    }

    // Before any control sees the key, so Space can't also click a focused button.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!seedBox.Focused)
        {
            if (keyData == Keys.Space) { TogglePause(); return true; }
            if (keyData == Keys.R) { Restart(); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
