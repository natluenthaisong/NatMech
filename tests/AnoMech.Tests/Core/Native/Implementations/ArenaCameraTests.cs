using AnoMech.Core.Native.Implementations;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

public unsafe class ArenaCameraTests
{
    [Test]
    public void ZoomIsUnavailableOutsideTheArenaOrOnAnotherCamera()
    {
        var native = new Camera { MaxDistance = 20f, Distance = 8f };
        var address = (nint)(&native);
        var active = true;
        using var camera = new ArenaCamera(() => address, () => active);
        Assert.That(camera.ZoomOut(inArena: false), Is.False);
        active = false;
        Assert.That(camera.ZoomOut(inArena: true), Is.False);
        Assert.That(native.MaxDistance, Is.EqualTo(20f));
        Assert.That(native.Distance, Is.EqualTo(8f));
    }

    [TestCase("reset")]
    [TestCase("leave")]
    [TestCase("unload")]
    public void OriginalViewIsRestored(string action)
    {
        var native = new Camera
        {
            MaxDistance = 20f,
            Distance = 8f,
            InterpDistance = 7f,
            ZoomMode = CameraZoomMode.FirstPerson,
        };
        var address = (nint)(&native);
        using var camera = new ArenaCamera(() => address, () => true);
        Assert.That(camera.ZoomOut(inArena: true), Is.True);
        Assert.That(native.MaxDistance, Is.EqualTo(ArenaCamera.ExtendedDistance));
        Assert.That(native.Distance, Is.EqualTo(ArenaCamera.ExtendedDistance));
        Assert.That(native.ZoomMode, Is.EqualTo(CameraZoomMode.ThirdPerson));
        native.Distance = 30f;
        if (action == "reset") camera.Reset();
        else if (action == "leave") camera.Tick(inArena: false);
        else camera.Dispose();
        Assert.That(camera.IsZoomedOut, Is.False);
        Assert.That(native.MaxDistance, Is.EqualTo(20f));
        Assert.That(native.Distance, Is.EqualTo(8f));
        Assert.That(native.InterpDistance, Is.EqualTo(7f));
        Assert.That(native.ZoomMode, Is.EqualTo(CameraZoomMode.FirstPerson));
    }

    [Test]
    public void ExtendedRangeAllowsManualZoomAndKeepsTheOriginalRestorePoint()
    {
        var native = new Camera { MaxDistance = 20f, Distance = 8f, InterpDistance = 8f };
        var address = (nint)(&native);
        using var camera = new ArenaCamera(() => address, () => true);
        camera.ZoomOut(inArena: true);
        native.MaxDistance = 20f;
        native.Distance = 25f;
        camera.Tick(inArena: true);
        Assert.That(native.MaxDistance, Is.EqualTo(ArenaCamera.ExtendedDistance));
        Assert.That(native.Distance, Is.EqualTo(25f));
        camera.ZoomOut(inArena: true);
        camera.Reset();
        Assert.That(native.Distance, Is.EqualTo(8f));
    }

    [Test]
    public void ReplacedCameraIsNotOverwrittenWithAnOldSnapshot()
    {
        var original = new Camera { MaxDistance = 20f, Distance = 8f };
        var replacement = new Camera { MaxDistance = 15f, Distance = 6f };
        var address = (nint)(&original);
        using var camera = new ArenaCamera(() => address, () => true);
        camera.ZoomOut(inArena: true);
        address = (nint)(&replacement);
        camera.Tick(inArena: true);
        Assert.That(camera.IsZoomedOut, Is.False);
        Assert.That(replacement.MaxDistance, Is.EqualTo(15f));
        Assert.That(replacement.Distance, Is.EqualTo(6f));
    }

    [Test]
    public void RestoreWaitsForTheCameraToBeAvailable()
    {
        var native = new Camera { MaxDistance = 20f, Distance = 8f };
        var originalAddress = (nint)(&native);
        var address = originalAddress;
        using var camera = new ArenaCamera(() => address, () => true);
        camera.ZoomOut(inArena: true);
        address = 0;
        camera.Tick(inArena: false);
        address = originalAddress;
        camera.Tick(inArena: false);
        Assert.That(native.MaxDistance, Is.EqualTo(20f));
        Assert.That(native.Distance, Is.EqualTo(8f));
    }
}
