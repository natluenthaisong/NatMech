using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.Playground;

// Not a fight: a toy built from Dancing Mad's pieces. P1's Tele-portent arrows stand in a ring around
// Chaos, each pointing at the next and launching you onto it, so you can ride the merry-go-round arrow
// to arrow, out of Exdeath's reach while you ride. Exdeath walks you down, faster as time goes on;
// Chaos beams the spot you stood on a moment ago. Ride a whole lap and Exdeath gets Confused and
// Thunder IIIs Chaos instead, and after the second hit Chaos Damning Edicts him back, just for the
// meme. It runs until you drop, then reports how long you lasted against your best.
public sealed class UmadPlaygroundScenario : IScenario
{
    public string Name => "Playground: Merry-go-round";
    public IPhase Phase => UmadZone.P3;
    public bool SupportsSolo => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [];

    private const int ArrowCount = 8;
    private const float RingRadius = 13f;
    private const float ArrowsAt = 1f;
    private const float ArrowTriggerRadius = 2f;
    private const float ArrowTriggerDwell = 0.15f;
    // The carry's length; you can ride again as soon as you land.
    private const float LaunchBind = 0.8f;

    private const float ExdeathStart = 3f;
    private const float ExdeathStartSpeed = 3.5f;
    private const float ExdeathTopSpeed = 6f;
    private const float ExdeathSpeedUpPerSecond = 0.04f;
    private const float CatchMargin = 0.4f;
    private const float CatchCooldown = 2f;

    // A full lap of launches without stepping off.
    private const float ConfusedSeconds = 12f;
    private const float ThunderCastAt = 0.5f;
    private const float ThunderHitsAt = 5.5f;   // ThunderIII_Cast's 5s bar, then two hits 3s apart
    private const float ThunderSecondHit = 3f;
    // Damning Edict's 5s bar plus its lock: Chaos leaves you alone till it's out.
    private const float FightBackSeconds = 6.2f;

    private const float ChaosStart = 5f;
    private const float BeamInterval = 1.5f;
    // How far behind you the beam aims: where you stood this long ago.
    private const float BeamLag = 0.6f;
    private const float BeamWarning = 0.5f;
    private const string BeamOmenPath = "vfx/omen/eff/general02f.avfx";
    private static readonly Vector3 BeamOmenScale = new(3f, 1f, 40f);

    private SimWorld world = null!;
    private SimEnemy? chaos;
    private SimEnemy? exdeath;
    private SimEnemy? thunderHelper;
    private int lapLaunches;
    private float confusedFor;
    private int thunderHits;
    private float chaosBusyUntil;
    private readonly List<SimEventObject?> arrows = [];
    private readonly Dictionary<SimEventObject, float> arrowDwell = [];
    private readonly List<(float At, Vector3 Position)> trail = [];
    private float launchCooldown;
    private float catchCooldown;
    private int launches;
    // Caught by Exdeath, or would have been in god mode.
    internal int Catches { get; private set; }
    internal int ConfusedExdeaths { get; private set; }
    internal int FightBacks { get; private set; }
    private int beamsDodged;
    private bool over;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        arrows.Clear();
        arrowDwell.Clear();
        trail.Clear();
        launchCooldown = 0f;
        catchCooldown = 0f;
        launches = 0;
        Catches = 0;
        ConfusedExdeaths = 0;
        FightBacks = 0;
        lapLaunches = 0;
        confusedFor = 0f;
        thunderHits = 0;
        chaosBusyUntil = 0f;
        beamsDodged = 0;
        over = false;

        world.Events.Add(0f, () => chaos = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.ChaosP3, NameId: BNpcNameId.Chaos, Level: 100,
            Targetable: false, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, Placement: new Placement(Vector3.Zero, 0f))));
        world.Events.Add(0f, () => exdeath = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Exdeath, NameId: BNpcNameId.Exdeath, Level: 100,
            Targetable: false, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, Placement: new Placement(new Vector3(0f, 0f, -16f), 0f))));
        world.Events.Add(0f, () => thunderHelper = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper, Placement: new Placement(Vector3.Zero, 0f))));
        world.Events.Add(ArrowsAt, SpawnArrows);
        world.Events.Add(ChaosStart, FireBeam);
    }

    public void Tick(float delta, float elapsed)
    {
        if (world.Party.Player is not { } player) return;
        if (over) return;
        if (!player.IsAlive())
        {
            End(elapsed);
            return;
        }
        RecordTrail(player, elapsed);
        if (!Riding(player)) lapLaunches = 0;
        TickArrows(player, delta);
        TickExdeath(player, delta, elapsed);
    }

    private void SpawnArrows()
    {
        for (var i = 0; i < ArrowCount; i++)
        {
            var placement = RingSlot(i);
            arrows.Add(world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.TelePortent,
                Placement = placement,
                EventId = EObjId.PropEventId,
                EntityId = Spawn.PlaygroundArrowEntityIdBase + (uint)i,
                Arg2 = ((uint)(Spawn.PlaygroundArrowArg2Serial + i) << 16) | 0x3U,
            }));
        }
    }

    // Clockwise from due south; each arrow points at the next, which is where it lands you.
    private static Placement RingSlot(int index)
    {
        var position = RingPoint(index);
        var toLanding = LandingFrom(index) - position;
        return new Placement(position, MathF.Atan2(toLanding.X, toLanding.Z));
    }

    private static Vector3 RingPoint(int index)
    {
        var angle = index * MathF.Tau / ArrowCount;
        return new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * RingRadius;
    }

    private void TickArrows(SimCharacter player, float delta)
    {
        if (launchCooldown > 0f) { launchCooldown -= delta; return; }
        for (var i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] is not { } arrow) continue;
            var slot = RingSlot(i);
            if (Vector2.Distance(Flat(player.Position), Flat(slot.Position)) > ArrowTriggerRadius)
            {
                arrowDwell.Remove(arrow);
                continue;
            }
            var dwell = arrowDwell.GetValueOrDefault(arrow) + delta;
            arrowDwell[arrow] = dwell;
            if (dwell < ArrowTriggerDwell) continue;
            Launch(player, slot, i);
            arrowDwell.Clear();
            return;
        }
    }

    private void Launch(SimCharacter player, Placement arrow, int index)
    {
        player.AddStatus(StatusId.Bind, LaunchBind);
        (player as ISimPartyMember)?.CarryTo(LandingFrom(index));
        world.SpawnOmen(VfxPath.TeleTrouncingArrowSpawnHit, new Placement(arrow.Position, 0f), Vector3.One, 2f);
        launchCooldown = LaunchBind;
        launches++;
        if (++lapLaunches < ArrowCount) return;
        lapLaunches = 0;
        ConfuseExdeath();
    }

    internal static Vector3 LandingFrom(int index) => RingPoint((index + 1) % ArrowCount);

    private void TickExdeath(SimCharacter player, float delta, float elapsed)
    {
        if (exdeath is null || elapsed < ExdeathStart) return;
        var speed = MathF.Min(ExdeathTopSpeed, ExdeathStartSpeed + (elapsed - ExdeathStart) * ExdeathSpeedUpPerSecond);
        if (confusedFor > 0f)
        {
            confusedFor -= delta;
            exdeath.Follow(chaos, speed);
            return;
        }
        exdeath.Follow(player, speed);
        if (catchCooldown > 0f) { catchCooldown -= delta; return; }
        if (Riding(player)) return;
        var reach = exdeath.HitboxRadius + player.HitboxRadius + CatchMargin;
        if (Vector2.Distance(Flat(exdeath.Position), Flat(player.Position)) > reach) return;
        catchCooldown = CatchCooldown;
        Catches++;
        player.Die(ActionId.DeathBomb, "Exdeath caught you");
    }

    // Purely for show: Chaos takes nothing, but the bar and both hits go up as in the fight.
    private void ConfuseExdeath()
    {
        if (exdeath is null || chaos is null || confusedFor > 0f) return;
        confusedFor = ConfusedSeconds;
        thunderHits = 0;
        ConfusedExdeaths++;
        exdeath.AddStatus(StatusId.Confused, ConfusedSeconds);
        world.Events.Add(ThunderCastAt, () => exdeath?.Cast(ActionId.ThunderIII_Cast, chaos, animationLock: 3.1f));
        world.Events.Add(ThunderHitsAt, ThunderChaos);
        world.Events.Add(ThunderHitsAt + ThunderSecondHit, ThunderChaos);
    }

    private void ThunderChaos()
    {
        if (over || chaos is null) return;
        thunderHelper?.SetPosition(chaos.Position);
        thunderHelper?.Cast(ActionId.ThunderIII_Resolve, chaos);
        if (++thunderHits == 2) ChaosFightsBack();
    }

    private void ChaosFightsBack()
    {
        if (chaos is null || exdeath is null) return;
        var toExdeath = exdeath.Position - chaos.Position;
        chaos.SetPosition(new Placement(chaos.Position, MathF.Atan2(toExdeath.X, toExdeath.Z)));
        chaos.Cast(ActionId.DamningEdict, exdeath, animationLock: 1.1f);
        chaosBusyUntil = world.Events.Elapsed + FightBackSeconds;
        FightBacks++;
    }

    // In the air from a launch, or standing on an arrow: the merry-go-round is safe from Exdeath.
    private bool Riding(SimCharacter player) =>
        launchCooldown > 0f
        || Enumerable.Range(0, ArrowCount).Any(i => Vector2.Distance(Flat(player.Position), Flat(RingPoint(i))) <= ArrowTriggerRadius);

    // A chain of events rather than a Tick timer: the run counts as over once the timeline runs dry.
    private void FireBeam()
    {
        if (over || chaos is null || world.Party.Player is not { } player || !player.IsAlive()) return;
        if (world.Events.Elapsed < chaosBusyUntil)
        {
            world.Events.Add(BeamInterval, FireBeam);
            return;
        }
        var target = WhereYouWere(world.Events.Elapsed - BeamLag) ?? player.Position;
        var along = target - chaos.Position;
        if (new Vector2(along.X, along.Z).LengthSquared() < 0.01f) along = new Vector3(0f, 0f, 1f);
        var rotation = MathF.Atan2(along.X, along.Z);
        chaos.SetPosition(new Placement(chaos.Position, rotation));
        world.SpawnOmen(BeamOmenPath, new Placement(chaos.Position, rotation), BeamOmenScale, BeamWarning);
        world.Events.Add(BeamWarning, () =>
        {
            if (over || chaos is null) return;
            if (chaos.Cast(UmadActions.PlaygroundBeam, chaos.Position + along) is { } shot && shot.Kills.Count == 0) beamsDodged++;
        });
        world.Events.Add(BeamInterval, FireBeam);
    }

    private void RecordTrail(SimCharacter player, float elapsed)
    {
        trail.Add((elapsed, player.Position));
        trail.RemoveAll(t => elapsed - t.At > BeamLag * 3f);
    }

    private Vector3? WhereYouWere(float at) =>
        trail.Count == 0 ? null : trail.MinBy(t => MathF.Abs(t.At - at)).Position;

    private void End(float elapsed)
    {
        over = true;
        exdeath?.Follow(null);
        var best = Plugin.Config is { } config ? config.BestPlaygroundSeconds : 0f;
        var record = elapsed > best;
        if (record && Plugin.Config is { } save)
        {
            save.BestPlaygroundSeconds = elapsed;
            save.Save();
        }
        world.Announce($"Merry-go-round: lasted {elapsed:F1}s, {launches} launches, {beamsDodged} beams dodged"
                       + (record ? " -- new best!" : $" (best {best:F1}s)"));
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
