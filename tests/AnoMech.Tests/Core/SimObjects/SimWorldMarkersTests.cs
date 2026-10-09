using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

public class SimWorldMarkersTests
{
    [Test]
    public void ReceivedAssignmentsUseLocalCharactersAndDespawnClearsThem()
    {
        FakeGame.Install();
        using var world = new SimWorld(new EventScheduler());
        world.CreateParty(19, PartyRole.MainTank);
        var fake = (FakeMarkings)Natives.Markings;
        world.SetPartyMarkers(new Dictionary<PartyRole, Sign>
        {
            [PartyRole.MainTank] = Sign.Attack1,
            [PartyRole.CasterDps] = Sign.Bind3,
        });
        Assert.That(fake.Marks[Sign.Attack1], Is.EqualTo(world.Party.Get(PartyRole.MainTank)!.GameObjectId));
        Assert.That(fake.Marks[Sign.Bind3], Is.EqualTo(world.Party.Get(PartyRole.CasterDps)!.GameObjectId));
        world.SetPartyMarkers(new Dictionary<PartyRole, Sign> { [PartyRole.OffTank] = Sign.Ignore1 });
        Assert.That(fake.Marks.Keys, Is.EquivalentTo(new[] { Sign.Ignore1 }));
        world.Despawn();
        Assert.That(world.PartyMarkers, Is.Empty);
        Assert.That(fake.Marks, Is.Empty);
    }

    [Test]
    public void InvalidRolesSignsAndDuplicateAssignmentsAreRejected()
    {
        FakeGame.Install();
        using var world = new SimWorld(new EventScheduler());
        world.SetPartyMarkers(new KeyValuePair<PartyRole, Sign>[]
        {
            new((PartyRole)99, Sign.Attack1),
            new(PartyRole.MainTank, (Sign)99),
            new(PartyRole.MainTank, Sign.Attack1),
            new(PartyRole.MainTank, Sign.Attack2),
            new(PartyRole.OffTank, Sign.Attack1),
            new(PartyRole.CasterDps, Sign.Bind3),
        });
        Assert.That(world.PartyMarkers, Is.EquivalentTo(new Dictionary<PartyRole, Sign>
        {
            [PartyRole.MainTank] = Sign.Attack1,
            [PartyRole.CasterDps] = Sign.Bind3,
        }));
        Assert.That(((FakeMarkings)Natives.Markings).Marks, Is.Empty);
    }
}
