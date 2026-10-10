# Plan: NatMech Practice — standalone Black Hole prototype (no FFXIV needed)

Status lives at the bottom of this file. Whoever picks this up (Claude Code or Codex): read
`AGENTS.md` first, then this plan, then continue from the first unchecked milestone. Update the
status log and commit after each milestone.

## Goal

A Windows desktop app that plays Dancing Mad P3 **Black Hole** without the game: a top-down 2D view
of the arena, the player steers their own dot with the keyboard, the bots play the selected strat,
and NatMech's callouts, tether guide, mistake log and pause all work. It runs the **same sim code**
as the plugin. Nothing in the plugin's behaviour changes.

Out of scope for the prototype: other scenarios, multiplayer, 3D, sound beyond the Windows voice,
an installer.

## Why it's possible

The test suite already runs every scenario headless (`tests/AnoMech.Tests/Support/ScenarioRun.cs`):
- `FakeGame.Install()` puts fakes behind every `Natives.*` game boundary.
- Dalamud services are null-object stand-ins (`DalamudServices`).
- Fight data comes from xivapi/ffxiv-datamining CSVs downloaded at build (`DataminingGameData`).

The app swaps the test loop for a real-time loop, keyboard input for the player, and a renderer.

## Architecture

New project `AnoMech.Standalone/` (in `AnoMech.sln`), `net10.0-windows`, `UseWindowsForms`,
`OutputType WinExe`, `AssemblyName NatMechPractice`. **No NuGet packages**: WinForms ships with the
.NET Windows Desktop runtime.

- **References**
  - `ProjectReference ../AnoMech/AnoMech.csproj`.
  - Dalamud libraries by `HintPath` from `$(DalamudLibPath)` with `Private=true`, copied like the
    test project does (see `tests/AnoMech.Tests/AnoMech.Tests.csproj`).
- **Fakes reused, not copied**
  - `<Compile Include="..\tests\AnoMech.Tests\Core\Native\Fakes\*.cs" Exclude="...\*Tests.cs" Link=...>`.
  - `..\tests\AnoMech.Tests\Support\FakeFramework.cs`.
  - The fakes are `internal`, in namespace `AnoMech.Tests`, and use AnoMech internals, so
    `AnoMech/AnoMech.csproj` gets `<InternalsVisibleTo Include="NatMechPractice" />`.
  - `DalamudServices.cs` is NOT linked (it uses NUnit attributes). The app has its own
    `ServiceStandIns` with the same `Install` + `NullService` idea.
- **Fight data**
  - The datamining download target moves from the test csproj into a shared
    `build/Datamining.targets`, imported by both projects.
  - Same pinned commit and sheet list. CSVs land in `<output>/datamining/*.csv`, which is where
    `DataminingGameData` reads them.
- **Engine host `SimHost`** (mirrors `ScenarioRun.Execute` setup)
  - `FakeGame.Install()`.
  - Install stand-ins for `Plugin.Log`, `PluginInterface`, `ChatGui`, `ToastGui`, `Config`
    (`new Configuration()`), `GameInstance`, `Framework`. `ChatGui`/`ToastGui` capture text for
    the app's log.
  - Set `fake.BattleCharas.Player.ClassJob` to fit the role.
  - `DebugBotControl.Enabled = false`, so the player is human and AI moves on it are no-ops.
  - `game.RunScenario(new RunScenarioParams(scenario, role, strat, 0, seed))`.
  - Step with a fixed 1/60 s timestep from an accumulator: `fake.Frame(1/60f, game.Tick)`.
  - Restart = a new `SimHost`. Plugin statics are process-global, so only one run at a time.
- **Input**
  - WASD/arrows move the fake player (`FakeCharacter.Position`, scenario origin + local) at
    6 y/s, north up (north is -Z).
  - `FakeLocalPlayerInput.MovementInputActive` is true while moving.
- **Renderer `ArenaView`** (double-buffered `Control`, GDI+)
  - Transform: centre (0,0), arena radius 20 y fits the view, -Z up.
  - Draw order:
    1. arena circle and waymarks;
    2. resolved AoE flashes;
    3. cast telegraphs;
    4. black holes and tethers (`SimTether` A→B);
    5. party dots, coloured by role, with labels, dead greyed, the player ringed;
    6. enemies with cast bars;
    7. the tether guide from `TetherGuides`.
  - **AoE shapes:** subscribe to `AoeQuery.Evaluated`, keep each query about 0.6 s, and shade the
    shape by sampling `AoeQuery.SignedDistance` on a 0.5 y grid. That's exact for every shape the
    sim knows, with no per-shape drawing code.
  - **Telegraphs:** while an enemy casts an action whose `Natives.Data.Action(id).CastType` is an
    area, sample the same way from the caster's placement.
- **HUD**
  - Fight time, your role, line number, Accretion and debuffs.
  - Callouts from `UmadP3BlackHoleCallouts.Next(...)`, newest on top for 5 s, speaking via
    `Plugin.Speech` when on.
  - Tether mistakes from `UmadP3BlackHoleScenario.TetherMistakes`.
  - The captured chat log (deaths, mistakes).
  - Paused and death overlays.
- **Toolbar:** Role, Strat (default from `DefaultAi`), Seed (blank = random), Start/Restart (R),
  Pause (Space, `Game.TogglePauseByUser`), God mode, Tether guide, Speak.

## Milestones

- [ ] **M0 Skeleton:** project, solution entry, shared `build/Datamining.targets`,
  `InternalsVisibleTo`, linked fakes. `dotnet build` passes and the test suite still passes.
- [ ] **M1 Headless host:** `SimHost` runs Black Hole to the end with bots and the player
  standing still (god mode). Prove it with a smoke test in `tests/AnoMech.Tests/Standalone/` or a
  `--selftest` command-line switch that prints time, deaths and mistakes.
- [ ] **M2 Window:** a WinForms form with `ArenaView` drawing the arena, party, enemies and
  tethers from a running `SimHost` at 60 fps.
- [ ] **M3 Control:** keyboard movement for the player; Start/Restart, Pause, God mode; role,
  strat and seed pickers.
- [ ] **M4 AoE:** resolved AoE flashes and cast telegraphs via `SignedDistance` sampling.
- [ ] **M5 Coaching:** callouts, tether guide, mistakes, chat log, status HUD, death overlay.
- [ ] **M6 Ship:** `dotnet publish AnoMech.Standalone -c Release -r win-x64 --self-contained
  false -o publish/NatMechPractice`; README section; manual run-through.

## Rules and gotchas

- Follow `AGENTS.md`.
  - Don't change sim behaviour for the app. If the app needs a hook, add a read-only accessor.
  - Comments only where the code can't speak.
- The fakes live in the test project. Keep them there (linked) so the tests and the app can't
  drift apart.
- `Plugin.*` statics, `Natives.*`, `DebugBotControl`, `PerRole` and `AoeQuery.Evaluated` are
  global. Unsubscribe and reinstall on restart.
- The test harness stops its loop on `Game.Paused` unless it's `PausedByUser` (the death freeze
  sets `Paused`). The app should do the same: show the death overlay and offer a restart.
- While the NatMech dev plugin is loaded in the game, a Release build of the plugin fails at the
  packager zip (locked log in `bin/Release/anomech-logs`). Build and test in Debug, or `-o` a
  scratch folder.
- Verify:
  - `dotnet build AnoMech.sln -c Debug`
  - `dotnet test AnoMech.sln -c Debug`
  - Black Hole sweep:
    `dotnet test tests/AnoMech.Tests -c Debug --filter "FullyQualifiedName~SunnyDay&Name~Black"`
    with a runsettings file setting `SunnySeeds` (see ScenarioCatalogTests).

## Status log

- 2026-10-11: plan written.
- 2026-10-11: M0 done. `AnoMech.Standalone` (NatMechPractice.exe) builds in the solution with
  linked fakes and an app-side `DalamudServices` (namespace AnoMech.Tests, no NUnit) in `Host/`;
  datamining download moved to `build/Datamining.targets`; all tests pass.
