using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Core.Native.Interfaces;

// A VFX placed in the world (VfxObject), e.g. an omen.
public interface IStaticVfxProxy
{
    void Remove();

    // Placement is world space.
    void Move(Placement placement, Vector3 scale);
}
