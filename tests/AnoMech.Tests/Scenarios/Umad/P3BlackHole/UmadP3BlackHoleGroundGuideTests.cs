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
        guide.Show([
            new GuideRing(new Vector3(3f, 0f, 4f), 1.5f, GuideColour.Spot),
            new GuideRing(new Vector3(9f, 0f, 9f), 2f, GuideColour.Held, OverlayOnly: true),
            new GuideLine(Vector3.Zero, new Vector3(3f, 0f, 4f), GuideColour.Spot),
            new GuideText(Vector3.Zero, "Hold here", GuideColour.Spot),
        ]);

        var live = Live(vfx);
        Assert.That(live.Select(h => h.Path), Is.EquivalentTo(new[] { Circle, Rect }));
        var ring = live.Single(h => h.Path == Circle);
        Assert.That(ring.Scale, Is.EqualTo(new Vector3(1.5f, 1f, 1.5f)));
        var line = live.Single(h => h.Path == Rect);
        Assert.That(line.Scale.Z, Is.EqualTo(5f).Within(1e-4f), "the line reaches the spot");
        Assert.That(line.Placement.Rotation, Is.EqualTo(MathF.Atan2(3f, 4f)).Within(1e-4f), "faces the spot");
    }

    [Test]
    public void MarkersMoveInPlaceAndHideWhenTheGuideHasNothingToShow()
    {
        FakeGame.Install();
        using var world = new SimWorld(new EventScheduler());
        var vfx = (FakeVfxFunctions)Natives.Vfx;
        var guide = new UmadP3BlackHoleGroundGuide(world);
        guide.Show([new GuideLine(Vector3.Zero, new Vector3(0f, 0f, 6f), GuideColour.Hint)]);
        guide.Show([new GuideLine(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 6f), GuideColour.Hint)]);
        Assert.That(vfx.Static, Has.Count.EqualTo(1), "moved, not respawned");
        Assert.That(vfx.Static[0].Moves, Is.EqualTo(1));

        guide.Show([]);
        Assert.That(Live(vfx), Is.Empty);
        guide.Show([new GuideLine(Vector3.Zero, new Vector3(0f, 0f, 6f), GuideColour.Hint)]);
        Assert.That(Live(vfx), Has.Count.EqualTo(1), "shown again once there is something to show");
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
        guide.Show(shapes, red);
        Assert.That(vfx.Static.Select(h => h.Color), Is.All.EqualTo(red));
        Assert.That(red.X, Is.GreaterThan(red.Y).And.GreaterThan(red.Z), "starts red");

        var later = UmadP3BlackHoleGroundGuide.Rainbow(2.0 / 3.0, brightness: 2f);
        guide.Show(shapes, later);
        Assert.That(vfx.Static.Select(h => h.Color), Is.All.EqualTo(later));
        Assert.That(later.Y, Is.GreaterThan(later.X), "a third of the way round it has turned green");
        Assert.That(new[] { later.X, later.Y, later.Z }, Is.All.GreaterThan(0f), "never blacks out a channel");
    }
}
