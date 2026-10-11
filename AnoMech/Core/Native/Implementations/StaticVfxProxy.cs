using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class StaticVfxProxy(VfxObject* vfx) : IStaticVfxProxy
{
    public void Remove() => vfx->CleanupRender();

    public void Move(Placement placement, Vector3 scale)
    {
        vfx->Position = placement.Position;
        vfx->Rotation = Quaternion.CreateFromYawPitchRoll(placement.Rotation, 0f, 0f);
        vfx->Scale = scale;
        // Not Update(0, -1) as at spawn: that restarts the effect, so a marker moved every frame
        // never got past its fade-in and stayed invisible.
        vfx->NotifyTransformChanged();
    }

    public void SetColor(Vector4 color) => vfx->Color = color;
}
