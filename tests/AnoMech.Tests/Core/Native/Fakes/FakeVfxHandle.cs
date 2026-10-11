using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeVfxHandle : IActorVfxProxy, IStaticVfxProxy
{
    public string Path { get; init; } = "";
    public Placement Placement { get; private set; }
    public Vector3 Scale { get; private set; }
    public int Moves { get; private set; }
    public bool Removed { get; private set; }
    public Vector4 Color { get; private set; } = Vector4.One;

    public FakeVfxHandle() { }

    public FakeVfxHandle(string path, Placement placement, Vector3 scale)
    {
        Path = path;
        Placement = placement;
        Scale = scale;
    }

    public void Remove() => Removed = true;

    public void SetColor(Vector4 color) => Color = color;

    public void Move(Placement placement, Vector3 scale)
    {
        Placement = placement;
        Scale = scale;
        Moves++;
    }
}
