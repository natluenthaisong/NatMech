using System.Collections.Generic;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public static class UmadP3BlackHoleMarkers
{
    public static Dictionary<PartyRole, Sign> Assign(
        IReadOnlyList<PartyRole> roles, UmadP3BlackHoleAi.TetherOrder order)
    {
        var supportFirst = order == UmadP3BlackHoleAi.TetherOrder.SupportDpsAccretion;
        var first = supportFirst ? 0 : 4;
        var second = supportFirst ? 4 : 0;
        return new Dictionary<PartyRole, Sign>
        {
            [roles[first]] = Sign.Attack1,
            [roles[second]] = Sign.Attack2,
            [roles[3]] = Sign.Attack3,
            [roles[first + 1]] = Sign.Bind1,
            [roles[second + 1]] = Sign.Bind2,
            [roles[7]] = Sign.Bind3,
            [roles[first + 2]] = Sign.Ignore1,
            [roles[second + 2]] = Sign.Ignore2,
        };
    }
}
