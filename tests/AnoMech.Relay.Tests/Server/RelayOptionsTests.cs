using System.Net;

namespace AnoMech.Relay.Tests;

public class RelayOptionsTests
{
    [Test]
    public void RailwayIngressRequiresAnAccessToken()
    {
        var options = new RelayOptions { RailwayHttpIngress = true };
        Assert.That(options.Validate(), Is.Not.Null);
        options.AccessToken = AnoMech.Network.RelayWire.NewSecret();
        Assert.That(options.Validate(), Is.Null);
        options.RailwayHttpIngress = false;
        Assert.That(options.Validate(), Is.Not.Null, "other deployments still need a trusted proxy");
    }

    [Test]
    public void MappedProxyCidrIsNormalized()
    {
        Assert.That(RelayOptions.TryParseNetwork("::ffff:192.0.2.0/120", out var mappedNetwork));
        Assert.That(mappedNetwork.Contains(IPAddress.Parse("192.0.2.8")));
    }
}
