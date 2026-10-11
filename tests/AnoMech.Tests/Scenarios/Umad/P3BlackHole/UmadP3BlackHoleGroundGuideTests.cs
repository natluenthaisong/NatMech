using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

public class UmadP3BlackHoleGroundGuideTests
{
    private const string Circle = "vfx/omen/eff/general_1bpf.avfx";
    private const string Rect = "vfx/omen/eff/general02_pf.avfx";

    private static List<FakeVfxHandle> Live(FakeVfxFunctions vfx) => vfx.Static.Where(h => !h.Removed).ToList();

    [Test]
    public void RingsAndLinesBecomeFriendlyOmensAndTextStaysOverlayOnly()
    {
        FakeGame.Install();
        using var world = new SimWorld(new EventScheduler());
        var vfx = (FakeVfxFunctions)Natives.Vfx;
        var guide = new UmadP3BlackHoleGroundGuide(world);
        guide.Show(seconds: 0, shapes: [
            new GuideRing(new Vector3(3f, 0f, 4f), 1.5f, GuideColour.Spot),
            new GuideRing(new Vector3(9f, 0f, 9f), 2f, GuideColour.Held, OverlayOnly: true),
            new GuideLine(Vector3.Zero, new Vector3(3f, 0f, 4f), GuideColour.Spot),
            new GuideText(Vector3.Zero, "Hold here", GuideColour.Spot),
        ]);

        var live = Live(vfx);
        Assert.That(live.Count(h => h.Path == Circle), Is.EqualTo(1), "text and overlay-only shapes stay off the floor");
        var ring = live.Single(h => h.Path == Circle);
        Assert.That(ring.Scale, Is.EqualTo(new Vector3(1.5f, 1f, 1.5f)));
        var arms = live.Where(h => h.Path == Rect).ToList();
        Assert.That(arms, Has.Count.EqualTo(6), "the 5y line becomes three > marks of two arms each");
        Assert.That(arms.Select(a => a.Scale.Z), Is.All.EqualTo(GuideChevrons.ArmLength * MathF.Sqrt(2f)).Within(1e-4f));
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

    [Test]
    public void MarkersMoveInPlaceAndHideWhenTheGuideHasNothingToShow()
    {
        FakeGame.Install();
        using var world = new SimWorld(new EventScheduler());
        var vfx = (FakeVfxFunctions)Natives.Vfx;
        var guide = new UmadP3BlackHoleGroundGuide(world);
        GuideShape[] line = [new GuideLine(Vector3.Zero, new Vector3(0f, 0f, 6f), GuideColour.Hint)];
        guide.Show(line, seconds: 0);
        var spawned = vfx.Static.Count;
        guide.Show(line, seconds: 0.2);
        Assert.That(vfx.Static, Has.Count.EqualTo(spawned), "the marks slide by moving, not respawning");
        Assert.That(vfx.Static.Select(h => h.Moves), Is.All.EqualTo(1));

        guide.Show([], seconds: 0.2);
        Assert.That(Live(vfx), Is.Empty);
        guide.Show(line, seconds: 0.2);
        Assert.That(Live(vfx), Has.Count.EqualTo(spawned), "shown again once there is something to show");
        world.Despawn();
        Assert.That(Live(vfx), Is.Empty, "a world reset clears the floor");
    }

    [Test]
    public void RainbowTintReachesEveryMarkerAndCyclesWithTime()
    {
        FakeGame.Install();
        using var world = new SimWorld(new EventScheduler());
        var vfx = (FakeVfxFunctions)Natives.Vfx;
        var guide = new UmadP3BlackHoleGroundGuide(world);
        GuideShape[] shapes = [
            new GuideRing(new Vector3(3f, 0f, 4f), 1f, GuideColour.Hint),
            new GuideLine(Vector3.Zero, new Vector3(3f, 0f, 4f), GuideColour.Hint),
        ];
        var red = UmadP3BlackHoleGroundGuide.Rainbow(0, brightness: 2f);
        guide.Show(shapes, 0, red);
        Assert.That(vfx.Static.Select(h => h.Color), Is.All.EqualTo(red));
        Assert.That(red.X, Is.GreaterThan(red.Y).And.GreaterThan(red.Z), "starts red");

        var later = UmadP3BlackHoleGroundGuide.Rainbow(2.0 / 3.0, brightness: 2f);
        guide.Show(shapes, 0, later);
        Assert.That(vfx.Static.Select(h => h.Color), Is.All.EqualTo(later));
        Assert.That(later.Y, Is.GreaterThan(later.X), "a third of the way round it has turned green");
        Assert.That(new[] { later.X, later.Y, later.Z }, Is.All.GreaterThan(0f), "never blacks out a channel");
    }
}
