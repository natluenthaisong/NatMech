using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Tests;

internal sealed class FakeMarkings : IMarkings
{
    public Dictionary<Sign, GameObjectId> Marks { get; } = new();
    public void Set(Sign sign, GameObjectId target) => Marks[sign] = target;
    public void Clear(Sign sign) => Marks.Remove(sign);
    public void ClearAll() => Marks.Clear();
}
