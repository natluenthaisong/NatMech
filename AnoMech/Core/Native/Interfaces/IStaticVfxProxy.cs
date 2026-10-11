using System.Numerics;

namespace AnoMech.Core.Native.Interfaces;

// A VFX placed in the world (VfxObject), e.g. an omen.
public interface IStaticVfxProxy
{
    void Remove();

    // Multiplies the effect's own colours (RGBA); 1,1,1,1 leaves it as authored.
    void SetColor(Vector4 color);
}
