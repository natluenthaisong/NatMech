using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

public class UmadP3BlackHoleGroundGuideTests
{
    private const string Circle = "vfx/omen/eff/general_1bpf.avfx";

    private static List<FakeVfxHandle> Live(FakeVfxFunctions vfx) => vfx.Static.Where(h => !h.Removed).ToList();

    private static List<SimEventObject> Arrows(SimWorld world) =>
        world.Children.OfType<SimEventObject>()
             .Where(o => o.IsActive && o.EObjRowId == UmadConstants.EObjId.TelePortent)
             .ToList();

    private static (SimWorld World, FakeVfxFunctions Vfx, UmadP3BlackHoleGroundGuide Guide) Setup()
    {
        FakeGame.Install();
        var world = new SimWorld(new EventScheduler());
        return (world, (FakeVfxFunctions)Natives.Vfx, new UmadP3BlackHoleGroundGuide(world));
    }

    [Test]
    public void SpotsBecomeFriendlyCirclesAndTextStaysOverlayOnly()
    {
        var (world, vfx, guide) = Setup();
        using var _ = world;
        guide.Show([
            new GuideRing(new Vector3(3f, 0f, 4f), 1.5f, GuideColour.Spot),
            new GuideRing(new Vector3(9f, 0f, 9f), 2f, GuideColour.Held, OverlayOnly: true),
            new GuideText(Vector3.Zero, "Hold here", GuideColour.Spot),
        ]);
        var live = Live(vfx);
        Assert.That(live.Select(h => h.Path), Is.EqualTo(new[] { Circle }));
        Assert.That(live[0].Scale, Is.EqualTo(new Vector3(1.5f, 1f, 1.5f)));
    }

    [Test]
    public void ASpotThatChangesIsRespawnedThereAndHiddenWhenGone()
    {
        var (world, vfx, guide) = Setup();
        using var _ = world;
        guide.Show([new GuideRing(new Vector3(3f, 0f, 4f), 1f, GuideColour.Hint)]);
        guide.Show([new GuideRing(new Vector3(3f, 0f, 4f), 1f, GuideColour.Hint)]);
        Assert.That(vfx.Static, Has.Count.EqualTo(1), "an unchanged spot is left alone");
        guide.Show([new GuideRing(new Vector3(-5f, 0f, 0f), 1f, GuideColour.Hint)]);
        Assert.That(Live(vfx).Single().Placement.Position, Is.EqualTo(new Vector3(-5f, 0f, 0f)));
        guide.Show([]);
        Assert.That(Live(vfx), Is.Empty);
        guide.Show([new GuideRing(new Vector3(-5f, 0f, 0f), 1f, GuideColour.Hint)]);
        world.Despawn();
        Assert.That(Live(vfx), Is.Empty, "a world reset clears the floor");
    }

    [Test]
    public void ALineBecomesTelePortentArrowsPointingAtItsEndForThisPlayerOnly()
    {
        var (world, _, guide) = Setup();
        using var __ = world;
        var to = new Vector3(0f, 0f, 15f);
        guide.Show([new GuideLine(Vector3.Zero, to, GuideColour.Hint)]);
        var arrows = Arrows(world);
        Assert.That(arrows, Has.Count.EqualTo(3));
        Assert.That(arrows.Select(a => a.SpawnConfig!.Placement.Rotation), Is.All.EqualTo(0f).Within(1e-4f), "pointing south, at the end");
        Assert.That(arrows.Select(a => MathF.Round(a.SpawnConfig!.Placement.Position.Z, 2)), Is.EquivalentTo(new[] { 12.5f, 7.5f, 2.5f }));
        Assert.That(arrows.Select(a => a.SpawnConfig!.LocalOnly), Is.All.True);
    }

    [Test]
    public void WalkingAlongTheTrailDropsPassedArrowsAndATurnLaysItAgain()
    {
        var (world, _, guide) = Setup();
        using var _ = world;
        var to = new Vector3(0f, 0f, 15f);
        guide.Show([new GuideLine(Vector3.Zero, to, GuideColour.Hint)]);
        var first = Arrows(world);

        guide.Show([new GuideLine(new Vector3(0f, 0f, 5f), to, GuideColour.Hint)]);
        var walked = Arrows(world);
        Assert.That(walked, Is.SubsetOf(first), "no arrow is re-laid while you walk straight at the end");
        Assert.That(walked.Select(a => MathF.Round(a.SpawnConfig!.Placement.Position.Z, 2)), Is.EquivalentTo(new[] { 12.5f, 7.5f }));

        guide.Show([new GuideLine(new Vector3(10f, 0f, 5f), to, GuideColour.Hint)]);
        var turned = Arrows(world);
        Assert.That(turned, Is.Not.Empty.And.No.AnyOf(first.ToArray()));
        var heading = MathF.Atan2(-10f, 10f);
        Assert.That(turned.Select(a => MathUtil.NormalizeRotation(a.SpawnConfig!.Placement.Rotation - heading)), Is.All.EqualTo(0f).Within(1e-3f));

        guide.Show([]);
        Assert.That(Arrows(world), Is.Empty);
    }

    [Test]
    public void RainbowTintReachesEverySpotAndCyclesWithTime()
    {
        var (world, vfx, guide) = Setup();
        using var _ = world;
        GuideShape[] shapes = [new GuideRing(new Vector3(3f, 0f, 4f), 1f, GuideColour.Hint)];
        var red = UmadP3BlackHoleGroundGuide.Rainbow(0, brightness: 2f);
        guide.Show(shapes, red);
        Assert.That(Live(vfx).Single().Color, Is.EqualTo(red));
        Assert.That(red.X, Is.GreaterThan(red.Y).And.GreaterThan(red.Z), "starts red");

        var later = UmadP3BlackHoleGroundGuide.Rainbow(2.0 / 3.0, brightness: 2f);
        guide.Show(shapes, later);
        Assert.That(Live(vfx).Single().Color, Is.EqualTo(later));
        Assert.That(later.Y, Is.GreaterThan(later.X), "a third of the way round it has turned green");
        Assert.That(new[] { later.X, later.Y, later.Z }, Is.All.GreaterThan(0f), "never blacks out a channel");
    }

    [Test]
    public void ChevronsPointAtTheTargetAndSlideTowardIt()
    {
        var to = new Vector3(0f, 0f, 9f);
        var now = GuideChevrons.Arms(Vector3.Zero, to, 0.2).ToList();
        var later = GuideChevrons.Arms(Vector3.Zero, to, 0.3).ToList();
        Assert.That(now.Select(a => a.Tip.Z - a.Back.Z), Is.All.GreaterThan(0f), "every arm closes toward the target");
        Assert.That(later[0].Tip.Z - now[0].Tip.Z, Is.EqualTo(GuideChevrons.Speed * 0.1f).Within(1e-4f), "the marks slide forward");
        Assert.That(now.Concat(later).Select(a => a.Tip.Z), Is.All.InRange(0f, 9f), "never past the target");
    }

    private static List<SimEnemy> Beacons(SimWorld world) =>
        world.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.SpawnConfig.LocalOnly).ToList();

    [Test]
    public void ABeaconHelperWearsThePlaystationMarkerOverTheSpotAndFollowsIt()
    {
        var (world, _, guide) = Setup();
        using var _ = world;
        var circle = UmadConstants.LockonId.PlaystationCircle;
        for (var frame = 0; frame < 20; frame++) guide.Show([new GuideBeacon(new Vector3(3f, 0f, 4f), circle)]);
        var helper = Beacons(world).Single();
        Assert.That(helper.Position, Is.EqualTo(new Vector3(3f, 0f, 4f)));
        Assert.That(helper.DrainPendingLockonVfxIds(out var dropped), Is.EqualTo(new[] { circle }), "marked once the actor exists");

        guide.Show([new GuideBeacon(new Vector3(-6f, 0f, 1f), circle)]);
        Assert.That(Beacons(world).Single(), Is.SameAs(helper), "a moved spot moves the helper");
        Assert.That(helper.Position, Is.EqualTo(new Vector3(-6f, 0f, 1f)));

        guide.Show([new GuideBeacon(new Vector3(-6f, 0f, 1f), UmadConstants.LockonId.PlaystationTriangle)]);
        Assert.That(Beacons(world).Single(), Is.Not.SameAs(helper), "a new symbol gets a fresh helper");

        guide.Show([]);
        Assert.That(Beacons(world), Is.Empty);
    }

    private static List<SimTether> Tethers(SimWorld world) =>
        world.Children.OfType<SimTether>().Where(t => t.IsActive).ToList();

    [Test]
    public void TheSpotTetherRunsFromTheBeaconToYouAndTurnsClearOnArrival()
    {
        var (world, _, guide) = Setup();
        using var _ = world;
        world.CreateParty(19, PartyRole.MainTank);
        var player = world.Party.Player!;
        var far = new Vector3(player.Position.X + 10f, 0f, player.Position.Z);
        GuideShape[] beacon = [new GuideBeacon(far, UmadConstants.LockonId.PlaystationCircle)];
        guide.Show(beacon, spotTether: UmadConstants.TetherId.Closer);
        Assert.That(Tethers(world), Is.Empty, "waits for the helper to exist, like the marker");
        for (var frame = 0; frame < 20; frame++) guide.Show(beacon, spotTether: UmadConstants.TetherId.Closer);
        var tether = Tethers(world).Single();
        Assert.That(tether.TetherId, Is.EqualTo(UmadConstants.TetherId.Closer));
        Assert.That(tether.A, Is.SameAs(Beacons(world).Single()));
        Assert.That(tether.B, Is.SameAs(player));

        guide.Show(beacon, spotTether: UmadConstants.TetherId.Closer, reverseTether: true);
        Assert.That(Tethers(world).Single().A, Is.SameAs(player), "reversed");

        player.SetPosition(far);
        guide.Show(beacon, spotTether: UmadConstants.TetherId.Closer);
        Assert.That(Tethers(world).Single().TetherId, Is.EqualTo(UmadConstants.TetherId.CloserClear), "arrived");

        guide.Show(beacon, spotTether: 0);
        Assert.That(Tethers(world), Is.Empty, "None turns it off");
        guide.Show(beacon, spotTether: UmadConstants.TetherId.Arrow1);
        guide.Show([], spotTether: UmadConstants.TetherId.Arrow1);
        Assert.That(Tethers(world), Is.Empty, "goes with the beacon");
    }
}
