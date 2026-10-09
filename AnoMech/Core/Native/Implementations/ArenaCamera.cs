using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class ArenaCamera : IDisposable
{
    internal const float ExtendedDistance = 50f;

    private readonly Func<nint> cameraAddress;
    private readonly Func<bool> mainCameraActive;
    private SavedCamera? saved;

    private sealed record SavedCamera(nint Address, float MaxDistance, float Distance,
        float InterpDistance, CameraZoomMode ZoomMode);

    internal ArenaCamera(Func<nint>? cameraAddress = null, Func<bool>? mainCameraActive = null)
    {
        this.cameraAddress = cameraAddress ?? GetMainCameraAddress;
        this.mainCameraActive = mainCameraActive ?? IsMainCameraActive;
    }

    internal bool IsZoomedOut { get; private set; }
    internal bool IsAvailable => cameraAddress() != 0 && mainCameraActive();

    internal bool ZoomOut(bool inArena)
    {
        if (!inArena || !IsAvailable) return false;
        var camera = (Camera*)cameraAddress();
        if (camera == null) return false;
        if (IsZoomedOut) return true;
        if (saved != null) Reset();
        saved = new SavedCamera((nint)camera, camera->MaxDistance, camera->Distance,
            camera->InterpDistance, camera->ZoomMode);
        camera->MaxDistance = MathF.Max(camera->MaxDistance, ExtendedDistance);
        camera->ZoomMode = CameraZoomMode.ThirdPerson;
        camera->Distance = MathF.Max(camera->Distance, ExtendedDistance);
        camera->InterpDistance = camera->Distance;
        IsZoomedOut = true;
        return true;
    }

    internal void Tick(bool inArena)
    {
        if (!inArena || !IsZoomedOut)
        {
            Reset();
            return;
        }
        var camera = (Camera*)cameraAddress();
        if (camera == null) return;
        if (saved?.Address != (nint)camera)
        {
            saved = null;
            IsZoomedOut = false;
            return;
        }
        camera->MaxDistance = MathF.Max(camera->MaxDistance, ExtendedDistance);
    }

    internal void Reset()
    {
        IsZoomedOut = false;
        if (saved == null) return;
        var camera = (Camera*)cameraAddress();
        if (camera == null) return;
        // A replaced camera may have freed the saved address; never dereference that pointer.
        if (saved.Address == (nint)camera)
        {
            camera->MaxDistance = saved.MaxDistance;
            camera->Distance = saved.Distance;
            camera->InterpDistance = saved.InterpDistance;
            camera->ZoomMode = saved.ZoomMode;
        }
        saved = null;
    }

    public void Dispose() => Reset();

    private static nint GetMainCameraAddress()
    {
        var manager = CameraManager.Instance();
        return manager == null ? 0 : (nint)manager->Camera;
    }

    private static bool IsMainCameraActive()
    {
        var manager = CameraManager.Instance();
        return manager != null && manager->Camera != null && manager->GetActiveCamera() == manager->Camera;
    }
}
