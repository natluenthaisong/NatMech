using System.Globalization;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeVfxFunctions : IVfxFunctions
{
    public List<FakeVfxHandle> Static { get; } = new();

    public IStaticVfxProxy? SpawnStatic(string path, Placement placement, Vector3 scale)
    {
        var handle = new FakeVfxHandle(path, placement, scale);
        Static.Add(handle);
        return handle;
    }

    public string? LockonIconName(uint lockonId) => lockonId.ToString(CultureInfo.InvariantCulture);
}
