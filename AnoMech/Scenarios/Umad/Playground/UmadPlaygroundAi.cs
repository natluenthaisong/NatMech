using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.Playground;

public sealed class UmadPlaygroundAi : IScenarioAi<SimParty>
{
    public string Name => "Conga train";

    private const float TrainSpeed = 7f;

    public void Run(SimParty party, SimWorld world) => world.Events.Add(0.5f, () => FollowTheLeader(party));

    private static void FollowTheLeader(SimParty party)
    {
        SimCharacter? ahead = party.Player;
        for (var seat = 0; seat < 8; seat++)
        {
            if (party.Get(seat) is not { } member || ReferenceEquals(member, party.Player)) continue;
            member.Follow(ahead, TrainSpeed);
            ahead = member;
        }
    }
}
