using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Tests;
using Dalamud.Plugin.Services;

namespace AnoMech.Standalone.Host;

// One Black Hole run outside the game: ScenarioRun's headless setup with a real-time clock and a
// human at the keyboard instead of a bot. Plugin's statics and Natives are process-wide, so only
// one host may live at a time; a restart builds a new one.
internal sealed class SimHost
{
    public const float Step = 1f / 60f;
    public const float RunSpeed = 6f;
    // The arena's wall: the game stops you here, so the app does too.
    private const float WallRadius = 19.5f;
    // A stall (a breakpoint, a dragged window) mustn't replay seconds of fight in one frame.
    private const float MaxCatchUpSeconds = 0.25f;

    private readonly FakeGame fake;
    private float accumulator;

    public Game Game { get; }
    public UmadP3BlackHoleScenario Scenario { get; }
    public PartyRole Role { get; }
    public List<(float Time, string Text)> Log { get; } = new();

    public SimHost(PartyRole role, int strat, int seed, bool godMode)
    {
        Role = role;
        fake = FakeGame.Install();
        fake.BattleCharas.Player.ClassJob = PartyPresets.Standard[(int)role].ClassJob;
        DalamudServices.InstallNullServices();
        DalamudServices.Install(nameof(Plugin.ChatGui), CaptureService.Create<IChatGui>(AddLog));
        DalamudServices.Install(nameof(Plugin.ToastGui), CaptureService.Create<IToastGui>(_ => { }));
        DalamudServices.Install(nameof(Plugin.Config), new Configuration());
        DebugBotControl.Enabled = false;
        Game = new Game();
        DalamudServices.Install(nameof(Plugin.GameInstance), Game);
        Game.GodMode = godMode;
        Scenario = (UmadP3BlackHoleScenario)Game.Scenarios.Single(s => s is UmadP3BlackHoleScenario);
        Game.RunScenario(new RunScenarioParams(Scenario, role, strat, 0, seed));
        fake.Frame(Step, Game.Tick);
    }

    public SimWorld World => Game.World;
    public float Time => World.Events.Elapsed;
    public UmadP3BlackHoleState? State => Scenario.LastState;
    public SimCharacter? Player => World.Party.Player;
    public bool Finished => Game.IsScenarioActive && World.Events.IsEmpty;
    public bool FrozenAfterDeath => Game.Paused && !Game.PausedByUser;

    // `move` is the held direction in arena axes: +X east, +Y south. Movement still applies while
    // paused, as in game: you can walk while the fight is frozen.
    public void Advance(float realSeconds, Vector2 move)
    {
        accumulator += MathF.Min(realSeconds, MaxCatchUpSeconds);
        while (accumulator >= Step)
        {
            Walk(move, Step);
            fake.Frame(Step, Game.Tick);
            accumulator -= Step;
        }
    }

    private void Walk(Vector2 move, float seconds)
    {
        var moving = move.LengthSquared() > 1e-4f && Player is { } player && player.IsAlive();
        ((FakeLocalPlayerInput)Natives.PlayerInput).MovementInputActive = moving;
        if (!moving) return;
        var dir = Vector2.Normalize(move);
        var character = fake.BattleCharas.Player;
        var local = World.Coordinates.ToLocal(character.Position);
        var next = new Vector2(local.X, local.Z) + dir * RunSpeed * seconds;
        if (next.Length() > WallRadius) next = Vector2.Normalize(next) * WallRadius;
        character.Position = World.Coordinates.ToGlobal(new Vector3(next.X, local.Y, next.Y));
        character.Rotation = MathF.Atan2(dir.X, dir.Y);
    }

    private void AddLog(string text) => Log.Add((Time, text));
}
